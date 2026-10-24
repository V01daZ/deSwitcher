using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DeSwitcher.Core;
using DeSwitcher.Runtime;
using HarmonyLib;
using UnityEngine;

namespace DeSwitcher.Modes
{
    public sealed class LoadedMod
    {
        public CSharpModInfo Info;
        public Assembly Assembly;
        public Mod Mod;
        public object GameMeta;
        public bool Initialised;
        public bool MainDone;
        public Sprite Thumbnail;
        public bool ThumbnailResolved;
    }

    public sealed class CSharpModLoader : IModLoader
    {
        internal static CSharpModLoader Instance;

        private readonly Assembly game;
        private readonly string[] references;
        private readonly Dictionary<string, string> rewrites;
        private readonly List<LoadedMod> mods = new List<LoadedMod>();

        public CSharpModLoader(Assembly game)
        {
            Instance = this;
            this.game = game;
            references = Bootstrap.ReferenceLocations(game);
            rewrites = new Dictionary<string, string>
            {
                ["CatalogBehaviour.PerformBeforeSpawn"] = "DeSwitcherCompat",
                ["CatalogBehaviour.PerformAfterSpawn"] = "DeSwitcherCompat",
                ["CatalogBehaviour.PerformMod"] = "DeSwitcherCompat",
            };
        }

        public IEnumerable<LoadedMod> Loaded => mods;

        public IEnumerable<Mod> GetMods() => mods.Select(m => m.Mod);

        public Task<bool> TryLoadAsync(IModSource source, DirectoryInfo directory)
        {
            var info = CSharpModInfo.Read(directory.FullName);
            if (info == null)
                return Task.FromResult(false);

            if (mods.Any(m => string.Equals(m.Info.Folder, info.Folder, StringComparison.OrdinalIgnoreCase)))
                return Task.FromResult(false);

            if (info.FromWorkshop)
                return Task.FromResult(false);

            string[] sources = info.Scripts
                .Select(s => Path.GetFullPath(Path.Combine(info.Folder, s)))
                .ToArray();

            if (sources.Any(s => !DeSwitcher.Core.SafeDir.Confines(info.Folder, s)))
            {
                var bad = new Mod(info, this, source, new FileInfo(Path.Combine(info.Folder, "mod.json")));
                bad.Errors = new[] { "Scripts point outside the mod folder" };
                info.Active = false;
                mods.Add(new LoadedMod { Info = info, Assembly = null, Mod = bad });
                return Task.FromResult(true);
            }

            var result = ModPipeline.Process(info.Folder, sources, info.AssemblyName ?? info.Name,
                info.Identifier, info.Name, info.FromWorkshop, references, rewrites);

            var mod = new Mod(info, this, source, new FileInfo(Path.Combine(info.Folder, "mod.json")));
            if (result.Verdict != null && !result.Verdict.MayLoad)
            {
                mod.Errors = new[] { "Blocked by deSwitcher (" + result.Verdict.Status + ")" };
                info.Active = false;
            }
            else if (result.Error != null && result.Assembly == null)
            {
                mod.Errors = new[] { result.Error };
                info.Active = false;
            }

            object gameMeta = result.Assembly != null ? BuildGameMeta(Path.Combine(info.Folder, "mod.json"), info.Active) : null;
            mods.Add(new LoadedMod { Info = info, Assembly = result.Assembly, Mod = mod, GameMeta = gameMeta });
            Log.Info("Loaded mod " + info.Name + " -> " + (result.Verdict != null ? result.Verdict.Status.ToString() : "error"));
            return Task.FromResult(true);
        }

        public bool HasModWithId(string id) => mods.Any(m => m.Info.Identifier == id);

        public bool TryGetModById(string id, out Mod mod)
        {
            var found = mods.FirstOrDefault(m => m.Info.Identifier == id);
            mod = found?.Mod;
            return found != null;
        }

        public Sprite GetThumbnail(Mod mod)
        {
            var lm = Find(mod);
            if (lm == null)
                return null;
            if (lm.ThumbnailResolved)
                return lm.Thumbnail;
            lm.ThumbnailResolved = true;
            try
            {
                string path = Path.Combine(lm.Info.Folder, lm.Info.ThumbnailPath);
                if (File.Exists(path))
                {
                    var tex = new Texture2D(2, 2);
                    tex.LoadImage(File.ReadAllBytes(path));
                    lm.Thumbnail = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }
            catch
            {
                lm.Thumbnail = null;
            }
            return lm.Thumbnail;
        }

        public void Reload(Mod mod)
        {
        }

        public void Unload(Mod mod)
        {
            var lm = Find(mod);
            if (lm != null && lm.Initialised)
            {
                ModInvoker.Invoke(lm.Assembly, lm.GameMeta, lm.Info.EntryPoint, "OnUnload");
                lm.Initialised = false;
            }
        }

        public void Enable(Mod mod)
        {
            var lm = Find(mod);
            if (lm == null)
                return;
            lm.Info.Active = true;
            lm.Info.SaveActive();
            SyncMetaActive(lm);
            InvokeOnLoad(lm);
        }

        public void Disable(Mod mod)
        {
            var lm = Find(mod);
            if (lm == null)
                return;
            lm.Info.Active = false;
            lm.Info.SaveActive();
            SyncMetaActive(lm);
            Unload(mod);
        }

        public bool IsEnabled(Mod mod) => Find(mod)?.Info.Active ?? false;

        public void OnLoadMap(Map map)
        {
        }

        public void OnUpdate()
        {
        }

        public void OnCreateModEntryUi(Mod mod, ModEntryUiBehaviour component)
        {
        }

        public void Dispose()
        {
            foreach (var lm in mods)
                if (lm.Initialised)
                    ModInvoker.Invoke(lm.Assembly, lm.GameMeta, lm.Info.EntryPoint, "OnUnload");
            mods.Clear();
        }

        public void InvokeOnLoad(LoadedMod lm)
        {
            if (lm.Assembly == null || lm.GameMeta == null || lm.Initialised || !lm.Info.Active)
                return;
            if (ModInvoker.Invoke(lm.Assembly, lm.GameMeta, lm.Info.EntryPoint, "OnLoad"))
                lm.Initialised = true;
        }

        public void InvokeMain(LoadedMod lm)
        {
            if (lm.Assembly == null || lm.GameMeta == null || !lm.Info.Active)
                return;
            ModInvoker.Invoke(lm.Assembly, lm.GameMeta, lm.Info.EntryPoint, "Main");
        }

        private static readonly MethodInfo loadMetaMethod = AccessTools.Method(AccessTools.TypeByName("ModLoader"), "LoadModAt");

        private static object BuildGameMeta(string metaPath, bool active)
        {
            try
            {
                object meta = loadMetaMethod?.Invoke(null, new object[] { metaPath });
                if (meta != null)
                    Traverse.Create(meta).Field("Active").SetValue(active);
                return meta;
            }
            catch (Exception e)
            {
                Log.Warn("Metadata build failed for " + metaPath + ": " + e.Message);
                return null;
            }
        }

        private static void SyncMetaActive(LoadedMod lm)
        {
            if (lm.GameMeta != null)
                Traverse.Create(lm.GameMeta).Field("Active").SetValue(lm.Info.Active);
        }

        private LoadedMod Find(Mod mod) => mods.FirstOrDefault(m => m.Mod == mod);

        public object InfoForGameMeta(object gameMeta) => mods.FirstOrDefault(m => ReferenceEquals(m.GameMeta, gameMeta))?.Info;

        public object GameMetaForInfo(object info) => mods.FirstOrDefault(m => ReferenceEquals(m.Info, info))?.GameMeta;
    }
}
