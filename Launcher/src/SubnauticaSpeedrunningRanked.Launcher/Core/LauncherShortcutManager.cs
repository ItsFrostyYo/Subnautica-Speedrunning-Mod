namespace SubnauticaSpeedrunningRanked.Launcher;

internal static class LauncherShortcutManager
{
    private const string ShortcutFileName = "Launch Ranked.lnk";
    private const string LauncherExecutableName = "Launch Ranked.exe";

    public static void EnsureGameShortcut(FileLayout layout)
    {
        try
        {
            string shortcutPath = Path.Combine(layout.GameRoot, ShortcutFileName);
            string targetPath = Path.Combine(layout.RankedRoot, LauncherExecutableName);
            if (!File.Exists(targetPath))
            {
                LauncherLog.Warn("Skipping shortcut creation because the launcher executable was not found at " + targetPath);
                return;
            }

            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                LauncherLog.Warn("Skipping shortcut creation because WScript.Shell is unavailable.");
                return;
            }

            dynamic shell = Activator.CreateInstance(shellType);
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = layout.RankedRoot;
            shortcut.Description = "Launch Subnautica Speedrunning Ranked";
            shortcut.IconLocation = targetPath + ",0";
            shortcut.Save();

            LauncherLog.Info("Ensured game shortcut at " + shortcutPath);
        }
        catch (Exception ex)
        {
            LauncherLog.Warn("Failed to create Launch Ranked shortcut: " + ex.Message);
        }
    }
}
