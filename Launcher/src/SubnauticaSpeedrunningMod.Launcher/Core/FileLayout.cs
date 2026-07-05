namespace SubnauticaSpeedrunningMod.Launcher;

internal sealed class FileLayout
{
    public string ModRoot { get; private set; } = "";
    public string GameRoot { get; private set; } = "";
    public string BootstrapDirectory { get; private set; } = "";
    public string RuntimeDirectory { get; private set; } = "";
    public string ConfigDirectory { get; private set; } = "";
    public string LogsDirectory { get; private set; } = "";
    public string CrashReportsDirectory { get; private set; } = "";
    public string ModulesDirectory { get; private set; } = "";
    public string CacheDirectory { get; private set; } = "";
    public string DataDirectory { get; private set; } = "";
    public string NativeTransportDirectory { get; private set; } = "";
    public string DoorstopConfigPath { get; private set; } = "";
    public string DoorstopLibraryPath { get; private set; } = "";
    public string DoorstopVersionPath { get; private set; } = "";
    public string BundledDoorstopLibraryPath { get; private set; } = "";
    public string BundledDoorstopVersionPath { get; private set; } = "";
    public string GameExecutablePath { get; private set; } = "";
    public string BootstrapAssemblyPath { get; private set; } = "";
    public string RuntimeAssemblyPath { get; private set; } = "";
    public string LoaderConfigPath { get; private set; } = "";

    public static FileLayout Discover()
    {
        var baseDirectory = Path.GetFullPath(AppContext.BaseDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var directoryName = new DirectoryInfo(baseDirectory).Name;

        string modRoot;
        string gameRoot;
        if (string.Equals(directoryName, "SubnauticaSpeedrunningMod", StringComparison.OrdinalIgnoreCase))
        {
            modRoot = baseDirectory;
            var modDirectory = new DirectoryInfo(modRoot);
            gameRoot = modDirectory.Parent?.FullName
                ?? throw new InvalidOperationException("The launcher must be inside a game-local SubnauticaSpeedrunningMod folder.");
        }
        else
        {
            gameRoot = baseDirectory;
            modRoot = Path.Combine(gameRoot, "SubnauticaSpeedrunningMod");
            if (!Directory.Exists(modRoot))
            {
                throw new InvalidOperationException(
                    "The launcher could not find a SubnauticaSpeedrunningMod folder beside Subnautica.exe.");
            }
        }

        return new FileLayout
        {
            ModRoot = modRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            GameRoot = gameRoot,
            BootstrapDirectory = Path.Combine(modRoot, "Bootstrap"),
            RuntimeDirectory = Path.Combine(modRoot, "Runtime"),
            ConfigDirectory = Path.Combine(modRoot, "Config"),
            LogsDirectory = Path.Combine(modRoot, "Logs"),
            CrashReportsDirectory = Path.Combine(modRoot, Path.Combine("Logs", "CrashReports")),
            ModulesDirectory = Path.Combine(modRoot, "Modules"),
            CacheDirectory = Path.Combine(modRoot, "Cache"),
            DataDirectory = Path.Combine(modRoot, "Data"),
            NativeTransportDirectory = Path.Combine(modRoot, "NativeTransport"),
            DoorstopConfigPath = Path.Combine(gameRoot, "doorstop_config.ini"),
            DoorstopLibraryPath = Path.Combine(gameRoot, "winhttp.dll"),
            DoorstopVersionPath = Path.Combine(gameRoot, ".doorstop_version"),
            BundledDoorstopLibraryPath = Path.Combine(modRoot, "NativeTransport", "winhttp.dll"),
            BundledDoorstopVersionPath = Path.Combine(modRoot, "NativeTransport", ".doorstop_version"),
            GameExecutablePath = Path.Combine(gameRoot, "Subnautica.exe"),
            BootstrapAssemblyPath = Path.Combine(modRoot, "Bootstrap", "SubnauticaSpeedrunningMod.Bootstrap.dll"),
            RuntimeAssemblyPath = Path.Combine(modRoot, "Runtime", "SubnauticaSpeedrunningMod.Runtime.dll"),
            LoaderConfigPath = Path.Combine(modRoot, "Config", "loader.config.xml")
        };
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(BootstrapDirectory);
        Directory.CreateDirectory(RuntimeDirectory);
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(CrashReportsDirectory);
        Directory.CreateDirectory(ModulesDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(NativeTransportDirectory);
    }

    public void EnsureDefaultConfig()
    {
        if (File.Exists(LoaderConfigPath))
        {
            return;
        }

        var defaultConfig = """
<?xml version="1.0" encoding="utf-8"?>
<LoaderConfig>
  <EnableNetworking>false</EnableNetworking>
  <ApiBaseUrl>https://example.invalid/</ApiBaseUrl>
  <ModEnvironmentName>production</ModEnvironmentName>
  <EnableCrashUpload>false</EnableCrashUpload>
  <ModuleFolder>Modules</ModuleFolder>
  <LogLevel>Info</LogLevel>
</LoaderConfig>
""";

        File.WriteAllText(LoaderConfigPath, defaultConfig);
    }
}
