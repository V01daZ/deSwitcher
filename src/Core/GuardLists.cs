using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DeSwitcher.Core
{
    public sealed class Blocklist
    {
        public Dictionary<string, string> Hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> WorkshopIds = new Dictionary<string, string>(StringComparer.Ordinal);
        public Dictionary<string, string> Names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class Catalog
    {
        public Dictionary<string, string> Mods = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class GuardLists
    {
        public const string BaseUrl = "https://deswitchstudio.com/api/guard/";
        public const string BlocklistUrl = BaseUrl + "blocklist.json";
        public const string CatalogUrl = BaseUrl + "catalog.json";

        public Blocklist Blocklist = new Blocklist();
        public Catalog Catalog = new Catalog();
        public bool Fresh;

        public static GuardLists Empty => new GuardLists();

        public static GuardLists Load(string stateDir, Func<string, byte[]> download) =>
            Load(stateDir, download, Signature.PublicKeyXml);

        internal static GuardLists Load(string stateDir, Func<string, byte[]> download, string publicKeyXml)
        {
            string cache = Path.Combine(stateDir, "lists");
            var result = new GuardLists();
            bool freshBlock = Fetch(download, BlocklistUrl, cache, "blocklist.json", publicKeyXml, out byte[] block);
            bool freshCatalog = Fetch(download, CatalogUrl, cache, "catalog.json", publicKeyXml, out byte[] catalog);
            result.Fresh = freshBlock && freshCatalog;
            if (block != null)
                result.Blocklist = ParseBlocklist(block);
            if (catalog != null)
                result.Catalog = ParseCatalog(catalog);
            foreach (var entry in BuiltInCatalog.Mods)
                if (!result.Catalog.Mods.ContainsKey(entry.Key))
                    result.Catalog.Mods[entry.Key] = entry.Value;
            return result;
        }

        private static bool Fetch(Func<string, byte[]> download, string url, string cache, string name, string key, out byte[] data)
        {
            try
            {
                byte[] body = download(url);
                string sig = Encoding.ASCII.GetString(download(url + ".sig"));
                if (body != null && Signature.Verify(body, sig, key) && VersionOf(body) >= SeenVersion(cache, name))
                {
                    try
                    {
                        Directory.CreateDirectory(cache);
                        File.WriteAllBytes(Path.Combine(cache, name), body);
                        File.WriteAllText(Path.Combine(cache, name + ".sig"), sig);
                        File.WriteAllText(Path.Combine(cache, name + ".ver"), VersionOf(body).ToString());
                    }
                    catch
                    {
                    }
                    data = body;
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                string file = Path.Combine(cache, name);
                byte[] body = File.ReadAllBytes(file);
                string sig = File.ReadAllText(file + ".sig");
                data = Signature.Verify(body, sig, key) ? body : null;
            }
            catch
            {
                data = null;
            }
            return false;
        }

        private static long VersionOf(byte[] body)
        {
            try
            {
                return (long?)Parse(body)?["version"] ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        private static long SeenVersion(string cache, string name)
        {
            try
            {
                return long.Parse(File.ReadAllText(Path.Combine(cache, name + ".ver")).Trim());
            }
            catch
            {
                return 0;
            }
        }

        private static JObject Parse(byte[] data)
        {
            try
            {
                return JToken.Parse(Encoding.UTF8.GetString(data)) as JObject;
            }
            catch
            {
                return null;
            }
        }

        private static void Fill(JObject root, string key, Dictionary<string, string> into)
        {
            if (root?[key] is JObject map)
                foreach (var p in map.Properties())
                    into[p.Name] = p.Value.Type == JTokenType.String ? (string)p.Value : "";
        }

        internal static Blocklist ParseBlocklist(byte[] data)
        {
            var root = Parse(data);
            var b = new Blocklist();
            Fill(root, "hashes", b.Hashes);
            Fill(root, "workshopIds", b.WorkshopIds);
            Fill(root, "names", b.Names);
            return b;
        }

        internal static Catalog ParseCatalog(byte[] data)
        {
            var c = new Catalog();
            Fill(Parse(data), "mods", c.Mods);
            return c;
        }
    }

    public sealed class GuardState
    {
        public HashSet<string> TrustedHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static GuardState Load(string stateDir)
        {
            string file = Path.Combine(stateDir, "state.json");
            var state = new GuardState();
            if (!File.Exists(file))
                return state;
            try
            {
                var root = JObject.Parse(File.ReadAllText(file));
                if (root["trusted"] is JArray arr)
                    foreach (var t in arr)
                        if (t.Type == JTokenType.String)
                            state.TrustedHashes.Add((string)t);
            }
            catch
            {
                try
                {
                    File.Copy(file, file + ".bad", true);
                    File.Delete(file);
                }
                catch
                {
                }
            }
            return state;
        }

        public void Save(string stateDir)
        {
            Directory.CreateDirectory(stateDir);
            string file = Path.Combine(stateDir, "state.json");
            string tmp = file + ".tmp";
            var root = new JObject { ["trusted"] = new JArray(new SortedSet<string>(TrustedHashes, StringComparer.Ordinal)) };
            File.WriteAllText(tmp, root.ToString(Formatting.Indented));
            if (File.Exists(file))
                File.Delete(file);
            File.Move(tmp, file);
        }
    }
}
