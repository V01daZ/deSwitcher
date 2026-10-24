using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DeSwitcher.Patcher.Logic
{
    public sealed class QuarantinedMod
    {
        public string Name;
        public string FolderName;
        public string Path;
    }

    public static class Quarantine
    {
        public static string Move(string game, string modFolder)
        {
            string dest = Path.Combine(Paths.Quarantine(game), UniqueName(Paths.Quarantine(game), new DirectoryInfo(modFolder).Name));
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            Directory.Move(modFolder, dest);
            return dest;
        }

        public static string Restore(string game, string quarantinedPath)
        {
            string dest = Path.Combine(Paths.Mods(game), UniqueName(Paths.Mods(game), new DirectoryInfo(quarantinedPath).Name));
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            Directory.Move(quarantinedPath, dest);
            return dest;
        }

        public static void Delete(string quarantinedPath)
        {
            if (Directory.Exists(quarantinedPath))
                Directory.Delete(quarantinedPath, true);
        }

        public static List<QuarantinedMod> List(string game)
        {
            string root = Paths.Quarantine(game);
            if (!Directory.Exists(root))
                return new List<QuarantinedMod>();
            return Directory.EnumerateDirectories(root)
                .Where(d => File.Exists(Path.Combine(d, "mod.json")))
                .Select(d => new QuarantinedMod { FolderName = new DirectoryInfo(d).Name, Path = d, Name = ModMeta.Name(d) })
                .OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static string UniqueName(string parent, string name)
        {
            string candidate = name;
            int n = 2;
            while (Directory.Exists(Path.Combine(parent, candidate)))
                candidate = name + " (" + n++ + ")";
            return candidate;
        }
    }
}
