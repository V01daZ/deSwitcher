using System.IO;

namespace DeSwitcher.Patcher.Logic
{
    public static class Paths
    {
        public static string Home(string game) => Path.Combine(game, "deSwitcher");

        public static string Quarantine(string game) => Path.Combine(Home(game), "Quarantine");

        public static string State(string game) => Home(game);

        public static string Mods(string game) => Path.Combine(game, "Mods");

        public static string Backup(string game) =>
            GameLocator.ManagedDll(game) + ".deswitcher.bak";

        public static string BootDll(string game) =>
            Path.Combine(GameLocator.ManagedDir(game), "deSwitcher.Boot.dll");
    }
}
