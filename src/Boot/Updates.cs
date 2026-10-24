using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace DeSwitcher.Boot
{
    internal static class Updates
    {
        public static void ApplyPending(string homeDir, Action<string> log)
        {
            string dir = Path.Combine(homeDir, "update");
            if (!Directory.Exists(dir))
                return;
            try
            {
                byte[] manifest = File.ReadAllBytes(Path.Combine(dir, "manifest.json"));
                string sig = File.ReadAllText(Path.Combine(dir, "manifest.json.sig"));
                if (!Verify(manifest, sig))
                {
                    Directory.Delete(dir, true);
                    return;
                }

                var files = (JObject.Parse(Encoding.UTF8.GetString(manifest))["runtime"] as JObject)?["files"] as JObject;
                if (files == null)
                {
                    Directory.Delete(dir, true);
                    return;
                }

                foreach (var f in files.Properties())
                {
                    string staged = Path.Combine(dir, f.Name);
                    if (!File.Exists(staged) || Hex(File.ReadAllBytes(staged)) != (string)f.Value)
                    {
                        Directory.Delete(dir, true);
                        return;
                    }
                }

                foreach (var f in files.Properties())
                    File.Copy(Path.Combine(dir, f.Name), Path.Combine(homeDir, f.Name), true);

                Directory.Delete(dir, true);
                log("Applied staged update");
            }
            catch (Exception e)
            {
                log("Update apply failed: " + e.Message);
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        private static bool Verify(byte[] data, string sig)
        {
            try
            {
                Type t = Type.GetType("DeSwitcher.Core.Signature, deSwitcher.Core");
                return (bool)t.GetMethod("Verify", new[] { typeof(byte[]), typeof(string) }).Invoke(null, new object[] { data, sig });
            }
            catch
            {
                return false;
            }
        }

        private static string Hex(byte[] data)
        {
            using (var sha = SHA256.Create())
            {
                var sb = new StringBuilder();
                foreach (byte b in sha.ComputeHash(data))
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
