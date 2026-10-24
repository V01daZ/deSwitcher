using System.Reflection;
using HarmonyLib;

namespace DeSwitcher.Modes
{
    public static class Entry
    {
        public static void Apply(Harmony harmony, Assembly game)
        {
            RegistryMode.Apply(harmony, game);
        }
    }
}
