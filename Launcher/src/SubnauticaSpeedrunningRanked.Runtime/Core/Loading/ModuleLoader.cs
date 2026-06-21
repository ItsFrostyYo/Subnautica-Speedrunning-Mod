using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace SubnauticaSpeedrunningRanked.Runtime
{
    internal static class ModuleLoader
    {
        public static IList<IRankedModule> LoadModules(RuntimeContext context)
        {
            List<IRankedModule> modules = new List<IRankedModule>();
            string moduleRoot = Path.Combine(context.RankedRoot, context.Config.ModuleFolder ?? "Modules");
            Directory.CreateDirectory(moduleRoot);

            foreach (string assemblyPath in Directory.GetFiles(moduleRoot, "*.dll", SearchOption.AllDirectories))
            {
                try
                {
                    if (File.Exists(assemblyPath + ".disabled"))
                    {
                        RankedLog.Warn("Skipping disabled module " + assemblyPath);
                        continue;
                    }

                    Assembly assembly = Assembly.LoadFrom(assemblyPath);
                    foreach (Type type in assembly.GetTypes())
                    {
                        if (!typeof(IRankedModule).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                        {
                            continue;
                        }

                        ConstructorInfo constructor = type.GetConstructor(Type.EmptyTypes);
                        if (constructor == null)
                        {
                            RankedLog.Warn("Skipping module without default constructor: " + type.FullName);
                            continue;
                        }

                        IRankedModule module = (IRankedModule)constructor.Invoke(null);
                        module.Initialize(context);
                        modules.Add(module);
                        RankedLog.Info("Initialized module " + module.Name + " from " + assemblyPath);
                    }
                }
                catch (Exception ex)
                {
                    RankedLog.Error("Failed to load module assembly " + assemblyPath, ex);
                }
            }

            return modules;
        }
    }
}
