using System;
using System.Linq;
using Mono.Cecil;

namespace DeSwitcher.Core
{
    public enum Shape
    {
        Legacy,
        Registry,
        Unsupported
    }

    public static class GameShape
    {
        public static Shape Detect(string assemblyPath)
        {
            try
            {
                using (var module = ModuleDefinition.ReadModule(assemblyPath, new ReaderParameters { ReadSymbols = false }))
                    return Detect(module);
            }
            catch
            {
                return Shape.Unsupported;
            }
        }

        public static Shape Detect(ModuleDefinition module)
        {
            var loader = module.GetType("ModLoader");
            if (loader == null || !Has(loader, "TryCompileMod") || loader.Fields.All(f => f.Name != "assemblyLocations"))
                return Shape.Unsupported;

            var registry = module.GetType("ModLoaderRegistry");
            if (registry != null)
            {
                bool ok = Has(registry, "AddLoader")
                    && module.GetType("IModLoader") != null
                    && module.GetType("ModLoadStep") != null
                    && module.GetType("CatalogBehaviour") != null
                    && module.GetType("SpawnableModification") != null;
                return ok ? Shape.Registry : Shape.Unsupported;
            }

            bool legacy = Has(loader, "LoadModAt")
                && CallsOutside(module, "ModLoader", "LoadModAt")
                && Has(loader, "Compile")
                && module.GetType("ModificationManager") is TypeDefinition mm && Has(mm, "Invoke")
                && module.GetType("ModMetaData") != null;
            return legacy ? Shape.Legacy : Shape.Unsupported;
        }

        private static bool CallsOutside(ModuleDefinition module, string owner, string method)
        {
            foreach (var type in module.GetTypes())
            {
                if (type.FullName == owner || type.FullName.StartsWith(owner + "/", StringComparison.Ordinal))
                    continue;
                foreach (var m in type.Methods)
                {
                    if (!m.HasBody)
                        continue;
                    foreach (var ins in m.Body.Instructions)
                        if (ins.Operand is MethodReference r && r.Name == method && r.DeclaringType.FullName == owner)
                            return true;
                }
            }
            return false;
        }

        private static bool Has(TypeDefinition type, string method) =>
            type.Methods.Any(m => m.Name == method);
    }
}
