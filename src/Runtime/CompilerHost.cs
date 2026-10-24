using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DeSwitcher.Runtime
{
    public sealed class CompileOutcome
    {
        public bool Ok;
        public List<string> Errors = new List<string>();
    }

    public static class CompilerHost
    {
        private static readonly object Gate = new object();
        private static Process process;
        private static StreamWriter input;
        private static int nextId;

        public static string ExePath => Path.Combine(Bootstrap.HomeDir, "Compiler", "deSwitcher.Compiler.exe");

        public static CompileOutcome Compile(string assemblyName, string[] sources, string[] references, string output, Dictionary<string, string> rewrites)
        {
            lock (Gate)
            {
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    try
                    {
                        EnsureStarted();
                        var request = new JObject
                        {
                            ["Id"] = ++nextId,
                            ["AssemblyName"] = assemblyName,
                            ["Sources"] = new JArray(sources),
                            ["References"] = new JArray(references),
                            ["Output"] = output,
                            ["Rewrites"] = rewrites == null ? null : JObject.FromObject(rewrites),
                        };
                        input.WriteLine(request.ToString(Formatting.None));
                        string line = process.StandardOutput.ReadLine();
                        if (line == null)
                            throw new IOException("compiler exited");
                        var reply = JObject.Parse(line);
                        var outcome = new CompileOutcome { Ok = (bool)reply["Ok"] };
                        if (reply["Errors"] is JArray errors)
                            foreach (var e in errors)
                                outcome.Errors.Add((string)e);
                        return outcome;
                    }
                    catch (Exception e)
                    {
                        Log.Warn("Compiler call failed: " + e.Message);
                        Stop();
                        if (attempt == 1)
                            return new CompileOutcome { Errors = { "deSwitcher compiler is not available: " + e.Message } };
                    }
                }
                return new CompileOutcome();
            }
        }

        public static void Stop()
        {
            lock (Gate)
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
        }

        private static void EnsureStarted()
        {
            if (process != null && !process.HasExited)
                return;
            if (!File.Exists(ExePath))
                throw new FileNotFoundException("missing " + ExePath);
            var utf8 = new UTF8Encoding(false);
            var info = new ProcessStartInfo(ExePath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                WorkingDirectory = Path.GetDirectoryName(ExePath),
                StandardOutputEncoding = utf8,
            };
            process = Process.Start(info);
            input = new StreamWriter(process.StandardInput.BaseStream, utf8) { AutoFlush = true };
        }
    }
}
