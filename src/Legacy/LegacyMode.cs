using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DeSwitcher.Core;
using DeSwitcher.Runtime;
using HarmonyLib;

namespace DeSwitcher.Legacy
{
    public static class Entry
    {
        private static MethodInfo tryCompile;
        private static Type resultType;
        private static Type scriptType;
        private static Type tupleType;

        public static void Apply(Harmony harmony, Assembly game)
        {
            var loader = AccessTools.TypeByName("ModLoader");
            tryCompile = AccessTools.Method(loader, "TryCompileMod");
            resultType = AccessTools.TypeByName("ModCompilationResult");
            scriptType = AccessTools.TypeByName("ModScript");
            tupleType = typeof(ValueTuple<,>).MakeGenericType(resultType, scriptType);

            harmony.Patch(AccessTools.Method(loader, "Compile"),
                prefix: new HarmonyMethod(typeof(Entry), nameof(CompilePrefix)));

            var loadModAt = AccessTools.Method(loader, "LoadModAt");
            if (loadModAt != null)
                harmony.Patch(loadModAt, postfix: new HarmonyMethod(typeof(Entry), nameof(AfterLoadModAt)));

            var manager = AccessTools.TypeByName("ModificationManager");
            var invoke = AccessTools.Method(manager, "Invoke", new[] { typeof(string), AccessTools.TypeByName("ModMetaData") });
            if (invoke != null)
                harmony.Patch(invoke, prefix: new HarmonyMethod(typeof(Entry), nameof(InvokePrefix)));

            var entryUi = AccessTools.Method(AccessTools.TypeByName("ModEntryBehaviour"), "UpdateUi");
            if (entryUi != null)
                harmony.Patch(entryUi, postfix: new HarmonyMethod(typeof(Entry), nameof(AfterModEntryUi)));

            var versionStart = AccessTools.Method(AccessTools.TypeByName("VersionDisplay"), "Start");
            if (versionStart != null)
                harmony.Patch(versionStart, postfix: new HarmonyMethod(typeof(Entry), nameof(AfterVersionStart)));

            Log.Info("Legacy mode active");
        }

        private static bool CompilePrefix(object mod, ref object __result)
        {
            var meta = Traverse.Create(mod);
            string folder = meta.Field("MetaLocation").GetValue() as string;
            string name = meta.Field("Name").GetValue() as string;
            string workshopId = meta.Field("UGCIdentity").GetValue() as string;
            string hash = Safe(() => ModHash.Compute(folder));
            string cacheDll = Path.Combine(Bootstrap.CacheDir, hash + ".dll");

            object resultObj = null;
            object script = null;
            byte[] data = null;

            if (File.Exists(cacheDll))
            {
                try
                {
                    data = File.ReadAllBytes(cacheDll);
                    script = MakeScript(mod, folder, data);
                    resultObj = MakeResult(true, null, false);
                    Log.Info("Cached " + name + ", skipped recompile");
                }
                catch
                {
                    data = null;
                }
            }

            if (data == null)
            {
                try
                {
                    for (int attempt = 0; ; attempt++)
                    {
                        var args = new object[] { mod, null };
                        resultObj = tryCompile.Invoke(null, args);
                        script = args[1];
                        if (attempt >= 20 || !CompilerNotReady(resultObj))
                            break;
                        System.Threading.Thread.Sleep(500);
                    }
                }
                catch (Exception e)
                {
                    Log.Error("TryCompileMod failed: " + (e.InnerException ?? e));
                    __result = MakeTuple(MakeResult(false, "deSwitcher: compile failed", false), null);
                    return false;
                }
                if ((bool)Traverse.Create(resultObj).Field("Success").GetValue() && script != null)
                {
                    data = Traverse.Create(script).Field("AssemblyData").GetValue() as byte[];
                    if (data != null)
                    {
                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(cacheDll));
                            File.WriteAllBytes(cacheDll, data);
                        }
                        catch
                        {
                        }
                    }
                }
            }

            if (data != null)
            {
                var findings = CodeScanner.Scan(data).ToList();
                if (!Guard.Lists.Catalog.Mods.ContainsKey(hash) && !Guard.State.TrustedHashes.Contains(hash))
                {
                    try
                    {
                        findings.AddRange(CodeScanner.ScanBundled(folder));
                    }
                    catch (Exception e)
                    {
                        findings.Add(new Finding("unreadable", Severity.Dangerous, "bundled dll scan failed: " + e.Message));
                    }
                }
                var verdict = Verdict.Evaluate(hash, workshopId, name, findings, Guard.Lists, Guard.State);
                if (!verdict.MayLoad)
                {
                    DangerQueue.Add(folder, name, verdict);
                    Log.Warn("Blocked " + name + " (" + verdict.Status + ")");
                    __result = MakeTuple(MakeResult(false, "Blocked by deSwitcher (" + verdict.Status + ")", false), null);
                    return false;
                }
            }

            __result = MakeTuple(resultObj, script);
            return false;
        }

        private static bool CompilerNotReady(object result)
        {
            var r = Traverse.Create(result);
            if ((bool)r.Field("Success").GetValue())
                return false;
            string errors = r.Field("Errors").GetValue() as string ?? "";
            return errors.IndexOf("actively refused", StringComparison.OrdinalIgnoreCase) >= 0
                || errors.IndexOf("No connection could be made", StringComparison.OrdinalIgnoreCase) >= 0
                || errors.IndexOf("TimeoutException", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static object MakeScript(object mod, string folder, byte[] data)
        {
            var meta = Traverse.Create(mod);
            string[] scripts = meta.Field("Scripts").GetValue() as string[] ?? new string[0];
            var script = Activator.CreateInstance(scriptType);
            var t = Traverse.Create(script);
            t.Field("AssemblyData").SetValue(data);
            t.Field("RelativePath").SetValue(folder);
            t.Field("AbsoluteScriptPaths").SetValue(scripts.Select(s => Path.GetFullPath(Path.Combine(folder, s))).ToArray());
            return script;
        }

        private static void AfterLoadModAt(object __result)
        {
            if (__result == null)
                return;
            var meta = Traverse.Create(__result);
            string errors = meta.Field("Errors").GetValue() as string;
            if (errors != null && errors.Contains("Safe mode is enabled"))
            {
                meta.Field("Errors").SetValue(null);
                meta.Field("HasErrors").SetValue(false);
            }
        }

        private static bool InvokePrefix(string methodName, object metaData, ref bool __result)
        {
            var meta = Traverse.Create(metaData);
            if (!(bool)meta.Field("Active").GetValue())
            {
                __result = false;
                return false;
            }
            var loader = AccessTools.TypeByName("ModLoader");
            var assembly = AccessTools.Method(loader, "GetModAssembly").Invoke(null, new[] { metaData }) as Assembly;
            string entry = meta.Field("EntryPoint").GetValue() as string;
            __result = ModInvoker.Invoke(assembly, metaData, entry, methodName);
            return false;
        }

        private static void AfterModEntryUi(object __instance)
        {
            var t = Traverse.Create(__instance);
            object meta = t.Field("ModMeta").GetValue();
            if (meta == null)
                return;
            var m = Traverse.Create(meta);
            bool hasErr = (bool)m.Field("HasErrors").GetValue();
            bool susp = (bool)m.Property("Suspicious").GetValue();
            string text = hasErr ? m.Field("Errors").GetValue() as string : m.Field("Description").GetValue() as string;
            var desc = Traverse.Create(t.Field("Description").GetValue());
            desc.Property("text").SetValue(text ?? "");
            desc.Property("color").SetValue(hasErr ? (susp ? UnityEngine.Color.yellow : new UnityEngine.Color(1f, 0.24f, 0.35f)) : UnityEngine.Color.white);
        }

        private static void AfterVersionStart(object __instance)
        {
            var txt = Traverse.Create(__instance).Field("Text").GetValue();
            if (txt == null)
                return;
            var tt = Traverse.Create(txt);
            string s = tt.Property("text").GetValue() as string;
            if (s == null)
                return;
            s = s.Replace(" <color=green>[SAFE MODE]</color>", "").Replace("[SAFE MODE]", "");
            if (!s.Contains("deswitchstudio.com"))
                s += "\n<color=#3DE0FF>deswitchstudio.com</color>";
            tt.Property("text").SetValue(s);
        }

        private static object MakeResult(bool success, string errors, bool suspicious) =>
            Activator.CreateInstance(resultType, success, errors, suspicious);

        private static object MakeTuple(object result, object script) =>
            Activator.CreateInstance(tupleType, result, script);

        private static string Safe(Func<string> f)
        {
            try { return f(); } catch { return Guid.NewGuid().ToString("N"); }
        }
    }
}
