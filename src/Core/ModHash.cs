using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DeSwitcher.Core
{
    public static class ModHash
    {
        private static readonly string[] ExcludedDirs = { "bin", "obj", "logs" };
        private static readonly string[] ExcludedExtensions = { ".pdb", ".cache", ".log", ".cfg" };
        private static readonly string[] ExcludedNames = { "thumbs.db", "desktop.ini", ".ds_store" };

        public static string Compute(string modFolder)
        {
            string root = Path.GetFullPath(modFolder);
            var files = SafeDir.EnumerateFiles(root, "*")
                .Select(f => new { Full = f, Rel = Normalize(f.Substring(root.Length)) })
                .Where(f => !IsExcluded(f.Rel))
                .OrderBy(f => f.Rel, StringComparer.Ordinal)
                .ToList();

            using (var sha = SHA256.Create())
            {
                foreach (var f in files)
                {
                    byte[] path = Encoding.UTF8.GetBytes(f.Rel);
                    byte[] content = f.Rel == "mod.json" ? CanonicalMeta(File.ReadAllBytes(f.Full)) : File.ReadAllBytes(f.Full);
                    Feed(sha, path);
                    Feed(sha, new byte[] { 0 });
                    Feed(sha, BitConverter.IsLittleEndian ? BitConverter.GetBytes((long)content.Length) : BitConverter.GetBytes((long)content.Length).Reverse().ToArray());
                    Feed(sha, content);
                }
                sha.TransformFinalBlock(new byte[0], 0, 0);
                return ToHex(sha.Hash);
            }
        }

        public static bool IsExcluded(string relativePath)
        {
            string rel = Normalize(relativePath);
            string[] parts = rel.Split('/');
            for (int i = 0; i < parts.Length - 1; i++)
                if (ExcludedDirs.Contains(parts[i]))
                    return true;
            string name = parts[parts.Length - 1];
            return ExcludedNames.Contains(name) || ExcludedExtensions.Any(e => name.EndsWith(e, StringComparison.Ordinal));
        }

        private static byte[] CanonicalMeta(byte[] raw)
        {
            try
            {
                var j = Newtonsoft.Json.Linq.JObject.Parse(Encoding.UTF8.GetString(raw).TrimStart('\uFEFF'));
                j.Remove("Active");
                foreach (var empty in j.Properties().Where(p => p.Value.Type == Newtonsoft.Json.Linq.JTokenType.Null).ToList())
                    empty.Remove();
                return Encoding.UTF8.GetBytes(j.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch
            {
                return raw;
            }
        }

        internal static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static string Normalize(string path) =>
            path.Replace('\\', '/').TrimStart('/').ToLowerInvariant();

        private static void Feed(HashAlgorithm sha, byte[] data) =>
            sha.TransformBlock(data, 0, data.Length, null, 0);
    }
}
