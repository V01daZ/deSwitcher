using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DeSwitcher.Core;

namespace DeSwitcher.Runtime
{
    public sealed class PipelineResult
    {
        public bool Loaded;
        public Assembly Assembly;
        public ModVerdict Verdict;
        public string Error;
    }

    public static class ModPipeline
    {
        public static PipelineResult Process(string modFolder, string[] sources, string assemblyName, string workshopId, string modName, bool fromWorkshop, string[] references, Dictionary<string, string> rewrites)
        {
            var result = new PipelineResult();

            if (fromWorkshop)
            {
                result.Error = "Workshop mods stay disabled";
                return result;
            }

            string hash = SafeHash(modFolder);
            string output = Path.Combine(Bootstrap.CacheDir, hash + ".dll");
            byte[] assembly = null;

            if (sources != null && sources.Length > 0)
            {
                if (File.Exists(output))
                {
                    assembly = File.ReadAllBytes(output);
                }
                else
                {
                    var outcome = CompilerHost.Compile(assemblyName, sources, references, output, rewrites);
                    if (!outcome.Ok)
                    {
                        result.Error = string.Join("\n", outcome.Errors);
                        result.Verdict = new ModVerdict { Status = ModStatus.Clean, Findings = new Finding[0], Hash = hash };
                        return result;
                    }
                    assembly = File.ReadAllBytes(output);
                }
                hash = SafeHash(modFolder);
            }

            var findings = new List<Finding>(assembly != null ? CodeScanner.Scan(assembly) : new Finding[0]);
            if (!Guard.Lists.Catalog.Mods.ContainsKey(hash) && !Guard.State.TrustedHashes.Contains(hash))
                findings.AddRange(SafeBundledScan(modFolder));
            result.Verdict = Verdict.Evaluate(hash, workshopId, modName, findings, Guard.Lists, Guard.State);

            if (!result.Verdict.MayLoad)
            {
                Log.Warn("Blocked " + modName + " (" + result.Verdict.Status + ")");
                DangerQueue.Add(modFolder, modName, result.Verdict);
                return result;
            }

            if (assembly != null)
            {
                result.Assembly = Assembly.Load(assembly);
                result.Loaded = true;
            }
            return result;
        }

        private static IReadOnlyList<Finding> SafeBundledScan(string folder)
        {
            try
            {
                return CodeScanner.ScanBundled(folder);
            }
            catch (Exception e)
            {
                return new[] { new Finding("unreadable", Severity.Dangerous, "bundled dll scan failed: " + e.Message) };
            }
        }

        private static string SafeHash(string folder)
        {
            try
            {
                return ModHash.Compute(folder);
            }
            catch (Exception e)
            {
                Log.Warn("Hash failed for " + folder + ": " + e.Message);
                return Guid.NewGuid().ToString("N");
            }
        }
    }
}
