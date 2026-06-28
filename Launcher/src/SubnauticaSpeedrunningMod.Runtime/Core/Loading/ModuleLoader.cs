using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace SubnauticaSpeedrunningMod.Runtime
{
    internal static class ModuleLoader
    {
        public static IList<IModModule> LoadModules(RuntimeContext context)
        {
            List<IModModule> modules = new List<IModModule>();
            string moduleRoot = Path.Combine(context.ModRoot, context.Config.ModuleFolder ?? "Modules");
            Directory.CreateDirectory(moduleRoot);

            foreach (string assemblyPath in Directory.GetFiles(moduleRoot, "*.dll", SearchOption.AllDirectories))
            {
                try
                {
                    if (File.Exists(assemblyPath + ".disabled"))
                    {
                        ModLog.Warn("Skipping disabled module " + assemblyPath);
                        continue;
                    }

                    Assembly assembly = Assembly.LoadFrom(assemblyPath);
                    foreach (Type type in assembly.GetTypes())
                    {
                        if (!typeof(IModModule).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                        {
                            continue;
                        }

                        ConstructorInfo constructor = type.GetConstructor(Type.EmptyTypes);
                        if (constructor == null)
                        {
                            ModLog.Warn("Skipping module without default constructor: " + type.FullName);
                            continue;
                        }

                        IModModule module = (IModModule)constructor.Invoke(null);
                        module.Initialize(context);
                        modules.Add(module);
                        ModLog.Info("Initialized module " + module.Name + " from " + assemblyPath);
                    }
                }
                catch (Exception ex)
                {
                    ModLog.Error("Failed to load module assembly " + assemblyPath, ex);
                }
            }

            return modules;
        }
    }
}
