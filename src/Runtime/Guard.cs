using System;
using System.IO;
using System.Net;
using DeSwitcher.Core;

namespace DeSwitcher.Runtime
{
    public static class Guard
    {
        private static readonly object Gate = new object();
        public static GuardLists Lists { get; private set; } = GuardLists.Empty;
        public static GuardState State { get; private set; } = new GuardState();
        public static string StateDir { get; private set; }

        public static void Load(string stateDir)
        {
            StateDir = stateDir;
            lock (Gate)
            {
                State = GuardState.Load(stateDir);
                Lists = GuardLists.Load(stateDir, Download);
                Log.Info("Guard lists: blocklist " + Lists.Blocklist.Hashes.Count + " hashes, catalog " + Lists.Catalog.Mods.Count + " mods, fresh=" + Lists.Fresh);
            }
        }

        public static void Trust(string hash)
        {
            lock (Gate)
            {
                State.TrustedHashes.Add(hash);
                State.Save(StateDir);
            }
        }

        private static byte[] Download(string url)
        {
            using (var client = new WebClient())
            {
                client.Headers[HttpRequestHeader.UserAgent] = "deSwitcher";
                var done = new System.Threading.ManualResetEvent(false);
                byte[] result = null;
                Exception error = null;
                client.DownloadDataCompleted += (s, e) =>
                {
                    if (e.Error != null) error = e.Error; else result = e.Result;
                    done.Set();
                };
                client.DownloadDataAsync(new Uri(url));
                if (!done.WaitOne(TimeSpan.FromSeconds(5)))
                {
                    client.CancelAsync();
                    throw new TimeoutException(url);
                }
                if (error != null)
                    throw error;
                return result;
            }
        }
    }
}
