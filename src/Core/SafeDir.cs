using System.Collections.Generic;
using System.IO;

namespace DeSwitcher.Core
{
    public static class SafeDir
    {
        public static IEnumerable<string> EnumerateFiles(string root, string searchPattern)
        {
            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                string dir = stack.Pop();
                string[] files;
                string[] subs;
                try
                {
                    files = Directory.GetFiles(dir, searchPattern);
                    subs = Directory.GetDirectories(dir);
                }
                catch
                {
                    continue;
                }
                foreach (string f in files)
                    yield return f;
                foreach (string sub in subs)
                {
                    if ((new DirectoryInfo(sub).Attributes & FileAttributes.ReparsePoint) != 0)
                        continue;
                    stack.Push(sub);
                }
            }
        }

        public static bool Confines(string folder, string candidate)
        {
            string root = Path.GetFullPath(folder);
            if (root[root.Length - 1] != Path.DirectorySeparatorChar)
                root += Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(candidate);
            return full.StartsWith(root, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
