using System.Collections.Generic;
using System.Linq;

namespace DeSwitcher.Core
{
    public enum ModStatus
    {
        FromSite,
        Clean,
        Trusted,
        Suspicious,
        Dangerous,
        Blocked
    }

    public sealed class ModVerdict
    {
        public ModStatus Status;
        public string Hash;
        public string CatalogSlug;
        public string Reason;
        public IReadOnlyList<Finding> Findings;

        public bool CanTrust => Status == ModStatus.Dangerous;
        public bool NeedsDecision => Status == ModStatus.Dangerous || Status == ModStatus.Blocked;
        public bool MayLoad => !NeedsDecision;
    }

    public static class Verdict
    {
        public static ModVerdict Evaluate(string hash, string workshopId, string name, IReadOnlyList<Finding> findings, GuardLists lists, GuardState state)
        {
            var v = new ModVerdict { Hash = hash, Findings = findings ?? new Finding[0] };
            var block = lists.Blocklist;

            if (hash != null && block.Hashes.TryGetValue(hash, out string reason)
                || !string.IsNullOrEmpty(workshopId) && block.WorkshopIds.TryGetValue(workshopId, out reason)
                || !string.IsNullOrEmpty(name) && block.Names.TryGetValue(name.Trim(), out reason))
            {
                v.Status = ModStatus.Blocked;
                v.Reason = reason;
                return v;
            }

            if (hash != null && lists.Catalog.Mods.TryGetValue(hash, out string slug))
            {
                v.Status = ModStatus.FromSite;
                v.CatalogSlug = slug;
                return v;
            }

            if (hash != null && state.TrustedHashes.Contains(hash))
            {
                v.Status = ModStatus.Trusted;
                return v;
            }

            if (v.Findings.Any(f => f.Severity == Severity.Dangerous))
                v.Status = ModStatus.Dangerous;
            else if (v.Findings.Any(f => f.Severity == Severity.Suspicious))
                v.Status = ModStatus.Suspicious;
            else
                v.Status = ModStatus.Clean;
            return v;
        }
    }
}
