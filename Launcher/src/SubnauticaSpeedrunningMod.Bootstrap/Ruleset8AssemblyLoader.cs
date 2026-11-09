using System;
using System.IO;
using System.Reflection;
using Mono.Cecil;

namespace SubnauticaSpeedrunningMod.Bootstrap
{
    internal static class Ruleset8AssemblyLoader
    {
        internal static void Load(string gameRoot, string runtimePath, Action<string> write)
        {
            Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < loaded.Length; i++)
            {
                if (string.Equals(loaded[i].GetName().Name, "Assembly-CSharp", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Assembly-CSharp loaded before the Ruleset 8 memory patch could be installed.");
                }
            }

            string managedRoot = Path.Combine(Path.Combine(gameRoot, "Subnautica_Data"), "Managed");
            string assemblyPath = Path.Combine(managedRoot, "Assembly-CSharp.dll");
            DefaultAssemblyResolver resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(managedRoot);
            resolver.AddSearchDirectory(Path.GetDirectoryName(runtimePath));

            ReaderParameters readerParameters = new ReaderParameters
            {
                AssemblyResolver = resolver,
                ReadSymbols = false,
                InMemory = true
            };

            byte[] patchedBytes;
            using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(assemblyPath, readerParameters))
            {
                Version runtimeVersion = AssemblyName.GetAssemblyName(runtimePath).Version;
                Ruleset8AssemblyPatcher.Patch(assembly, runtimeVersion, write);
                using (MemoryStream output = new MemoryStream())
                {
                    assembly.Write(output);
                    patchedBytes = output.ToArray();
                }
            }

            Assembly.Load(patchedBytes);
            write("Loaded Ruleset 8 build-247 generation patch in memory; the installed Assembly-CSharp.dll was not modified.");
        }
    }
}
