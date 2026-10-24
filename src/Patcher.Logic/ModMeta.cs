using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace DeSwitcher.Patcher.Logic
{
    public sealed class ModInfo
    {
        public string Folder;
        public string Name;
        public string WorkshopId;
        public bool FromWorkshop;
        public string EntryPoint;
        public string AssemblyName;
        public string[] Scripts;
    }

    public static class ModMeta
    {
        public static string Name(string modFolder)
        {
            var info = Read(modFolder);
            return info != null && !string.IsNullOrWhiteSpace(info.Name) ? info.Name : new DirectoryInfo(modFolder).Name;
        }

        public static ModInfo Read(string modFolder)
        {
            string file = Path.Combine(modFolder, "mod.json");
            if (!File.Exists(file))
                return null;
            try
            {
                var j = JObject.Parse(File.ReadAllText(file));
                return new ModInfo
                {
                    Folder = modFolder,
                    Name = (string)j["Name"],
                    WorkshopId = (string)j["UGCIdentity"],
                    FromWorkshop = (bool?)j["FromWorkshop"] ?? false,
                    EntryPoint = (string)j["EntryPoint"] ?? "Mod.Mod",
                    AssemblyName = (string)j["AssemblyName"],
                    Scripts = (j["Scripts"] as JArray)?.Select(s => (string)s).ToArray() ?? new string[0],
                };
            }
            catch
            {
                return null;
            }
        }

        public static List<ModInfo> All(string modsDir)
        {
            var result = new List<ModInfo>();
            if (!Directory.Exists(modsDir))
                return result;
            foreach (string meta in DeSwitcher.Core.SafeDir.EnumerateFiles(modsDir, "mod.json"))
            {
                string dir = Path.GetDirectoryName(meta);
                if (dir.IndexOf(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, System.StringComparison.Ordinal) >= 0)
                    continue;
                var info = Read(dir);
                if (info != null)
                    result.Add(info);
            }
            return result;
        }
    }
}
