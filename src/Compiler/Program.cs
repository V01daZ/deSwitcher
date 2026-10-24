using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Newtonsoft.Json;

namespace DeSwitcher.Compiler
{
    public sealed class CompileRequest
    {
        public int Id;
        public string AssemblyName;
        public string[] Sources;
        public string[] References;
        public string Output;
        public Dictionary<string, string> Rewrites;
    }

    public sealed class CompileReply
    {
        public int Id;
        public bool Ok;
        public List<string> Errors = new List<string>();
    }

    public static class Program
    {
        private static readonly Dictionary<string, MetadataReference> ReferenceCache = new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);

        public static int Main()
        {
            var utf8 = new UTF8Encoding(false);
            var input = new StreamReader(Console.OpenStandardInput(), utf8);
            var output = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true };
            string line;
            while ((line = input.ReadLine()) != null)
            {
                if (line.Length == 0)
                    continue;
                CompileReply reply;
                try
                {
                    reply = Compile(JsonConvert.DeserializeObject<CompileRequest>(line));
                }
                catch (Exception e)
                {
                    reply = new CompileReply { Ok = false, Errors = { e.GetType().Name + ": " + e.Message } };
                }
                output.WriteLine(JsonConvert.SerializeObject(reply, Formatting.None));
            }
            return 0;
        }

        public static CompileReply Compile(CompileRequest request)
        {
            var reply = new CompileReply { Id = request.Id };
            if (request.Sources == null || request.Sources.Length == 0)
            {
                reply.Errors.Add("mod.json has no Scripts");
                return reply;
            }

            var parse = new CSharpParseOptions(LanguageVersion.Latest);
            var trees = new List<SyntaxTree>();
            foreach (string source in request.Sources)
            {
                if (!File.Exists(source))
                {
                    reply.Errors.Add("Missing script: " + Path.GetFileName(source));
                    continue;
                }
                var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(source), parse, source, Encoding.UTF8);
                if (request.Rewrites != null && request.Rewrites.Count > 0)
                    tree = tree.WithRootAndOptions(new CompatRewriter(request.Rewrites).Visit(tree.GetRoot()), parse);
                trees.Add(tree);
            }
            if (reply.Errors.Count > 0)
                return reply;

            var references = (request.References ?? new string[0]).Where(File.Exists).Select(Reference).ToList();
            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                allowUnsafe: false,
                platform: Platform.AnyCpu,
                concurrentBuild: true);
            var compilation = CSharpCompilation.Create(SafeName(request.AssemblyName), trees, references, options);

            using (var ms = new MemoryStream())
            {
                var result = compilation.Emit(ms);
                if (!result.Success)
                {
                    reply.Errors.AddRange(result.Diagnostics
                        .Where(d => d.Severity == DiagnosticSeverity.Error)
                        .Take(50)
                        .Select(Format));
                    return reply;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(request.Output)));
                string tmp = request.Output + ".tmp";
                File.WriteAllBytes(tmp, ms.ToArray());
                if (File.Exists(request.Output))
                    File.Delete(request.Output);
                File.Move(tmp, request.Output);
            }
            reply.Ok = true;
            return reply;
        }

        private static MetadataReference Reference(string path)
        {
            lock (ReferenceCache)
            {
                if (!ReferenceCache.TryGetValue(path, out var r))
                {
                    r = MetadataReference.CreateFromFile(path);
                    ReferenceCache[path] = r;
                }
                return r;
            }
        }

        private static string Format(Diagnostic d)
        {
            var span = d.Location.GetLineSpan();
            string file = string.IsNullOrEmpty(span.Path) ? "" : Path.GetFileName(span.Path);
            return string.IsNullOrEmpty(file)
                ? d.Id + ": " + d.GetMessage()
                : file + "(" + (span.StartLinePosition.Line + 1) + "," + (span.StartLinePosition.Character + 1) + "): " + d.Id + ": " + d.GetMessage();
        }

        private static string SafeName(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name ?? "Mod")
                sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '-' ? c : '_');
            return sb.Length == 0 ? "Mod" : sb.ToString();
        }
    }
}
