using System;
using System.IO;
using System.Net;
using System.Threading;
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
                Lists = GuardLists.Load(stateDir, Offline);
            }
            new Thread(Refresh) { IsBackground = true }.Start();
        }

        public static void Trust(string hash)
        {
            lock (Gate)
            {
                State.TrustedHashes.Add(hash);
                State.Save(StateDir);
            }
        }

        private static void Refresh()
        {
            try
            {
                var fresh = GuardLists.Load(StateDir, Download);
                lock (Gate)
                    Lists = fresh;
                Log.Info("Guard lists: blocklist " + fresh.Blocklist.Hashes.Count + " hashes, " + fresh.Blocklist.Names.Count + " names, catalog " + fresh.Catalog.Mods.Count + " mods, fresh=" + fresh.Fresh);
            }
            catch (Exception e)
            {
                Log.Warn("Guard refresh failed: " + e.Message);
            }
        }

        private static byte[] Offline(string url) => throw new IOException("offline");

        private static byte[] Download(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "deSwitcher";
            request.Timeout = 10000;
            request.ReadWriteTimeout = 10000;
            using (var response = request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }
}
