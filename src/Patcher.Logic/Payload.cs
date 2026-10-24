using System.IO;

namespace DeSwitcher.Patcher.Logic
{
    public static class Payload
    {
        public static void LayDown(string payloadDir, string game)
        {
            string src = Path.Combine(payloadDir, "deSwitcher");
            string dst = Paths.Home(game);
            CopyDir(src, dst, skipTop: "Quarantine");
            CopyDir(Path.Combine(payloadDir, "Compiler"), Path.Combine(dst, "Compiler"), null);
        }

        public static void Clear(string game)
        {
            string home = Paths.Home(game);
            string quarantine = Paths.Quarantine(game);
            if (Directory.Exists(quarantine) && Directory.GetDirectories(quarantine).Length > 0)
            {
                string kept = Path.Combine(game, "deSwitcher Quarantine");
                if (Directory.Exists(kept))
                    Directory.Delete(kept, true);
                Directory.Move(quarantine, kept);
            }
            if (Directory.Exists(home))
                Directory.Delete(home, true);
        }

        private static void CopyDir(string src, string dst, string skipTop)
        {
            if (!Directory.Exists(src))
                return;
            Directory.CreateDirectory(dst);
            foreach (string file in Directory.GetFiles(src))
                File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), true);
            foreach (string dir in Directory.GetDirectories(src))
            {
                string name = Path.GetFileName(dir);
                if (name == skipTop)
                    continue;
                CopyDir(dir, Path.Combine(dst, name), null);
            }
        }
    }
}
