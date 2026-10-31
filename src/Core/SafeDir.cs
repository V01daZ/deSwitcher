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

        public static IEnumerable<string> ScriptEntries(IEnumerable<string> scripts)
        {
            foreach (string s in scripts)
                if (!string.IsNullOrWhiteSpace(s) && s.Trim().EndsWith(".cs", System.StringComparison.OrdinalIgnoreCase))
                    yield return s.Trim();
        }

        public static string ResolveScript(string folder, string script)
        {
            string direct = null;
            string name = null;
            try
            {
                direct = Path.GetFullPath(Path.Combine(folder, script));
                name = Path.GetFileName(script);
            }
            catch
            {
            }
            bool inside = direct != null && Confines(folder, direct);
            if (inside && File.Exists(direct))
                return direct;
            if (!string.IsNullOrEmpty(name) && name.IndexOfAny(new[] { '*', '?' }) < 0)
            {
                foreach (string found in EnumerateFiles(folder, name))
                    if (string.Equals(Path.GetFileName(found), name, System.StringComparison.OrdinalIgnoreCase))
                        return found;
            }
            return inside ? direct : null;
        }
    }
}
