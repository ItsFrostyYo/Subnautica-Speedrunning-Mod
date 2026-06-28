using System;
using System.Collections.Generic;
using SubnauticaSpeedrunningMod.Runtime.RunTracking;
using SubnauticaSpeedrunningMod.Runtime.Seeds;
using SubnauticaSpeedrunningMod.Runtime.Ui;

namespace SubnauticaSpeedrunningMod.Runtime
{
    internal static class BuiltInModuleLoader
    {
        public static IList<IModModule> LoadModules(RuntimeContext context)
        {
            List<IModModule> modules = new List<IModModule>();
            InitializeModule(modules, new ModSeedModule(), context, "seeds");
            InitializeModule(modules, new ModUiCustomizationModule(), context, "ui");
            InitializeModule(modules, new ModRunTrackingModule(), context, "runtracking");
            return modules;
        }

        private static void InitializeModule(ICollection<IModModule> modules, IModModule module, RuntimeContext context, string moduleToken)
        {
            if (!ShouldLoadModule(moduleToken))
            {
                ModLog.Info("Skipped built-in module " + module.Name + " due to MOD_ENABLE_ONLY_MODULES filter.");
                return;
            }

            try
            {
                module.Initialize(context);
                modules.Add(module);
                ModLog.Info("Initialized built-in module " + module.Name + ".");
            }
            catch (Exception ex)
            {
                ModLog.Error("Failed to initialize built-in module " + module.Name + ".", ex);
            }
        }

        private static bool ShouldLoadModule(string moduleToken)
        {
            string filter = Environment.GetEnvironmentVariable("MOD_ENABLE_ONLY_MODULES");
            if (string.IsNullOrEmpty(filter))
            {
                return true;
            }

            string[] tokens = filter.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string currentToken = tokens[i].Trim();
                if (string.Equals(currentToken, moduleToken, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
