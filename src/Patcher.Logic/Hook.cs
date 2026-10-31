using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DeSwitcher.Patcher.Logic
{
    public static class Hook
    {
        private const string BootType = "DeSwitcher.Boot.Entry";
        private const string BootMethod = "Init";

        private static readonly (string Type, string Method)[] Points =
        {
            ("BackgroundItemLoader", "Awake"),
            ("MenuController", "Awake"),
            ("VersionDisplay", "Start"),
        };

        public static bool IsApplied(string managedDir)
        {
            string dll = Path.Combine(managedDir, "Assembly-CSharp.dll");
            if (!File.Exists(dll))
                return false;
            using (var asm = AssemblyDefinition.ReadAssembly(dll))
                return asm.MainModule.AssemblyReferences.Any(r => r.Name == "deSwitcher.Boot");
        }

        public static void Apply(string managedDir, byte[] bootDll)
        {
            string dll = Path.Combine(managedDir, "Assembly-CSharp.dll");
            string backup = dll + ".deswitcher.bak";
            string bootPath = Path.Combine(managedDir, "deSwitcher.Boot.dll");

            byte[] source = IsApplied(managedDir) && File.Exists(backup)
                ? File.ReadAllBytes(backup)
                : File.ReadAllBytes(dll);

            byte[] patched = Inject(source, bootDll, managedDir);

            if (!IsApplied(managedDir))
                File.WriteAllBytes(backup, source);
            File.WriteAllBytes(bootPath, bootDll);
            File.WriteAllBytes(dll, patched);
        }

        public static void Remove(string managedDir)
        {
            string dll = Path.Combine(managedDir, "Assembly-CSharp.dll");
            string backup = dll + ".deswitcher.bak";
            if (IsApplied(managedDir) && File.Exists(backup))
                File.Copy(backup, dll, true);
            string bootPath = Path.Combine(managedDir, "deSwitcher.Boot.dll");
            if (File.Exists(bootPath))
                File.Delete(bootPath);
        }

        private static byte[] Inject(byte[] gameDll, byte[] bootDll, string managedDir)
        {
            using (var resolver = new DefaultAssemblyResolver())
            {
                resolver.AddSearchDirectory(managedDir);
                var readerParams = new ReaderParameters { AssemblyResolver = resolver };

                using (var game = AssemblyDefinition.ReadAssembly(new MemoryStream(gameDll), readerParams))
                using (var boot = AssemblyDefinition.ReadAssembly(new MemoryStream(bootDll), readerParams))
                {
                    var module = game.MainModule;
                    var init = boot.MainModule.GetType(BootType).Methods.Single(m => m.Name == BootMethod);
                    var initRef = module.ImportReference(init);

                    int injected = 0;
                    foreach (var (typeName, methodName) in Points)
                    {
                        var method = module.GetType(typeName)?.Methods.FirstOrDefault(m => m.Name == methodName && m.HasBody);
                        if (method == null)
                            continue;
                        var il = method.Body.GetILProcessor();
                        var first = method.Body.Instructions.First();
                        il.InsertBefore(first, il.Create(OpCodes.Call, initRef));
                        injected++;
                    }
                    if (injected == 0)
                        throw new Exception("No known entry point found in Assembly-CSharp. The game version is not supported.");
                    AddLegacyMembers(module);
                    using (var outMs = new MemoryStream())
                    {
                        game.Write(outMs);
                        return outMs.ToArray();
                    }
                }
            }
        }

        public static string OriginalGameDll(string managedDir)
        {
            string dll = Path.Combine(managedDir, "Assembly-CSharp.dll");
            string backup = dll + ".deswitcher.bak";
            return IsApplied(managedDir) && File.Exists(backup) ? backup : dll;
        }

        private static void AddLegacyMembers(ModuleDefinition module)
        {
            var spawnableModification = module.GetType("SpawnableModification");
            var asset = module.GetType("SpawnableAsset");
            var modApi = module.GetType("ModAPI");
            var catalog = module.GetType("CatalogBehaviour");
            var gameObject = module.GetTypeReferences().FirstOrDefault(t => t.FullName == "UnityEngine.GameObject");
            if (spawnableModification == null || asset == null || modApi == null || catalog == null || gameObject == null)
                return;

            var modification = module.GetType("Modification");
            if (modification == null)
            {
                var baseCtor = spawnableModification.Methods.FirstOrDefault(m => m.IsConstructor && !m.IsStatic && !m.HasParameters);
                if (baseCtor == null)
                    return;
                modification = new TypeDefinition("", "Modification", TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.BeforeFieldInit, spawnableModification);
                var ctor = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, module.TypeSystem.Void);
                var il = ctor.Body.GetILProcessor();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, baseCtor);
                il.Emit(OpCodes.Ret);
                modification.Methods.Add(ctor);
                module.Types.Add(modification);
            }

            var register = modApi.Methods.FirstOrDefault(m => m.Name == "Register" && m.IsStatic && m.Parameters.Count == 1 && m.Parameters[0].ParameterType == spawnableModification);
            if (register != null && !HasStatic(modApi, "Register", modification))
                AddForwarder(modApi, "Register", register, modification);

            var shim = module.AssemblyReferences.FirstOrDefault(r => r.Name == "deSwitcher.Shim");
            if (shim == null)
            {
                shim = new AssemblyNameReference("deSwitcher.Shim", new Version(0, 0, 0, 0));
                module.AssemblyReferences.Add(shim);
            }
            var compat = new TypeReference("", "DeSwitcherCompat", module, shim);
            foreach (string name in new[] { "PerformBeforeSpawn", "PerformAfterSpawn", "PerformMod" })
            {
                if (HasStatic(catalog, name, asset, gameObject))
                    continue;
                var target = new MethodReference(name, module.TypeSystem.Void, compat) { HasThis = false };
                target.Parameters.Add(new ParameterDefinition(asset));
                target.Parameters.Add(new ParameterDefinition(gameObject));
                AddForwarder(catalog, name, target, asset, gameObject);
            }

            var contraptions = catalog.Methods.FirstOrDefault(m => m.Name == "get_Contraptions" && !m.IsStatic && !m.HasParameters);
            if (contraptions != null && !catalog.Properties.Any(p => p.Name == "LocalItems"))
            {
                var getter = new MethodDefinition("get_LocalItems", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName, contraptions.ReturnType);
                var il = getter.Body.GetILProcessor();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, contraptions);
                il.Emit(OpCodes.Ret);
                catalog.Methods.Add(getter);
                catalog.Properties.Add(new PropertyDefinition("LocalItems", PropertyAttributes.None, contraptions.ReturnType) { GetMethod = getter });
            }
        }

        private static bool HasStatic(TypeDefinition type, string name, params TypeReference[] parameters) =>
            type.Methods.Any(m => m.Name == name && m.IsStatic && m.Parameters.Count == parameters.Length
                && m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(parameters.Select(p => p.FullName)));

        private static void AddForwarder(TypeDefinition type, string name, MethodReference target, params TypeReference[] parameters)
        {
            var method = new MethodDefinition(name, MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, type.Module.TypeSystem.Void);
            foreach (var p in parameters)
                method.Parameters.Add(new ParameterDefinition(p));
            var il = method.Body.GetILProcessor();
            for (int i = 0; i < parameters.Length; i++)
                il.Emit(OpCodes.Ldarg, method.Parameters[i]);
            il.Emit(OpCodes.Call, target);
            il.Emit(OpCodes.Ret);
            type.Methods.Add(method);
        }
    }
}
