using System;
using System.IO;

namespace DeSwitcher.Runtime
{
    public static class Log
    {
        private static readonly object Gate = new object();
        private static string file;

        public static void Init(string homeDir)
        {
            file = Path.Combine(homeDir, "deSwitcher.log");
            try
            {
                if (File.Exists(file) && new FileInfo(file).Length > 2 * 1024 * 1024)
                    File.Delete(file);
            }
            catch
            {
            }
        }

        public static void Info(string line) => Write("INFO", line);

        public static void Warn(string line) => Write("WARN", line);

        public static void Error(string line) => Write("ERROR", line);

        private static void Write(string level, string line)
        {
            if (file == null)
                return;
            lock (Gate)
            {
                try
                {
                    File.AppendAllText(file, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + level + " " + line + Environment.NewLine);
                }
                catch
                {
                }
            }
        }
    }
}
