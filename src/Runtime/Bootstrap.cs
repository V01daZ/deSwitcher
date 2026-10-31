using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DeSwitcher.Core;
using HarmonyLib;

namespace DeSwitcher.Runtime
{
    public static class Bootstrap
    {
        public static readonly string Version = typeof(Bootstrap).Assembly.GetName().Version.ToString(3);
        public static string HomeDir { get; private set; }
        public static string CacheDir { get; private set; }
        public static Shape Shape { get; private set; }
        public static bool GuardReady { get; private set; }
        public static string UnsupportedReason { get; private set; }

        public static void Start(Assembly game, string homeDir)
        {
            HomeDir = homeDir;
            Log.Init(homeDir);
            Log.Info("deSwitcher runtime starting");

            try
            {
                Guard.Load(homeDir);
                GuardReady = true;
            }
            catch (Exception e)
            {
                GuardReady = false;
                Log.Error("Guard failed to load: " + e);
            }

            if (game == null)
            {
                UnsupportedReason = "Assembly-CSharp not found";
                Log.Error(UnsupportedReason);
                return;
            }

            PrepareCache(game);

            Shape = GameShape.Detect(game.Location);
            Log.Info("Game shape: " + Shape);

            if (Shape == Shape.Unsupported || !GuardReady)
            {
                UnsupportedReason = !GuardReady ? "protection could not start" : "this game version is not supported yet";
                return;
            }

            try
            {
                var harmony = new Harmony("com.deswitchstudio.deswitcher");
                string dll = Shape == Shape.Registry ? "deSwitcher.Modes.dll" : "deSwitcher.Legacy.dll";
                string entryType = Shape == Shape.Registry ? "DeSwitcher.Modes.Entry" : "DeSwitcher.Legacy.Entry";
                var modes = Assembly.LoadFrom(Path.Combine(homeDir, dll));
                modes.GetType(entryType, true)
                    .GetMethod("Apply")
                    .Invoke(null, new object[] { harmony, game });
                new UnityEngine.GameObject("deSwitcher").AddComponent<DangerWindow>();
                var menuAwake = HarmonyLib.AccessTools.Method(HarmonyLib.AccessTools.TypeByName("MenuController"), "Awake");
                if (menuAwake != null)
                    harmony.Patch(menuAwake, postfix: new HarmonyLib.HarmonyMethod(typeof(Bootstrap), nameof(AfterMenuAwake)));
                Updater.CheckInBackground();
                Log.Info("Patches applied for " + Shape + " mode");
            }
            catch (Exception e)
            {
                UnsupportedReason = "could not install the mod loader";
                Log.Error("Patch failed: " + (e.InnerException ?? e));
            }
        }

        private static void PrepareCache(Assembly game)
        {
            string root = Path.Combine(HomeDir, "Cache");
            string key = game.ManifestModule.ModuleVersionId.ToString("N").Substring(0, 12)
                + "-" + typeof(Bootstrap).Assembly.ManifestModule.ModuleVersionId.ToString("N").Substring(0, 8);
            CacheDir = Path.Combine(root, key);
            try
            {
                Directory.CreateDirectory(CacheDir);
                foreach (string dir in Directory.GetDirectories(root))
                    if (!string.Equals(Path.GetFileName(dir), key, StringComparison.OrdinalIgnoreCase))
                        Directory.Delete(dir, true);
                foreach (string file in Directory.GetFiles(root))
                    File.Delete(file);
            }
            catch (Exception e)
            {
                Log.Warn("Cache prune failed: " + e.Message);
            }
        }

        private static void AfterMenuAwake(object __instance)
        {
            DeswitchCredit.Attach(__instance);
        }

        public static string[] ReferenceLocations(Assembly game)
        {
            string managed = Path.GetDirectoryName(game.Location);
            return Directory.GetFiles(managed, "*.dll")
                .Where(f =>
                {
                    string name = Path.GetFileName(f);
                    return name.StartsWith("UnityEngine", StringComparison.Ordinal)
                        || name.StartsWith("Unity.", StringComparison.Ordinal)
                        || name == "Assembly-CSharp.dll"
                        || name == "Assembly-CSharp-firstpass.dll"
                        || name == "netstandard.dll"
                        || name == "mscorlib.dll"
                        || name == "System.dll"
                        || name == "System.Core.dll"
                        || name == "Newtonsoft.Json.dll"
                        || name == "Ceras.dll"
                        || name == "ModModels.dll";
                })
                .Select(f => Path.GetFileName(f) == "Assembly-CSharp.dll" && File.Exists(f + ".deswitcher.bak") ? f + ".deswitcher.bak" : f)
                .Concat(new[] { Path.Combine(HomeDir, "deSwitcher.Shim.dll") })
                .ToArray();
        }
    }
}
