using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using SubnauticaSpeedrunningMod.Shared;

namespace SubnauticaSpeedrunningMod.Launcher;

internal static class LauncherUpdateService
{
    private static readonly HttpClient HttpClient = CreateHttpClient();
    public static bool TryRunUpdateFlow(FileLayout layout, string[] args)
    {
        if (args.Any(static arg => string.Equals(arg, "--skip-update-check", StringComparison.OrdinalIgnoreCase)) ||
            args.Any(static arg => string.Equals(arg, "--setup-only", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (!TryGetAvailableUpdate(out AvailableUpdate update))
        {
            return false;
        }

        LauncherLog.Info("Update available: " + update.VersionLabel + " (" + update.AssetName + ")");
        bool accepted = LauncherAlert.Confirm(
            "Update Available",
            "A new Subnautica Speedrunning Mod client update is available." + Environment.NewLine + Environment.NewLine
            + "Current: " + LauncherVersion.DisplayVersion + Environment.NewLine
            + "Latest: " + update.VersionLabel + Environment.NewLine + Environment.NewLine
            + "Close the launcher, update now, and relaunch automatically?");
        if (!accepted)
        {
            LauncherLog.Info("User declined automatic update.");
            return false;
        }

        string tempRoot = Path.Combine(
            Path.GetTempPath(),
            "SubnauticaSpeedrunningMod",
            "Updater",
            Guid.NewGuid().ToString("N"));
        string updaterSourceRoot = Path.Combine(layout.ModRoot, "Updater");
        if (!Directory.Exists(updaterSourceRoot))
        {
            LauncherLog.Warn("Updater folder was not found at " + updaterSourceRoot + "; continuing without auto-update.");
            LauncherAlert.ShowError(
                "Updater Missing",
                "The updater files are missing from the mod install. Reinstall the mod client package first.");
            return false;
        }

        string tempUpdaterRoot = Path.Combine(tempRoot, "Updater");
        Directory.CreateDirectory(tempUpdaterRoot);
        CopyDirectory(updaterSourceRoot, tempUpdaterRoot);

        string updaterExecutablePath = Path.Combine(tempUpdaterRoot, "Mod Updater.exe");
        if (!File.Exists(updaterExecutablePath))
        {
            LauncherLog.Warn("Updater executable was not found at " + updaterExecutablePath + "; continuing without auto-update.");
            LauncherAlert.ShowError(
                "Updater Missing",
                "The updater executable is missing from the mod install. Reinstall the mod client package first.");
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = updaterExecutablePath,
            WorkingDirectory = Path.GetDirectoryName(updaterExecutablePath) ?? tempRoot,
            UseShellExecute = true
        };

        startInfo.ArgumentList.Add("--wait-for-pid");
        startInfo.ArgumentList.Add(Process.GetCurrentProcess().Id.ToString());
        startInfo.ArgumentList.Add("--asset-url");
        startInfo.ArgumentList.Add(update.AssetUrl);
        startInfo.ArgumentList.Add("--version");
        startInfo.ArgumentList.Add(update.VersionLabel);
        startInfo.ArgumentList.Add("--install-root");
        startInfo.ArgumentList.Add(layout.GameRoot);
        startInfo.ArgumentList.Add("--launcher-relative-path");
        startInfo.ArgumentList.Add(@"SubnauticaSpeedrunningMod\Launch Mod.exe");
        startInfo.ArgumentList.Add("--relaunch-arg");
        startInfo.ArgumentList.Add("--skip-update-check");

        LauncherLog.Info("Starting updater from " + updaterExecutablePath);
        Process.Start(startInfo);
        return true;
    }

    private static bool TryGetAvailableUpdate(out AvailableUpdate update)
    {
        update = null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ModClientRelease.ReleaseManifestUrl);
            request.Headers.UserAgent.ParseAdd("SubnauticaSpeedrunningMod/" + LauncherVersion.DisplayVersion);
            request.Headers.Accept.ParseAdd("application/json");

            using HttpResponseMessage response = HttpClient.SendAsync(request).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                LauncherLog.Warn("Update check skipped because GitHub returned " + (int)response.StatusCode + " " + response.ReasonPhrase + ".");
                return false;
            }

            string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            string versionLabel = ReadString(root, "version");
            string zipFileName = ReadString(root, "zipFileName");
            if (string.IsNullOrWhiteSpace(versionLabel) ||
                string.IsNullOrWhiteSpace(zipFileName) ||
                !IsNewerRelease(versionLabel, LauncherVersion.DisplayVersion))
            {
                return false;
            }

            string assetUrl = ReadString(root, "zipUrl");
            if (string.IsNullOrWhiteSpace(assetUrl))
            {
                assetUrl = ModClientRelease.ReleaseDownloadBaseUrl + zipFileName;
            }

            update = new AvailableUpdate(versionLabel, zipFileName, assetUrl);
            return true;
        }
        catch (Exception ex)
        {
            LauncherLog.Warn("Update check failed: " + ex.Message);
            return false;
        }
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        return value.GetString() ?? string.Empty;
    }

    private static bool IsNewerRelease(string candidateLabel, string currentLabel)
    {
        if (!TryParseVersion(candidateLabel, out ParsedReleaseVersion candidate) ||
            !TryParseVersion(currentLabel, out ParsedReleaseVersion current))
        {
            return false;
        }

        int channelComparison = string.Compare(candidate.Channel, current.Channel, StringComparison.OrdinalIgnoreCase);
        if (channelComparison != 0)
        {
            return channelComparison > 0;
        }

        if (candidate.Major != current.Major)
        {
            return candidate.Major > current.Major;
        }

        if (candidate.Minor != current.Minor)
        {
            return candidate.Minor > current.Minor;
        }

        return candidate.Patch > current.Patch;
    }

    private static bool TryParseVersion(string label, out ParsedReleaseVersion version)
    {
        version = default(ParsedReleaseVersion);
        if (string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        string[] parts = label.Split('-');
        if (parts.Length != 2)
        {
            return false;
        }

        string[] numericParts = parts[1].Split('.');
        if (numericParts.Length != 3)
        {
            return false;
        }

        int major;
        int minor;
        int patch;
        if (!int.TryParse(numericParts[0], out major) ||
            !int.TryParse(numericParts[1], out minor) ||
            !int.TryParse(numericParts[2], out patch))
        {
            return false;
        }

        version = new ParsedReleaseVersion(parts[0], major, minor, patch);
        return true;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        return client;
    }

    private static void CopyDirectory(string sourceRoot, string destinationRoot)
    {
        Directory.CreateDirectory(destinationRoot);

        foreach (string directory in Directory.GetDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            string relativePath = directory.Substring(sourceRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Directory.CreateDirectory(Path.Combine(destinationRoot, relativePath));
        }

        foreach (string file in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            string relativePath = file.Substring(sourceRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string destinationPath = Path.Combine(destinationRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? destinationRoot);
            File.Copy(file, destinationPath, true);
        }
    }

    private sealed class AvailableUpdate
    {
        public AvailableUpdate(string versionLabel, string assetName, string assetUrl)
        {
            VersionLabel = versionLabel;
            AssetName = assetName;
            AssetUrl = assetUrl;
        }

        public string VersionLabel { get; private set; }
        public string AssetName { get; private set; }
        public string AssetUrl { get; private set; }
    }

    private struct ParsedReleaseVersion
    {
        public ParsedReleaseVersion(string channel, int major, int minor, int patch)
        {
            Channel = channel;
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        public string Channel { get; private set; }
        public int Major { get; private set; }
        public int Minor { get; private set; }
        public int Patch { get; private set; }
    }
}
