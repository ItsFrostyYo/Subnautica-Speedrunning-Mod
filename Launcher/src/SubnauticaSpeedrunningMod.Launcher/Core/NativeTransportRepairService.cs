namespace SubnauticaSpeedrunningMod.Launcher;

internal static class NativeTransportRepairService
{
    public static void EnsureInstalled(FileLayout layout)
    {
        TryRestoreFile(layout.BundledDoorstopLibraryPath, layout.DoorstopLibraryPath, "winhttp.dll");
        TryRestoreFile(layout.BundledDoorstopVersionPath, layout.DoorstopVersionPath, ".doorstop_version");
    }

    private static void TryRestoreFile(string bundledPath, string installedPath, string label)
    {
        if (File.Exists(installedPath))
        {
            return;
        }

        if (!File.Exists(bundledPath))
        {
            LauncherLog.Warn("Bundled native transport file missing for " + label + " at " + bundledPath + ".");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(installedPath) ?? AppContext.BaseDirectory);
        File.Copy(bundledPath, installedPath, true);
        LauncherLog.Info("Restored missing " + label + " into the game root.");
    }
}
