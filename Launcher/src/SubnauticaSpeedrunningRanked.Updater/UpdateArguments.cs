namespace SubnauticaSpeedrunningRanked.Updater;

internal sealed class UpdateArguments
{
    public int WaitForPid { get; private set; }
    public string AssetUrl { get; private set; } = "";
    public string VersionLabel { get; private set; } = "";
    public string InstallRoot { get; private set; } = "";
    public string LauncherRelativePath { get; private set; } = "";
    public string RelaunchArgument { get; private set; } = "";

    public static UpdateArguments Parse(string[] args)
    {
        var options = new UpdateArguments();
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            string value = i + 1 < args.Length ? args[i + 1] : string.Empty;
            switch (arg)
            {
                case "--wait-for-pid":
                    options.WaitForPid = int.Parse(value);
                    i++;
                    break;
                case "--asset-url":
                    options.AssetUrl = value;
                    i++;
                    break;
                case "--version":
                    options.VersionLabel = value;
                    i++;
                    break;
                case "--install-root":
                    options.InstallRoot = value;
                    i++;
                    break;
                case "--launcher-relative-path":
                    options.LauncherRelativePath = value;
                    i++;
                    break;
                case "--relaunch-arg":
                    options.RelaunchArgument = value;
                    i++;
                    break;
            }
        }

        if (options.WaitForPid <= 0)
        {
            throw new InvalidOperationException("Missing or invalid --wait-for-pid.");
        }

        if (string.IsNullOrWhiteSpace(options.AssetUrl))
        {
            throw new InvalidOperationException("Missing --asset-url.");
        }

        if (string.IsNullOrWhiteSpace(options.InstallRoot))
        {
            throw new InvalidOperationException("Missing --install-root.");
        }

        if (string.IsNullOrWhiteSpace(options.LauncherRelativePath))
        {
            throw new InvalidOperationException("Missing --launcher-relative-path.");
        }

        return options;
    }
}
