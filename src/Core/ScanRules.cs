using System;
using System.Collections.Generic;
using System.Linq;

namespace DeSwitcher.Core
{
    internal sealed class MemberRule
    {
        public readonly string Type;
        public readonly bool IsPrefix;
        public readonly string[] Members;
        public readonly string RuleId;
        public readonly Severity Severity;

        public MemberRule(string type, string rule, Severity severity, params string[] members)
        {
            IsPrefix = type.EndsWith(".", StringComparison.Ordinal);
            Type = type;
            RuleId = rule;
            Severity = severity;
            Members = members;
        }

        public bool Matches(string typeName, string member)
        {
            bool typeOk = IsPrefix
                ? typeName.StartsWith(Type, StringComparison.Ordinal)
                : typeName == Type || typeName.StartsWith(Type + "/", StringComparison.Ordinal);
            return typeOk && (Members.Length == 0 || (member != null && Members.Contains(member)));
        }
    }

    internal static class ScanRules
    {
        public static readonly MemberRule[] Members =
        {
            new MemberRule("System.Net.", "net", Severity.Dangerous),
            new MemberRule("UnityEngine.Networking.", "net", Severity.Dangerous),
            new MemberRule("UnityEngine.WWW", "net", Severity.Dangerous),
            new MemberRule("UnityEngine.WWWForm", "net", Severity.Dangerous),

            new MemberRule("System.Diagnostics.Process", "process", Severity.Dangerous),
            new MemberRule("System.Diagnostics.ProcessStartInfo", "process", Severity.Dangerous),

            new MemberRule("Microsoft.Win32.Registry", "registry", Severity.Dangerous),
            new MemberRule("Microsoft.Win32.RegistryKey", "registry", Severity.Dangerous),

            new MemberRule("System.Reflection.Assembly", "asm-load", Severity.Dangerous, "Load", "LoadFrom", "LoadFile", "UnsafeLoadFrom", "LoadWithPartialName", "LoadModule", "ReflectionOnlyLoad", "ReflectionOnlyLoadFrom"),
            new MemberRule("System.AppDomain", "asm-load", Severity.Dangerous, "Load", "ExecuteAssembly", "ExecuteAssemblyByName", "CreateDomain", "CreateInstance", "CreateInstanceAndUnwrap", "CreateInstanceFrom", "CreateInstanceFromAndUnwrap"),

            new MemberRule("System.Runtime.Serialization.Formatters.Binary.BinaryFormatter", "serialization", Severity.Dangerous),
            new MemberRule("System.Runtime.Serialization.Formatters.Soap.SoapFormatter", "serialization", Severity.Dangerous),
            new MemberRule("System.Runtime.Serialization.NetDataContractSerializer", "serialization", Severity.Dangerous),

            new MemberRule("System.Reflection.Emit.", "codegen", Severity.Dangerous),
            new MemberRule("System.CodeDom.Compiler.", "codegen", Severity.Dangerous),
            new MemberRule("Microsoft.CSharp.CSharpCodeProvider", "codegen", Severity.Dangerous),
            new MemberRule("System.Linq.Expressions.LambdaExpression", "codegen", Severity.Suspicious, "Compile"),
            new MemberRule("System.Linq.Expressions.Expression`1", "codegen", Severity.Suspicious, "Compile"),

            new MemberRule("System.Environment", "env", Severity.Dangerous, "GetEnvironmentVariable", "GetEnvironmentVariables", "ExpandEnvironmentVariables", "SetEnvironmentVariable", "GetFolderPath"),
            new MemberRule("System.Environment", "env", Severity.Suspicious, "get_UserName", "get_MachineName", "get_UserDomainName"),

            new MemberRule("System.Runtime.InteropServices.Marshal", "pinvoke", Severity.Dangerous, "GetDelegateForFunctionPointer", "GetFunctionPointerForDelegate"),
            new MemberRule("System.Runtime.InteropServices.NativeLibrary", "pinvoke", Severity.Dangerous),
            new MemberRule("System.Runtime.InteropServices.Marshal", "unsafe", Severity.Suspicious, "Copy", "AllocHGlobal", "ReadIntPtr", "WriteIntPtr", "ReadByte", "WriteByte", "PtrToStructure", "StructureToPtr"),

            new MemberRule("DeSwitcher.", "tamper", Severity.Dangerous),
            new MemberRule("HarmonyLib.", "tamper", Severity.Dangerous),
            new MemberRule("Harmony.", "tamper", Severity.Dangerous),
            new MemberRule("MonoMod.", "tamper", Severity.Dangerous),
            new MemberRule("Mono.Cecil.", "tamper", Severity.Dangerous),
        };

        public static readonly string[] FileApiTypes =
        {
            "System.IO.File", "System.IO.Directory", "System.IO.FileStream", "System.IO.FileInfo", "System.IO.DirectoryInfo",
            "System.IO.StreamWriter", "System.IO.StreamReader", "System.IO.FileSystemInfo"
        };

        public static readonly HashSet<string> FileWriteMembers = new HashSet<string>(StringComparer.Ordinal)
        {
            "WriteAllBytes", "WriteAllText", "WriteAllLines", "AppendAllText", "AppendAllLines",
            "Create", "CreateText", "AppendText", "CreateDirectory", "CreateSubdirectory",
            "Delete", "Move", "MoveTo", "Copy", "CopyTo", "Replace", "OpenWrite",
            "Encrypt", "Decrypt", "SetAttributes", "SetAccessControl", "SetCreationTime", "SetLastWriteTime"
        };

        public static readonly HashSet<string> SystemLocationSources = new HashSet<string>(StringComparer.Ordinal)
        {
            "System.Environment::GetFolderPath", "System.Environment::GetEnvironmentVariable", "System.Environment::ExpandEnvironmentVariables",
            "System.Environment::get_SystemDirectory",
            "System.IO.Path::GetTempPath", "System.IO.Path::GetTempFileName",
            "System.IO.DriveInfo::GetDrives", "System.IO.Directory::GetLogicalDrives"
        };

        public static bool IsSensitiveWriteTarget(string literal)
        {
            if (LooksLikeLaunchTarget(literal))
                return true;
            string l = literal.ToLowerInvariant().Replace('\\', '/');
            return l.Contains("/managed/") || l.EndsWith("/managed", StringComparison.Ordinal) || l.StartsWith("managed/", StringComparison.Ordinal)
                || l.EndsWith(".dll", StringComparison.Ordinal) || l.EndsWith(".config", StringComparison.Ordinal)
                || l.Contains("deswitcher");
        }

        public static readonly HashSet<string> TypeEnumMethods = new HashSet<string>(StringComparer.Ordinal)
        {
            "System.Reflection.Assembly::GetTypes", "System.Reflection.Assembly::GetExportedTypes", "System.Reflection.Assembly::get_DefinedTypes",
            "System.Reflection.Module::GetTypes", "System.Type::GetNestedTypes", "System.AppDomain::GetAssemblies"
        };

        public static readonly HashSet<string> ReflectInvokeMethods = new HashSet<string>(StringComparer.Ordinal)
        {
            "System.Reflection.MethodBase::Invoke", "System.Reflection.MethodInfo::Invoke", "System.Reflection.ConstructorInfo::Invoke",
            "System.Type::InvokeMember", "System.Delegate::CreateDelegate", "System.Reflection.MethodInfo::CreateDelegate",
            "System.Reflection.FieldInfo::SetValue", "System.Reflection.PropertyInfo::SetValue"
        };

        public static readonly HashSet<string> TypeByNameMethods = new HashSet<string>(StringComparer.Ordinal)
        {
            "System.Type::GetType", "System.Reflection.Assembly::GetType", "System.Activator::CreateInstance",
            "System.Type::InvokeMember", "System.Reflection.Module::GetType"
        };

        public static readonly string[] DangerousTypeNames =
        {
            "System.Diagnostics.Process", "System.Net.", "System.Net,", "Microsoft.Win32.Registry", "System.Reflection.Emit",
            "System.Runtime.InteropServices.Marshal", "System.Runtime.Serialization.Formatters", "System.CodeDom",
            "UnityEngine.Networking", "UnityEngine.WWW", "System.AppDomain", "System.Environment",
            "DeSwitcher", "HarmonyLib", "0Harmony", "MonoMod", "Mono.Cecil"
        };

        private static readonly string[] ExecExtensions = { ".exe", ".bat", ".cmd", ".ps1", ".vbs", ".scr", ".com", ".msi", ".hta", ".jar" };

        public static bool IsAbsolutePathLiteral(string s)
        {
            if (s.Length < 3)
                return false;
            if (char.IsLetter(s[0]) && s[1] == ':' && (s[2] == '\\' || s[2] == '/'))
                return true;
            return s.StartsWith("\\\\", StringComparison.Ordinal)
                || (s.StartsWith("%", StringComparison.Ordinal) && s.IndexOf('%', 1) > 1);
        }

        public static bool LooksLikeLaunchTarget(string s)
        {
            if (IsAbsolutePathLiteral(s))
                return true;
            string l = s.ToLowerInvariant();
            if (l.StartsWith("file:", StringComparison.Ordinal))
                return true;
            return ExecExtensions.Any(e => l.EndsWith(e, StringComparison.Ordinal));
        }

        public static bool IsWebUrl(string s)
        {
            string l = s.TrimStart().ToLowerInvariant();
            return l.StartsWith("http://", StringComparison.Ordinal) || l.StartsWith("https://", StringComparison.Ordinal);
        }

        public static bool NamesDangerousType(string s) =>
            DangerousTypeNames.Any(t => s.IndexOf(t, StringComparison.Ordinal) >= 0);
    }
}
