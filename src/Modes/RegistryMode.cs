using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            harmony.Patch(AccessTools.Method(loadStep, "Awake"),
                postfix: new HarmonyMethod(typeof(RegistryMode), nameof(AfterLoadStepAwake)));
            harmony.Patch(AccessTools.Method(loadStep, "Load"),
                prefix: new HarmonyMethod(typeof(RegistryMode), nameof(BeforeLoadStep)));

            var catalog = AccessTools.TypeByName("CatalogBehaviour");
            harmony.Patch(AccessTools.Method(catalog, "Populate"),
                postfix: new HarmonyMethod(typeof(RegistryMode), nameof(AfterPopulate)));

            var perfInst = AccessTools.Method(catalog, "PerformInstantiation");
            if (perfInst != null)
                harmony.Patch(perfInst, postfix: new HarmonyMethod(typeof(RegistryMode), nameof(AfterInstantiate)));

            new GameObject("deSwitcherInit").AddComponent<ModInitDriver>();
        }

        internal static void DriveInit()
        {
            if (loader == null)
                return;
            var main = Traverse.Create(AccessTools.TypeByName("CatalogBehaviour")).Field("Main").GetValue() as UnityEngine.Object;
            if (main == null)
                return;
            foreach (var lm in loader.Loaded.Where(m => m.Info.Active && m.Assembly != null && !m.MainDone).ToList())
            {
                loader.InvokeOnLoad(lm);
                loader.InvokeMain(lm);
                lm.MainDone = true;
            }
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

        private static void AfterLoadStepAwake()
        {
            var registry = AccessTools.TypeByName("ModLoaderRegistry");
            AccessTools.Method(registry, "AddLoader").Invoke(null, new object[] { loader });
            Log.Info("CSharpModLoader registered");
        }

        private static bool BeforeLoadStep(object __instance, ref IEnumerator __result)
        {
            if (loader != null && loader.Loaded.Any())
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
            object main = Traverse.Create(AccessTools.TypeByName("CatalogBehaviour")).Field("Main").GetValue();
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
            var catalog = AccessTools.TypeByName("CatalogBehaviour");
            object main = Traverse.Create(catalog).Field("Main").GetValue();
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

        private void Update()
        {
            RegistryMode.DriveInit();
        }
    }
}
