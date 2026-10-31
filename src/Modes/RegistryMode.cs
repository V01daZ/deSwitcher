using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using DeSwitcher.Runtime;
using HarmonyLib;
using UnityEngine;

namespace DeSwitcher.Modes
{
    internal static class RegistryMode
    {
        private static CSharpModLoader loader;
        private static readonly HashSet<Type> bgDone = new HashSet<Type>();

        public static void Apply(Harmony harmony, Assembly game)
        {
            loader = new CSharpModLoader(game);

            WireCompat();

            var loadStep = AccessTools.TypeByName("ModLoadStep");
            harmony.Patch(AccessTools.Method(loadStep, "Load"),
                prefix: new HarmonyMethod(typeof(RegistryMode), nameof(BeforeLoadStep)));

            var catalog = AccessTools.TypeByName("CatalogBehaviour");
            harmony.Patch(AccessTools.Method(catalog, "Populate"),
                postfix: new HarmonyMethod(typeof(RegistryMode), nameof(AfterPopulate)));

            var perfInst = AccessTools.Method(catalog, "PerformInstantiation");
            if (perfInst != null)
                harmony.Patch(perfInst, postfix: new HarmonyMethod(typeof(RegistryMode), nameof(AfterInstantiate)));

            ModLoaderRegistry.AddLoader(loader);
            Log.Info("CSharpModLoader registered");

            new GameObject("deSwitcherInit").AddComponent<ModInitDriver>();
            Preload();
        }

        internal static int PreloadDone;
        internal static int PreloadTotal;
        internal static volatile bool Preloading;
        private static bool preloadStarted;

        internal static int LoadedCount => loader != null ? loader.Loaded.Count() : 0;

        private static void Preload()
        {
            var dir = new DirectoryInfo("./Mods");
            if (!dir.Exists)
                return;
            var folders = DeSwitcher.Core.SafeDir.EnumerateFiles(dir.FullName, "mod.json")
                .Select(Path.GetDirectoryName)
                .Where(d => d.IndexOf(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal) < 0
                    && !d.EndsWith(Path.DirectorySeparatorChar + "bin", StringComparison.Ordinal))
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                .Select(d => new DirectoryInfo(d))
                .ToList();
            preloadStarted = true;
            if (folders.Count == 0)
            {
                Log.Info("Preload: no mod.json found in " + dir.FullName);
                return;
            }
            var source = new ModFolderSource(dir);
            PreloadTotal = folders.Count;
            Preloading = true;
            new Thread(() =>
            {
                foreach (var folder in folders)
                {
                    try
                    {
                        loader.TryLoadAsync(source, folder).Wait();
                    }
                    catch (Exception e)
                    {
                        Log.Error("Preload " + folder.Name + " failed: " + e);
                    }
                    Interlocked.Increment(ref PreloadDone);
                }
                Preloading = false;
                Log.Info("Preloaded " + folders.Count + " mods");
            }) { IsBackground = true }.Start();
        }

        private static readonly FieldInfo catalogMain = AccessTools.Field(AccessTools.TypeByName("CatalogBehaviour"), "Main");

        private static object CatalogMain() => catalogMain?.GetValue(null);

        internal static void DriveInit()
        {
            if (loader == null)
                return;
            MirrorLegacy();
            foreach (var lm in loader.Loaded.Where(m => m.Info.Active && m.Assembly != null && !m.OnLoadTried).ToList())
            {
                lm.OnLoadTried = true;
                loader.InvokeOnLoad(lm);
            }
            var main = CatalogMain() as UnityEngine.Object;
            if (main == null)
                return;
            foreach (var lm in loader.Loaded.Where(m => m.Info.Active && m.Assembly != null && !m.MainDone).ToList())
            {
                loader.InvokeOnLoad(lm);
                loader.InvokeMain(lm);
                lm.MainDone = true;
            }
        }

        private static MethodInfo legacyListChange;

        private static void MirrorLegacy()
        {
            if (loader == null)
                return;
            bool changed = false;
            foreach (var lm in loader.Loaded)
            {
                var meta = lm.GameMeta as ModMetaData;
                if (meta == null)
                    continue;
                if (!ModLoader.LoadedMods.Contains(meta))
                {
                    ModLoader.LoadedMods.Add(meta);
                    changed = true;
                }
                if (meta.Active != lm.Info.Active)
                {
                    if (meta.Active)
                        loader.Enable(lm.Mod);
                    else
                        loader.Disable(lm.Mod);
                    changed = true;
                }
            }
            if (!changed)
                return;
            if (legacyListChange == null)
                legacyListChange = AccessTools.Method(typeof(ModLoader), "InvokeModListChange");
            try
            {
                legacyListChange?.Invoke(null, null);
            }
            catch (Exception e)
            {
                Log.Error("Legacy mod list change failed: " + (e.InnerException ?? e));
            }
            ModLoaderRegistry.OnModLoaded.Invoke();
        }

        public static void AfterInstantiate(object e, GameObject __result)
        {
            if (e == null || __result == null)
                return;
            try
            {
                PerformSpawn("beforeSpawnByAsset", e, __result);
                PerformSpawn("afterSpawnByAsset", e, __result);
                RaiseItemSpawned(e, __result);
            }
            catch (Exception ex)
            {
                Log.Error("Spawn hook failed: " + ex);
            }
        }

        private static Type spawnArgsType;
        private static MethodInfo invokeItemSpawned;

        private static void RaiseItemSpawned(object asset, GameObject instance)
        {
            if (spawnArgsType == null)
                spawnArgsType = AccessTools.TypeByName("UserSpawnEventArgs");
            if (invokeItemSpawned == null)
                invokeItemSpawned = AccessTools.Method(AccessTools.TypeByName("ModAPI"), "InvokeItemSpawned");
            if (spawnArgsType == null || invokeItemSpawned == null)
                return;
            object args = Activator.CreateInstance(spawnArgsType, instance, asset);
            invokeItemSpawned.Invoke(null, new object[] { instance, args });
        }

        private static bool BeforeLoadStep(object __instance, ref IEnumerator __result)
        {
            if (preloadStarted || (loader != null && loader.Loaded.Any()))
            {
                Traverse.Create(__instance).Property("RequestingLoad").SetValue(false);
                __result = Empty();
                return false;
            }
            return true;
        }

        private static IEnumerator Empty()
        {
            yield break;
        }

        private static readonly List<object> savedCategories = new List<object>();

        private static void AfterPopulate()
        {
            try
            {
                foreach (var lm in loader.Loaded.Where(m => m.Info.Active && m.Assembly != null))
                {
                    loader.InvokeOnLoad(lm);
                    if (!lm.MainDone)
                    {
                        loader.InvokeMain(lm);
                        lm.MainDone = true;
                    }
                }
                SyncCategories();
                ApplyModifications();
                InstantiateBackgroundScripts();
            }
            catch (Exception e)
            {
                Log.Error("Catalog population failed: " + e);
            }
        }

        private static void SyncCategories()
        {
            object main = CatalogMain();
            var modded = main != null ? Traverse.Create(main).Field("ModdedCatalog").GetValue() as IList : null;
            if (modded == null)
                return;
            foreach (var c in modded)
                if (c != null && !savedCategories.Contains(c))
                    savedCategories.Add(c);
            foreach (var c in savedCategories)
                if (!modded.Contains(c))
                    modded.Add(c);
        }

        private static void ApplyModifications()
        {
            var manager = AccessTools.TypeByName("ModificationManager");
            var modifications = Traverse.Create(manager).Field("Modifications").GetValue() as IDictionary;
            if (modifications == null)
                return;
            var catalog = AccessTools.TypeByName("CatalogBehaviour");
            object main = Traverse.Create(catalog).Field("Main").GetValue();
            var apply = AccessTools.Method(catalog, "ApplyModification");

            foreach (DictionaryEntry pair in modifications)
            {
                object mod = pair.Key;
                object gameMeta = pair.Value;
                object info = loader.InfoForGameMeta(gameMeta);
                if (info == null)
                    continue;
                try
                {
                    object original = Traverse.Create(mod).Field("OriginalItem").GetValue();
                    ModInvoker.WithMetadata(gameMeta, () => apply.Invoke(main, new[] { mod, info, original }));
                }
                catch (Exception e)
                {
                    Log.Error("ApplyModification failed: " + e);
                }
            }
        }

        private static void InstantiateBackgroundScripts()
        {
            var manager = AccessTools.TypeByName("ModificationManager");
            var scripts = Traverse.Create(manager).Field("BackgroundScripts").GetValue() as IEnumerable;
            if (scripts == null)
                return;
            foreach (Type type in scripts.Cast<Type>())
            {
                if (!bgDone.Add(type))
                    continue;
                try
                {
                    new GameObject("Background script: " + type.Name, type);
                }
                catch (Exception e)
                {
                    Log.Error("Background script " + type.Name + " failed: " + e);
                }
            }
        }

        private static void WireCompat()
        {
            var compat = AccessTools.TypeByName("DeSwitcherCompat");
            if (compat == null)
                return;
            var before = Delegate.CreateDelegate(AccessTools.Field(compat, "BeforeSpawn").FieldType,
                typeof(RegistryMode), nameof(PerformBeforeSpawn));
            var after = Delegate.CreateDelegate(AccessTools.Field(compat, "AfterSpawn").FieldType,
                typeof(RegistryMode), nameof(PerformAfterSpawn));
            Traverse.Create(compat).Field("BeforeSpawn").SetValue(before);
            Traverse.Create(compat).Field("AfterSpawn").SetValue(after);
        }

        public static void PerformBeforeSpawn(object asset, GameObject instance) => PerformSpawn("beforeSpawnByAsset", asset, instance);

        public static void PerformAfterSpawn(object asset, GameObject instance) => PerformSpawn("afterSpawnByAsset", asset, instance);

        private static void PerformSpawn(string field, object asset, GameObject instance)
        {
            object main = CatalogMain();
            var map = Traverse.Create(main).Field(field).GetValue() as IDictionary;
            object info = Traverse.Create(asset).Field("RelevantModMetadata").GetValue();
            if (map == null || info == null || !map.Contains(asset))
                return;
            var action = map[asset] as Action<GameObject>;
            if (action == null)
                return;
            object gameMeta = loader.GameMetaForInfo(info);
            try
            {
                ModInvoker.WithMetadata(gameMeta, () => action(instance));
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
    }

    internal sealed class ModInitDriver : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private int announced;
        private float lastAnnounce;
        private GUIStyle style;

        private float nextDrive;

        private void Update()
        {
            if (Time.unscaledTime >= nextDrive)
            {
                nextDrive = Time.unscaledTime + 0.25f;
                RegistryMode.DriveInit();
            }
            int count = RegistryMode.LoadedCount;
            if (count != announced && (!RegistryMode.Preloading || Time.unscaledTime - lastAnnounce > 1f))
            {
                announced = count;
                lastAnnounce = Time.unscaledTime;
                ModLoaderRegistry.OnModLoaded.Invoke();
            }
        }

        private void OnGUI()
        {
            if (!RegistryMode.Preloading)
                return;
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = new Color(0.24f, 0.88f, 1f) } };
            GUI.Label(new Rect(12, Screen.height - 34, 600, 24),
                "deSwitcher: " + (Application.systemLanguage == SystemLanguage.Russian ?"загрузка модов " : "loading mods ") + RegistryMode.PreloadDone + " / " + RegistryMode.PreloadTotal, style);
        }
    }
}
