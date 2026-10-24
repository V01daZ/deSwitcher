using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using DeSwitcher.Core;
using Newtonsoft.Json.Linq;

namespace DeSwitcher.Runtime
{
    public static class Updater
    {
        public const string ManifestUrl = "https://deswitchstudio.com/api/guard/manifest.json";

        public static void CheckInBackground()
        {
            var thread = new Thread(Check) { IsBackground = true };
            thread.Start();
        }

        private static void Check()
        {
            try
            {
                byte[] body = Download(ManifestUrl);
                string sig = Encoding.ASCII.GetString(Download(ManifestUrl + ".sig"));
                if (body == null || !Signature.Verify(body, sig))
                    return;

                var manifest = JObject.Parse(Encoding.UTF8.GetString(body));
                var runtime = manifest["runtime"] as JObject;
                if (runtime == null)
                    return;
                if (!IsNewer((string)runtime["version"], Bootstrap.Version))
                    return;

                string updateDir = Path.Combine(Bootstrap.HomeDir, "update");
                string tmp = updateDir + ".tmp";
                if (Directory.Exists(tmp))
                    Directory.Delete(tmp, true);
                Directory.CreateDirectory(tmp);

                string baseUrl = (string)runtime["base"];
                var files = runtime["files"] as JObject;
                foreach (var f in files.Properties())
                {
                    byte[] fileData = Download(baseUrl + f.Name);
                    if (fileData == null || Hex(fileData) != (string)f.Value)
                    {
                        Directory.Delete(tmp, true);
                        return;
                    }
                    File.WriteAllBytes(Path.Combine(tmp, f.Name), fileData);
                }
                File.WriteAllBytes(Path.Combine(tmp, "manifest.json"), body);
                File.WriteAllText(Path.Combine(tmp, "manifest.json.sig"), sig);

                if (Directory.Exists(updateDir))
                    Directory.Delete(updateDir, true);
                Directory.Move(tmp, updateDir);
                Log.Info("Update " + (string)runtime["version"] + " staged for next launch");
            }
            catch (Exception e)
            {
                Log.Warn("Update check failed: " + e.Message);
            }
        }

        private static bool IsNewer(string candidate, string current)
        {
            try
            {
                return new Version(candidate) > new Version(current);
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

        private static byte[] Download(string url)
        {
            using (var client = new WebClient())
            {
                client.Headers[HttpRequestHeader.UserAgent] = "deSwitcher";
                return client.DownloadData(new Uri(url));
            }
        }
    }
}
