using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace DeSwitcher.Modes
{
    public sealed class CSharpModInfo : IModInfo
    {
        public string Name { get; set; }
        public string Identifier { get; set; }
        public string[] Authors { get; set; }
        public string Summary { get; set; }
        public string Version { get; set; }
        public string GameVersion { get; set; }
        public string[] Tags { get; set; }
        public string[] Dependencies { get; set; }
        public string[] LoadBefore { get; set; }
        public string[] LoadAfter { get; set; }

        public string EntryPoint;
        public string AssemblyName;
        public string[] Scripts;
        public string Folder;
        public bool Active;
        public bool FromWorkshop;
        public string ThumbnailPath;

        public static CSharpModInfo Read(string folder)
        {
            string file = Path.Combine(folder, "mod.json");
            if (!File.Exists(file))
                return null;
            JObject j;
            try
            {
                j = JObject.Parse(File.ReadAllText(file));
            }
            catch
            {
                return null;
            }
            string[] scripts = (j["Scripts"] as JArray)?.Select(s => (string)s).ToArray() ?? new string[0];
            if (scripts.Length == 0)
                return null;
            return new CSharpModInfo
            {
                Folder = folder,
                Name = (string)j["Name"] ?? new DirectoryInfo(folder).Name,
                Identifier = (string)j["UGCIdentity"] ?? (string)j["Name"] ?? new DirectoryInfo(folder).Name,
                Authors = new[] { (string)j["Author"] ?? "" },
                Summary = (string)j["Description"] ?? "",
                Version = (string)j["ModVersion"] ?? "1.0",
                GameVersion = (string)j["GameVersion"] ?? "",
                Tags = (j["Tags"] as JArray)?.Select(t => (string)t).ToArray() ?? new string[0],
                Dependencies = new string[0],
                LoadBefore = new string[0],
                LoadAfter = new string[0],
                EntryPoint = (string)j["EntryPoint"] ?? "Mod.Mod",
                AssemblyName = (string)j["AssemblyName"],
                Scripts = scripts,
                Active = (bool?)j["Active"] ?? true,
                FromWorkshop = (bool?)j["FromWorkshop"] ?? false,
                ThumbnailPath = (string)j["ThumbnailPath"] ?? "thumb.png",
            };
        }

        public void SaveActive()
        {
            string file = Path.Combine(Folder, "mod.json");
            try
            {
                var j = JObject.Parse(File.ReadAllText(file));
                j["Active"] = Active;
                File.WriteAllText(file, j.ToString());
            }
            catch
            {
            }
        }
    }
}
