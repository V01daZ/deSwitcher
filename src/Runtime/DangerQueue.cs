using System.Collections.Generic;
using DeSwitcher.Core;

namespace DeSwitcher.Runtime
{
    public sealed class DangerEntry
    {
        public string Folder;
        public string Name;
        public ModVerdict Verdict;
    }

    public static class DangerQueue
    {
        private static readonly object Gate = new object();
        private static readonly List<DangerEntry> Pending = new List<DangerEntry>();

        public static void Add(string folder, string name, ModVerdict verdict)
        {
            lock (Gate)
            {
                Pending.RemoveAll(e => e.Folder == folder);
                Pending.Add(new DangerEntry { Folder = folder, Name = name, Verdict = verdict });
            }
        }

        public static DangerEntry Take()
        {
            lock (Gate)
            {
                if (Pending.Count == 0)
                    return null;
                var first = Pending[0];
                Pending.RemoveAt(0);
                return first;
            }
        }

        public static int Count
        {
            get { lock (Gate) return Pending.Count; }
        }
    }
}
