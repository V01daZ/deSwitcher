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
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(managedDir);
            var readerParams = new ReaderParameters { AssemblyResolver = resolver };

            using (var game = AssemblyDefinition.ReadAssembly(new MemoryStream(gameDll), readerParams))
            using (var boot = AssemblyDefinition.ReadAssembly(new MemoryStream(bootDll), readerParams))
            {
                var module = game.MainModule;
                var init = boot.MainModule.GetType(BootType).Methods.Single(m => m.Name == BootMethod);
                var initRef = module.ImportReference(init);

                foreach (var (typeName, methodName) in Points)
                {
                    var method = module.GetType(typeName)?.Methods.FirstOrDefault(m => m.Name == methodName && m.HasBody);
                    if (method == null)
                        continue;
                    var il = method.Body.GetILProcessor();
                    var first = method.Body.Instructions.First();
                    il.InsertBefore(first, il.Create(OpCodes.Call, initRef));
                    using (var outMs = new MemoryStream())
                    {
                        game.Write(outMs);
                        return outMs.ToArray();
                    }
                }
                throw new Exception("No known entry point found in Assembly-CSharp. The game version is not supported.");
            }
        }
    }
}
