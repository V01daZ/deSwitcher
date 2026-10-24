using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using DeSwitcher.Core;
using Newtonsoft.Json.Linq;

namespace DeSwitcher.Patcher.Logic
{
    public sealed class ModScan
    {
        public ModInfo Info;
        public ModVerdict Verdict;
    }

    public sealed class ScanService : IDisposable
    {
        private readonly string compilerExe;
        private readonly string[] references;
        private readonly string cacheDir;
        private Process process;
        private StreamWriter input;
        private int nextId;

        public ScanService(string compilerExe, IEnumerable<string> references, string cacheDir)
        {
            this.compilerExe = compilerExe;
            this.references = references.ToArray();
            this.cacheDir = cacheDir;
            Directory.CreateDirectory(cacheDir);
        }

        public List<ModScan> ScanAll(string modsDir, GuardLists lists, GuardState state, string[] extraReferences = null, Action<int, int> onProgress = null)
        {
            var mods = ModMeta.All(modsDir);
            var result = new List<ModScan>();
            onProgress?.Invoke(0, mods.Count);
            foreach (var mod in mods)
            {
                result.Add(Scan(mod, lists, state, extraReferences));
                onProgress?.Invoke(result.Count, mods.Count);
            }
            return result;
        }

        public ModScan Scan(ModInfo mod, GuardLists lists, GuardState state, string[] extraReferences = null)
        {
            string hash = ModHash.Compute(mod.Folder);
            var findings = new List<Finding>();

            if (!mod.FromWorkshop && mod.Scripts.Length > 0)
            {
                string output = Path.Combine(cacheDir, hash + ".dll");
                byte[] assembly = Build(mod, output, extraReferences);
                if (assembly != null)
                {
                    findings.AddRange(CodeScanner.Scan(assembly));
                    hash = ModHash.Compute(mod.Folder);
                }
                else
                {
                    findings.Add(new Finding("compile-failed", Severity.Suspicious, mod.Name));
                }
            }

            var verdict = Verdict.Evaluate(hash, mod.WorkshopId, mod.Name, findings, lists, state);
            return new ModScan { Info = mod, Verdict = verdict };
        }

        private byte[] Build(ModInfo mod, string output, string[] extraReferences)
        {
            try
            {
                string[] sources = mod.Scripts
                    .Select(s => Path.GetFullPath(Path.Combine(mod.Folder, s)))
                    .ToArray();
                if (sources.Any(s => !DeSwitcher.Core.SafeDir.Confines(mod.Folder, s)))
                    return null;
                if (File.Exists(output))
                    return File.ReadAllBytes(output);
                string[] refs = extraReferences == null ? references : references.Concat(extraReferences).ToArray();
                var request = new JObject
                {
                    ["Id"] = ++nextId,
                    ["AssemblyName"] = string.IsNullOrEmpty(mod.AssemblyName) ? mod.Name : mod.AssemblyName,
                    ["Sources"] = new JArray(sources),
                    ["References"] = new JArray(refs),
                    ["Output"] = output,
                };
                EnsureStarted();
                input.WriteLine(request.ToString(Newtonsoft.Json.Formatting.None));
                string line = process.StandardOutput.ReadLine();
                if (line == null)
                    return null;
                var reply = JObject.Parse(line);
                return (bool)reply["Ok"] && File.Exists(output) ? File.ReadAllBytes(output) : null;
            }
            catch
            {
                Stop();
                return null;
            }
        }

        private void EnsureStarted()
        {
            if (process != null && !process.HasExited)
                return;
            var utf8 = new UTF8Encoding(false);
            process = Process.Start(new ProcessStartInfo(compilerExe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                WorkingDirectory = Path.GetDirectoryName(compilerExe),
                StandardOutputEncoding = utf8,
            });
            input = new StreamWriter(process.StandardInput.BaseStream, utf8) { AutoFlush = true };
        }

        private void Stop()
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    input.Close();
                    if (!process.WaitForExit(2000))
                        process.Kill();
                }
            }
            catch
            {
            }
            process = null;
            input = null;
        }

        public void Dispose() => Stop();
    }
}
