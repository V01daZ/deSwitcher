using System.Collections.Generic;

namespace DeSwitcher.Core
{
    public static class CompatRewrites
    {
        public static Dictionary<string, string> Create() => new Dictionary<string, string>
        {
            ["CatalogBehaviour.PerformBeforeSpawn"] = "DeSwitcherCompat",
            ["CatalogBehaviour.PerformAfterSpawn"] = "DeSwitcherCompat",
            ["CatalogBehaviour.PerformMod"] = "DeSwitcherCompat",
            ["ContraptionSerialiser.SaveThumbnail"] = "DeSwitcherContraptions",
            ["ContraptionSerialiser.Path"] = "DeSwitcherContraptions",
            ["ContraptionOutlineSerialiser.SaveOutline"] = "DeSwitcherContraptions",
            ["GameVersion.GetDifference"] = "LegacyGameVersionUtils",
            ["GameVersion.IsOlderThan"] = "LegacyGameVersionUtils",
            ["GameVersion.IsAtLeast"] = "LegacyGameVersionUtils",
            ["GameVersion.IsNewerThan"] = "LegacyGameVersionUtils",
            ["GameVersion.ParseBuildIndex"] = "LegacyGameVersionUtils",
        };
    }
}
