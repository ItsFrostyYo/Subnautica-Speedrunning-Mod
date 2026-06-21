using System;
using System.Collections.Generic;
using SubnauticaSpeedrunningRanked.Runtime.RunTracking;
using SubnauticaSpeedrunningRanked.Runtime.Seeds;
using SubnauticaSpeedrunningRanked.Runtime.Ui;

namespace SubnauticaSpeedrunningRanked.Runtime
{
    internal static class BuiltInModuleLoader
    {
        public static IList<IRankedModule> LoadModules(RuntimeContext context)
        {
            List<IRankedModule> modules = new List<IRankedModule>();
            InitializeModule(modules, new RankedSeedModule(), context, "seeds");
            InitializeModule(modules, new RankedUiCustomizationModule(), context, "ui");
            InitializeModule(modules, new RankedRunTrackingModule(), context, "runtracking");
            return modules;
        }

        private static void InitializeModule(ICollection<IRankedModule> modules, IRankedModule module, RuntimeContext context, string moduleToken)
        {
            if (!ShouldLoadModule(moduleToken))
            {
                RankedLog.Info("Skipped built-in module " + module.Name + " due to RANKED_ENABLE_ONLY_MODULES filter.");
                return;
            }

            try
            {
                module.Initialize(context);
                modules.Add(module);
                RankedLog.Info("Initialized built-in module " + module.Name + ".");
            }
            catch (Exception ex)
            {
                RankedLog.Error("Failed to initialize built-in module " + module.Name + ".", ex);
            }
        }

        private static bool ShouldLoadModule(string moduleToken)
        {
            string filter = Environment.GetEnvironmentVariable("RANKED_ENABLE_ONLY_MODULES");
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
