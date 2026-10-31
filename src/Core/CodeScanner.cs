using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DeSwitcher.Core
{
    public static class CodeScanner
    {
        public static IReadOnlyList<Finding> ScanBundled(string modFolder)
        {
            var findings = new List<Finding>();
            string root = Path.GetFullPath(modFolder);
            foreach (string dll in SafeDir.EnumerateFiles(root, "*.dll"))
            {
                string rel = dll.Substring(root.Length).TrimStart('\\', '/');
                if (ModHash.IsExcluded(rel))
                    continue;
                byte[] bytes;
                try
                {
                    bytes = File.ReadAllBytes(dll);
                }
                catch (Exception e)
                {
                    findings.Add(new Finding("unreadable", Severity.Dangerous, rel + ": " + e.GetType().Name));
                    continue;
                }
                foreach (var f in Scan(bytes))
                    findings.Add(new Finding(f.RuleId, f.Severity, rel + ": " + f.Detail));
            }
            return findings;
        }

        public static IReadOnlyList<Finding> Scan(byte[] assembly)
        {
            var findings = new List<Finding>();
            ModuleDefinition module;
            try
            {
                module = ModuleDefinition.ReadModule(new MemoryStream(assembly), new ReaderParameters { ReadSymbols = false, InMemory = true });
            }
            catch (Exception e)
            {
                findings.Add(new Finding("unreadable", Severity.Dangerous, e.GetType().Name));
                return findings;
            }

            using (module)
            {
                foreach (var native in module.ModuleReferences)
                    findings.Add(new Finding("pinvoke", Severity.Dangerous, native.Name));

                foreach (var type in module.GetTypes())
                foreach (var method in type.Methods)
                    ScanMethod(method, findings);

                ScanNames(module, findings);
            }

            return findings
                .GroupBy(f => f.RuleId + "|" + f.Severity + "|" + Where(f.Detail))
                .Select(g => g.Count() == 1 ? g.First() : new Finding(g.First().RuleId, g.First().Severity, g.First().Detail + " (+" + (g.Count() - 1) + ")"))
                .OrderByDescending(f => f.Severity)
                .ThenBy(f => f.RuleId, StringComparer.Ordinal)
                .ThenBy(f => f.Detail, StringComparer.Ordinal)
                .ToList();
        }

        private static void ScanMethod(MethodDefinition method, List<Finding> findings)
        {
            string where = method.DeclaringType.FullName + "." + method.Name;

            if (method.IsPInvokeImpl || method.HasPInvokeInfo)
                findings.Add(new Finding("pinvoke", Severity.Dangerous, (method.PInvokeInfo?.Module?.Name ?? "native") + " @ " + where));

            if (!method.HasBody)
                return;

            bool usesFiles = false;
            bool usesSystemLocation = false;
            bool hasAbsolutePath = false;
            bool usesTypeEnum = false;
            bool usesReflectInvoke = false;
            bool usesOpenUrl = false;
            bool hasWrite = false;
            bool sensitiveWriteTarget = false;
            var literals = new List<string>();
            var byName = new List<Instruction>();

            foreach (var ins in method.Body.Instructions)
            {
                if (ins.OpCode == OpCodes.Ldstr && ins.Operand is string literal)
                {
                    literals.Add(literal);
                    if (ScanRules.IsAbsolutePathLiteral(literal))
                        hasAbsolutePath = true;
                    if (ScanRules.IsSensitiveWriteTarget(literal))
                        sensitiveWriteTarget = true;
                    continue;
                }

                string typeName;
                string member = null;
                if (ins.Operand is MemberReference mr && mr.DeclaringType != null && !(ins.Operand is TypeReference))
                {
                    typeName = mr.DeclaringType.GetElementType().FullName;
                    member = mr.Name;
                }
                else if (ins.Operand is TypeReference tr)
                {
                    typeName = tr.GetElementType().FullName;
                }
                else
                {
                    continue;
                }

                string key = member == null ? typeName : typeName + "::" + member;

                foreach (var rule in ScanRules.Members)
                    if ((member != null || rule.Members.Length == 0) && rule.Matches(typeName, member))
                        findings.Add(new Finding(rule.RuleId, rule.Severity, key + " @ " + where));

                if (ScanRules.FileApiTypes.Contains(typeName))
                {
                    usesFiles = true;
                    if (member != null && ScanRules.FileWriteMembers.Contains(member))
                        hasWrite = true;
                }
                if (ScanRules.SystemLocationSources.Contains(key))
                    usesSystemLocation = true;
                if (ScanRules.TypeEnumMethods.Contains(key))
                    usesTypeEnum = true;
                if (ScanRules.ReflectInvokeMethods.Contains(key))
                    usesReflectInvoke = true;
                if (key == "UnityEngine.Application::OpenURL")
                    usesOpenUrl = true;
                if (member != null && ScanRules.TypeByNameMethods.Contains(key))
                    byName.Add(ins);
            }

            if (usesFiles && (usesSystemLocation || hasAbsolutePath))
                findings.Add(new Finding("fs-outside", Severity.Dangerous, where));
            else if (hasWrite && sensitiveWriteTarget)
                findings.Add(new Finding("fs-write", Severity.Dangerous, where));

            if (usesTypeEnum && usesReflectInvoke)
                findings.Add(new Finding("reflection-dangerous", Severity.Dangerous, "type lookup + invoke @ " + where));

            if (usesOpenUrl)
            {
                bool safe = literals.Count > 0 && literals.All(l => ScanRules.IsWebUrl(l) || !ScanRules.LooksLikeLaunchTarget(l));
                findings.Add(new Finding("net", safe ? Severity.Suspicious : Severity.Dangerous, "OpenURL @ " + where));
            }

            if (byName.Count > 0)
            {
                string named = literals.FirstOrDefault(ScanRules.NamesDangerousType);
                if (named != null)
                    findings.Add(new Finding("reflection-dangerous", Severity.Dangerous, named + " @ " + where));
                else if (byName.Any(i => !HasLiteralArgument(i)))
                    findings.Add(new Finding("reflection-dangerous", Severity.Dangerous, "computed type name @ " + where));
            }
        }

        private static string Where(string detail)
        {
            int at = detail.LastIndexOf(" @ ", StringComparison.Ordinal);
            return at < 0 ? detail : detail.Substring(at + 3);
        }

        private static bool HasLiteralArgument(Instruction call)
        {
            var prev = call.Previous;
            for (int i = 0; i < 3 && prev != null; i++, prev = prev.Previous)
            {
                if (prev.OpCode == OpCodes.Ldstr)
                    return true;
                if (prev.OpCode.FlowControl == FlowControl.Call)
                    return false;
            }
            return false;
        }

        private static void ScanNames(ModuleDefinition module, List<Finding> findings)
        {
            int odd = 0;
            foreach (var type in module.GetTypes())
            {
                if (IsOddName(type.Name))
                    odd++;
                foreach (var method in type.Methods)
                    if (IsOddName(method.Name))
                        odd++;
                foreach (var field in type.Fields)
                    if (IsOddName(field.Name))
                        odd++;
            }
            if (odd >= 3)
                findings.Add(new Finding("obfuscation", Severity.Suspicious, odd + " unreadable names"));
        }

        private static bool IsOddName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return true;
            foreach (char c in name)
            {
                switch (char.GetUnicodeCategory(c))
                {
                    case System.Globalization.UnicodeCategory.Control:
                    case System.Globalization.UnicodeCategory.Format:
                    case System.Globalization.UnicodeCategory.PrivateUse:
                    case System.Globalization.UnicodeCategory.Surrogate:
                    case System.Globalization.UnicodeCategory.OtherNotAssigned:
                    case System.Globalization.UnicodeCategory.SpaceSeparator:
                    case System.Globalization.UnicodeCategory.LineSeparator:
                    case System.Globalization.UnicodeCategory.ParagraphSeparator:
                    case System.Globalization.UnicodeCategory.NonSpacingMark:
                    case System.Globalization.UnicodeCategory.EnclosingMark:
                        return true;
                }
            }
            return false;
        }
    }
}
