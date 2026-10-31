using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeSwitcher.Core;

namespace DeSwitcher.Patcher.Logic
{
    public sealed class GameContext : IDisposable
    {
        public string Game { get; private set; }
        public string Version { get; private set; } = "?";
        public bool Installed { get; private set; }
        public GuardLists Lists { get; private set; } = GuardLists.Empty;
        public GuardState State { get; private set; } = new GuardState();

        private readonly string payloadDir;

        public GameContext(string payloadDir)
        {
            this.payloadDir = payloadDir;
        }

        public bool Locate(string dir = null)
        {
            Game = dir ?? GameLocator.Find();
            if (Game == null || !GameLocator.IsGame(Game))
                return false;
            Version = GameLocator.Version(Game);
            Installed = Hook.IsApplied(GameLocator.ManagedDir(Game));
            return true;
        }

        private bool guardLoaded;

        public void LoadGuard()
        {
            if (guardLoaded)
                return;
            string stateDir = Paths.State(Game);
            Directory.CreateDirectory(stateDir);
            State = GuardState.Load(stateDir);
            Lists = GuardLists.Load(stateDir, Download);
            guardLoaded = true;
        }

        public List<ModScan> ScanMods(Action<int, int> onProgress = null)
        {
            using (var scanner = new ScanService(CompilerExe, References(), ScanCacheDir))
                return scanner.ScanAll(Paths.Mods(Game), Lists, State, new[] { ShimDll }, onProgress);
        }

        private string ScanCacheDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "deSwitcher", "scan-cache");

        public static bool CanWrite(string dir)
        {
            try
            {
                string probe = Path.Combine(dir, ".dsw_write_test");
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void RefreshInstalled() => Installed = Hook.IsApplied(GameLocator.ManagedDir(Game));

        public string CompilerExe => Path.Combine(payloadDir, "Compiler", "deSwitcher.Compiler.exe");
        public string ShimDll => Path.Combine(payloadDir, "deSwitcher", "deSwitcher.Shim.dll");
        public string BootDll => Path.Combine(payloadDir, "deSwitcher.Boot.dll");

        private string[] References()
        {
            string managed = GameLocator.ManagedDir(Game);
            return Directory.GetFiles(managed, "*.dll")
                .Where(f =>
                {
                    string n = Path.GetFileName(f);
                    return n.StartsWith("UnityEngine", StringComparison.Ordinal)
                        || n.StartsWith("Unity.", StringComparison.Ordinal)
                        || n == "Assembly-CSharp.dll" || n == "Assembly-CSharp-firstpass.dll"
                        || n == "netstandard.dll" || n == "mscorlib.dll" || n == "System.dll"
                        || n == "System.Core.dll" || n == "Newtonsoft.Json.dll" || n == "Ceras.dll"
                        || n == "ModModels.dll";
                })
                .Select(f => Path.GetFileName(f) == "Assembly-CSharp.dll" ? Hook.OriginalGameDll(managed) : f)
                .ToArray();
        }

        private static byte[] Download(string url)
        {
            using (var client = new System.Net.WebClient())
            {
                client.Headers[System.Net.HttpRequestHeader.UserAgent] = "deSwitcher";
                return client.DownloadData(url);
            }
        }

        public List<ForeignPatch> DetectForeign() => ForeignPatches.Detect(Game);

        public void CleanForeign()
        {
            foreach (var patch in ForeignPatches.Detect(Game))
                ForeignPatches.Clean(Game, patch);
        }

        public void Install()
        {
            if (ForeignPatches.Detect(Game).Count > 0)
                throw new InvalidOperationException("foreign-patch");
            Payload.LayDown(payloadDir, Game);
            Hook.Apply(GameLocator.ManagedDir(Game), File.ReadAllBytes(BootDll));
            Installed = true;
        }

        public void Remove()
        {
            Hook.Remove(GameLocator.ManagedDir(Game));
            Payload.Clear(Game);
            Installed = false;
        }

        public bool PayloadOutdated() =>
            Differs(Path.Combine(payloadDir, "deSwitcher"), Paths.Home(Game))
            || Differs(Path.Combine(payloadDir, "Compiler"), Path.Combine(Paths.Home(Game), "Compiler"))
            || Differs(BootDll, Paths.BootDll(Game));

        public static bool GameRunning()
        {
            var running = System.Diagnostics.Process.GetProcessesByName("People Playground");
            foreach (var p in running)
                p.Dispose();
            return running.Length > 0;
        }

        private static bool Differs(string src, string dst)
        {
            if (File.Exists(src))
            {
                if (!File.Exists(dst))
                    return true;
                var a = new FileInfo(src);
                var b = new FileInfo(dst);
                return a.Length != b.Length || !File.ReadAllBytes(src).SequenceEqual(File.ReadAllBytes(dst));
            }
            if (!Directory.Exists(src))
                return false;
            return Directory.GetFiles(src).Any(f => Differs(f, Path.Combine(dst, Path.GetFileName(f))))
                || Directory.GetDirectories(src).Any(d => Differs(d, Path.Combine(dst, Path.GetFileName(d))));
        }

        public void SaveState() => State.Save(Paths.State(Game));

        public void Dispose()
        {
        }
    }
}
