using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Mono.Cecil;

namespace DeSwitcher.Patcher.Logic
{
    public static class GameLocator
    {
        public const string Relative = "People Playground_Data";

        public static string Find()
        {
            var libraries = new List<string>();
            string steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
            if (steam != null)
            {
                libraries.Add(steam);
                string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdf))
                    foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                        libraries.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
            }
            return libraries
                .Select(l => Path.Combine(l, "steamapps", "common", "People Playground"))
                .FirstOrDefault(IsGame);
        }

        public static bool IsGame(string dir) =>
            !string.IsNullOrEmpty(dir) && File.Exists(ManagedDll(dir));

        public static string ManagedDir(string game) =>
            Path.Combine(game, Relative, "Managed");

        public static string ManagedDll(string game) =>
            Path.Combine(ManagedDir(game), "Assembly-CSharp.dll");

        public static string Version(string game)
        {
            try
            {
                using (var module = ModuleDefinition.ReadModule(ManagedDll(game), new ReaderParameters { ReadSymbols = false }))
                {
                    var versionField = module.GetType("GameVersion")?.Fields.FirstOrDefault(f => f.Name == "Version" && f.HasConstant);
                    if (versionField?.Constant is string v && v.Length > 0)
                        return v;

                    var cctor = module.GetType("GameInfo")?.Methods.FirstOrDefault(m => m.IsConstructor && m.IsStatic);
                    var ctor = cctor?.Body.Instructions.FirstOrDefault(i =>
                        i.OpCode.Code == Mono.Cecil.Cil.Code.Newobj &&
                        ((MethodReference)i.Operand).DeclaringType.FullName == "System.Version");
                    if (ctor == null)
                        return "?";
                    var parts = new List<int>();
                    for (var i = ctor.Previous; i != null && IsLdcI4(i.OpCode.Code); i = i.Previous)
                        parts.Insert(0, LdcValue(i));
                    return parts.Count > 0 ? string.Join(".", parts) : "?";
                }
            }
            catch
            {
                return "?";
            }
        }

        private static bool IsLdcI4(Mono.Cecil.Cil.Code code) =>
            code >= Mono.Cecil.Cil.Code.Ldc_I4_M1 && code <= Mono.Cecil.Cil.Code.Ldc_I4;

        private static int LdcValue(Mono.Cecil.Cil.Instruction i)
        {
            switch (i.OpCode.Code)
            {
                case Mono.Cecil.Cil.Code.Ldc_I4: return (int)i.Operand;
                case Mono.Cecil.Cil.Code.Ldc_I4_S: return (sbyte)i.Operand;
                default: return i.OpCode.Code - Mono.Cecil.Cil.Code.Ldc_I4_0;
            }
        }
    }
}
