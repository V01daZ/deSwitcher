using System.Collections.Generic;
using System.IO;

namespace DeSwitcher.Patcher.Logic
{
    public sealed class ForeignPatch
    {
        public string Id;
        public string[] Files;
    }

    public static class ForeignPatches
    {
        public static List<ForeignPatch> Detect(string game)
        {
            string managed = GameLocator.ManagedDir(game);
            var found = new List<ForeignPatch>();

            AddIfAny(found, "old-ppgmodsrestore", managed,
                "Assembly-CSharp.dll.safemode.bak", "FastComp.dll", "DeswitchCredit.dll");

            if (File.Exists(Path.Combine(managed, "PpgModRuntimeBridge.dll")))
                AddIfAny(found, "flizan", managed, "PpgModRuntimeBridge.dll", "README.md", "LICENSE");

            return found;
        }

        private static void AddIfAny(List<ForeignPatch> into, string id, string managed, params string[] names)
        {
            var present = new List<string>();
            foreach (string name in names)
                if (File.Exists(Path.Combine(managed, name)))
                    present.Add(name);
            if (present.Count > 0)
                into.Add(new ForeignPatch { Id = id, Files = present.ToArray() });
        }

        public static void Clean(string game, ForeignPatch patch)
        {
            string managed = GameLocator.ManagedDir(game);
            foreach (string name in patch.Files)
            {
                string path = Path.Combine(managed, name);
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }
}
