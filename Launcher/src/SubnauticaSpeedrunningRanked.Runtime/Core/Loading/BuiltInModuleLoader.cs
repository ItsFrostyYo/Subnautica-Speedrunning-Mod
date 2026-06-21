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
            InitializeModule(modules, new RankedSeedModule(), context);
            InitializeModule(modules, new RankedUiCustomizationModule(), context);
            InitializeModule(modules, new RankedRunTrackingModule(), context);
            return modules;
        }

        private static void InitializeModule(ICollection<IRankedModule> modules, IRankedModule module, RuntimeContext context)
        {
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
    }
}
