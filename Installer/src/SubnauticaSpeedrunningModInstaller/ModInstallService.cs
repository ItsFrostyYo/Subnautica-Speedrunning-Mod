using System.IO.Compression;
using SubnauticaSpeedrunningMod.Shared;

namespace SubnauticaSpeedrunningModInstaller;

internal static class ModInstallService
{
    private static readonly string[] PreservedModDirectories =
    {
        "Config",
        "Data",
        "Logs",
        "Cache",
        "Modules"
    };

    private static readonly string[] LegacyRootFilesToDelete =
    {
        "Launch Ranked.exe",
        "Launch Ranked.dll",
        "Launch Ranked.deps.json",
        "Launch Ranked.runtimeconfig.json",
        "Launch Ranked.cmd",
        "Launch Ranked.lnk",
        "Launch Mod.cmd",
        "Launch Mod.lnk"
    };

    public static async Task<ReleasePackageInfo> InstallLatestAsync(
        string gameRoot,
        IProgress<InstallProgress> progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(gameRoot))
        {
            throw new InvalidOperationException("A Subnautica game folder is required.");
        }

        GameInstallValidationReport validation = GameInstallValidator.Validate(gameRoot);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.ToUserFacingMessage());
        }

        progress.Report(new InstallProgress(5, "Checking latest release..."));
        ReleasePackageInfo package = await ReleaseManifestClient.GetLatestPackageAsync(cancellationToken).ConfigureAwait(false);

        string tempRoot = Path.Combine(
            Path.GetTempPath(),
            "SubnauticaSpeedrunningModInstaller",
            Guid.NewGuid().ToString("N"));
        string zipPath = Path.Combine(tempRoot, package.ZipFileName);
        string extractPath = Path.Combine(tempRoot, "Extracted");

        try
        {
            Directory.CreateDirectory(tempRoot);
            Directory.CreateDirectory(extractPath);

            progress.Report(new InstallProgress(12, "Downloading " + package.Version + "..."));
            await DownloadFileAsync(package.ZipUrl, zipPath, progress, cancellationToken).ConfigureAwait(false);

            progress.Report(new InstallProgress(70, "Extracting package..."));
            ZipFile.ExtractToDirectory(zipPath, extractPath, true);

            ValidateExtractedPackage(extractPath);

            progress.Report(new InstallProgress(80, "Installing files into the game folder..."));
            InstallExtractedPackage(extractPath, gameRoot);

            progress.Report(new InstallProgress(100, "Install complete."));
            return package;
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static async Task DownloadFileAsync(
        string url,
        string destinationPath,
        IProgress<InstallProgress> progress,
        CancellationToken cancellationToken)
    {
        using HttpClient client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
        using HttpResponseMessage response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        long? contentLength = response.Content.Headers.ContentLength;
        await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using FileStream destination = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

        byte[] buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            totalRead += bytesRead;

            if (contentLength is long totalLength && totalLength > 0)
            {
                int percent = 12 + (int)Math.Min(50, Math.Round((double)totalRead / totalLength * 50d));
                progress.Report(new InstallProgress(percent, "Downloading package..."));
            }
        }
    }

    private static void ValidateExtractedPackage(string extractPath)
    {
        string modRoot = Path.Combine(extractPath, "SubnauticaSpeedrunningMod");
        string launcherPath = Path.Combine(modRoot, "Launch Mod.exe");
        string bootstrapPath = Path.Combine(modRoot, Path.Combine("Bootstrap", "SubnauticaSpeedrunningMod.Bootstrap.dll"));
        string runtimePath = Path.Combine(modRoot, Path.Combine("Runtime", "SubnauticaSpeedrunningMod.Runtime.dll"));
        string doorstopPath = Path.Combine(extractPath, "doorstop_config.ini");
        string transportPath = Path.Combine(extractPath, "winhttp.dll");

        if (!Directory.Exists(modRoot) ||
            !File.Exists(launcherPath) ||
            !File.Exists(bootstrapPath) ||
            !File.Exists(runtimePath) ||
            !File.Exists(doorstopPath) ||
            !File.Exists(transportPath))
        {
            throw new InvalidOperationException("The downloaded release package is incomplete.");
        }
    }

    private static void InstallExtractedPackage(string extractPath, string gameRoot)
    {
        string legacyInstallRoot = Path.Combine(gameRoot, "SubnauticaSpeedrunningRanked");
        string modInstallRoot = Path.Combine(gameRoot, "SubnauticaSpeedrunningMod");
        string extractedModRoot = Path.Combine(extractPath, "SubnauticaSpeedrunningMod");

        if (Directory.Exists(legacyInstallRoot))
        {
            Directory.Delete(legacyInstallRoot, true);
        }

        if (Directory.Exists(extractedModRoot))
        {
            ReplaceModRoot(extractedModRoot, modInstallRoot);
        }

        foreach (string fileName in LegacyRootFilesToDelete)
        {
            string path = Path.Combine(gameRoot, fileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        foreach (string directory in Directory.GetDirectories(extractPath))
        {
            string name = Path.GetFileName(directory);
            if (string.Equals(name, "SubnauticaSpeedrunningMod", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(name, "__MACOSX", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            CopyDirectoryContents(directory, Path.Combine(gameRoot, name), skipPreservedDirectories: false);
        }

        foreach (string file in Directory.GetFiles(extractPath))
        {
            string name = Path.GetFileName(file);
            if (string.Equals(name, "INSTALL.txt", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Copy(file, Path.Combine(gameRoot, name), true);
        }
    }

    private static void ReplaceModRoot(string sourceRoot, string destinationRoot)
    {
        Directory.CreateDirectory(destinationRoot);

        string[] existingEntries = Directory.GetFileSystemEntries(destinationRoot);
        for (int i = 0; i < existingEntries.Length; i++)
        {
            string entry = existingEntries[i];
            string name = Path.GetFileName(entry);
            if (ShouldPreservePath(name))
            {
                continue;
            }

            DeleteFileSystemEntry(entry);
        }

        CopyDirectoryContents(sourceRoot, destinationRoot, skipPreservedDirectories: true);
    }

    private static void CopyDirectoryContents(string sourceRoot, string destinationRoot, bool skipPreservedDirectories)
    {
        Directory.CreateDirectory(destinationRoot);

        foreach (string directory in Directory.GetDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            string relative = directory.Substring(sourceRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (skipPreservedDirectories && IsPathUnderPreservedDirectory(relative))
            {
                continue;
            }

            Directory.CreateDirectory(Path.Combine(destinationRoot, relative));
        }

        foreach (string file in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            string relative = file.Substring(sourceRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (skipPreservedDirectories && IsPathUnderPreservedDirectory(relative))
            {
                continue;
            }

            string destinationPath = Path.Combine(destinationRoot, relative);
            string? destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(file, destinationPath, true);
        }
    }

    private static bool ShouldPreservePath(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        for (int i = 0; i < PreservedModDirectories.Length; i++)
        {
            if (string.Equals(name, PreservedModDirectories[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPathUnderPreservedDirectory(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        string normalizedRelativePath = relativePath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (int i = 0; i < PreservedModDirectories.Length; i++)
        {
            string preservedName = PreservedModDirectories[i];
            if (string.Equals(normalizedRelativePath, preservedName, StringComparison.OrdinalIgnoreCase) ||
                normalizedRelativePath.StartsWith(preservedName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                normalizedRelativePath.StartsWith(preservedName + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void DeleteFileSystemEntry(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
            return;
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
        }
    }
}
