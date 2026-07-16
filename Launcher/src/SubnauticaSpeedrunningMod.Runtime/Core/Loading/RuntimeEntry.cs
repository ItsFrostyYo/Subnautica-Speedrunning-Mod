using System;
using System.Collections.Generic;
using System.IO;
using SubnauticaSpeedrunningMod.Runtime.Localization;
using SubnauticaSpeedrunningMod.Runtime.Seeds;
using SubnauticaSpeedrunningMod.Shared;

namespace SubnauticaSpeedrunningMod.Runtime
{
    public static class RuntimeEntry
    {
        private static bool _initialized;
        private static readonly object Sync = new object();

        public static void Initialize()
        {
            lock (Sync)
            {
                if (_initialized)
                {
                    return;
                }

                _initialized = true;
            }

            string modRoot = PathLayout.GetModRoot();
            ModLog.Initialize(Path.Combine(Path.Combine(modRoot, "Logs"), "Runtime"));
            ModLog.Info("Runtime initializing.");
            ModLog.Info("Mod root: " + modRoot);
            string gameRoot = PathLayout.GetGameRoot(modRoot);
            ModLog.Info("Game root: " + gameRoot);

            LoaderConfig config = LoaderConfigStore.Load(
                Path.Combine(Path.Combine(modRoot, "Config"), "loader.config.xml"));
            string effectiveApiBaseUrl = ModNetworkBridgeRuntimeHost.PrepareEffectiveApiBaseUrl(modRoot, config.ApiBaseUrl);
            string sessionId = Environment.GetEnvironmentVariable("MOD_SESSION_ID") ?? string.Empty;
            if (string.IsNullOrEmpty(sessionId))
            {
                sessionId = Guid.NewGuid().ToString("N");
            }

            string launcherVersion = Environment.GetEnvironmentVariable("MOD_LAUNCHER_VERSION") ?? "direct-launch";
            RuntimeContext context = new RuntimeContext(
                modRoot,
                gameRoot,
                sessionId,
                launcherVersion,
                config,
                new ModServerApiClient(effectiveApiBaseUrl));
            RuntimeCrashReporter.InstallGlobalHandlers(context);

            GameInstallValidationReport validationReport = GameInstallValidator.Validate(gameRoot);
            if (!validationReport.IsValid)
            {
                string reportPath = RuntimeCrashReporter.WriteCrashReport(
                    context,
                    new RuntimeValidationException(validationReport),
                    "VersionValidationFailed",
                    true);
                ModLog.Error("Runtime version validation failed. Crash report: " + reportPath);
                throw new RuntimeValidationException(validationReport);
            }

            for (int i = 0; i < validationReport.Warnings.Count; i++)
            {
                ModLog.Warn("Validation warning: " + validationReport.Warnings[i]);
            }

            ModLog.Info("Game version validation passed.");
            ModLog.Info("Session id: " + sessionId);
            ModLog.Info("Launcher version: " + launcherVersion);
            ModLog.Info("Networking enabled: " + config.EnableNetworking);
            ModLog.Info("Effective API base URL: " + effectiveApiBaseUrl);
            ModLog.Info("Mod environment: " + config.ModEnvironmentName);
            ModLog.Info("Crash upload enabled: " + config.EnableCrashUpload);

            ModLanguageFileRuntimeHost.Initialize(modRoot, gameRoot);
            ModSeedStore.Initialize(context);

            IList<IModModule> builtInModules = BuiltInModuleLoader.LoadModules(context);
            ModLog.Info("Loaded built-in module count: " + builtInModules.Count);
        }
    }
}
