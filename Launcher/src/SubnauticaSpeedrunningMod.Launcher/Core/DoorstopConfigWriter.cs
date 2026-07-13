namespace SubnauticaSpeedrunningMod.Launcher;

internal static class DoorstopConfigWriter
{
    public static void Write(FileLayout layout)
    {
        var targetAssembly = @"SubnauticaSpeedrunningMod\Bootstrap\SubnauticaSpeedrunningMod.Bootstrap.dll";

        if (File.Exists(layout.DoorstopConfigPath))
        {
            var backupDirectory = Path.Combine(layout.ConfigDirectory, "Backups");
            Directory.CreateDirectory(backupDirectory);
            var backupPath = Path.Combine(backupDirectory, "doorstop_config.original.ini");
            if (!File.Exists(backupPath))
            {
                File.Copy(layout.DoorstopConfigPath, backupPath, false);
            }
        }

        var content = string.Join(
            Environment.NewLine,
            new[]
            {
                "# Managed by Subnautica Speedrunning Mod Launcher",
                "[General]",
                "enabled = true",
                "target_assembly=" + targetAssembly,
                "redirect_output_log = false",
                "boot_config_override =",
                "ignore_disable_switch = false",
                "",
                "[UnityMono]",
                "dll_search_path_override =",
                "debug_enabled = false",
                "debug_address = 127.0.0.1:10000",
                "debug_suspend = false",
                ""
            });

        File.WriteAllText(layout.DoorstopConfigPath, content);
        LauncherLog.Info("Wrote doorstop config to " + layout.DoorstopConfigPath);
    }
}
