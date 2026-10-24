using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DeSwitcher.Boot
{
    public static class Entry
    {
        private static bool started;

        public static string GameDir
        {
            get
            {
                string managed = Path.GetDirectoryName(typeof(Entry).Assembly.Location);
                return Path.GetFullPath(Path.Combine(managed, "..", ".."));
            }
        }

        public static string HomeDir => Path.Combine(GameDir, "deSwitcher");

        public static void Init()
        {
            if (started)
                return;
            started = true;
            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += Resolve;
                Updates.ApplyPending(HomeDir, Log);
                var runtime = Assembly.LoadFrom(Path.Combine(HomeDir, "deSwitcher.Runtime.dll"));
                var game = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                runtime.GetType("DeSwitcher.Runtime.Bootstrap", true).GetMethod("Start").Invoke(null, new object[] { game, HomeDir });
            }
            catch (Exception e)
            {
                Log("Boot failed: " + (e.InnerException ?? e));
            }
        }

        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name;
            var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name);
            if (loaded != null)
                return loaded;
            string file = Path.Combine(HomeDir, name + ".dll");
            return File.Exists(file) ? Assembly.LoadFrom(file) : null;
        }

        public static void Log(string line)
        {
            try
            {
                Directory.CreateDirectory(HomeDir);
                File.AppendAllText(Path.Combine(HomeDir, "deSwitcher.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + line + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
