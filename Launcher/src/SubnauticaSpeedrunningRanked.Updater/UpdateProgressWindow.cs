using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Windows.Forms;
using SubnauticaSpeedrunningRanked.Shared;

namespace SubnauticaSpeedrunningRanked.Updater;

internal sealed class UpdateProgressWindow : Form
{
    private readonly UpdateArguments _options;
    private readonly Label _statusLabel;
    private readonly ProgressBar _progressBar;

    public int ExitCode { get; private set; }

    public UpdateProgressWindow(UpdateArguments options)
    {
        _options = options;
        Text = "Updating Ranked Client";
        Width = 560;
        Height = 150;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;

        _statusLabel = new Label
        {
            Left = 18,
            Top = 18,
            Width = 500,
            Height = 36,
            Text = "Preparing update..."
        };

        _progressBar = new ProgressBar
        {
            Left = 18,
            Top = 64,
            Width = 500,
            Height = 22,
            Minimum = 0,
            Maximum = 100,
            Style = ProgressBarStyle.Continuous
        };

        Controls.Add(_statusLabel);
        Controls.Add(_progressBar);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);

        try
        {
            await RunUpdateAsync();
            ExitCode = 0;
            Close();
        }
        catch (Exception ex)
        {
            ExitCode = 1;
            MessageBox.Show(
                "The ranked client update failed." + Environment.NewLine + Environment.NewLine + ex.Message,
                "Update Failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1);
            Close();
        }
    }

    private async Task RunUpdateAsync()
    {
        SetStatus("Waiting for launcher to close...", 3);
        await WaitForLauncherExitAsync();

        string workingRoot = Path.Combine(Path.GetTempPath(), "SubnauticaSpeedrunningRanked", "UpdateWork", Guid.NewGuid().ToString("N"));
        string zipPath = Path.Combine(workingRoot, "ranked-update.zip");
        string extractRoot = Path.Combine(workingRoot, "extract");
        Directory.CreateDirectory(workingRoot);

        try
        {
            await DownloadUpdateAsync(zipPath);
            SetStatus("Extracting update package...", 74);
            ZipFile.ExtractToDirectory(zipPath, extractRoot);
            ApplyExtractedFiles(extractRoot, _options.InstallRoot);
            RelaunchLauncher();
        }
        finally
        {
            TryDeleteDirectory(workingRoot);
        }
    }

    private async Task WaitForLauncherExitAsync()
    {
        try
        {
            using Process process = Process.GetProcessById(_options.WaitForPid);
            while (!process.HasExited)
            {
                await Task.Delay(100);
            }
        }
        catch
        {
            // The launcher is already gone.
        }
    }

    private async Task DownloadUpdateAsync(string zipPath)
    {
        SetStatus("Downloading " + (_options.VersionLabel.Length > 0 ? _options.VersionLabel : RankedClientRelease.DisplayVersion) + "...", 5);

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(10);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SubnauticaSpeedrunningRanked-Updater/" + RankedClientRelease.DisplayVersion);

        using HttpResponseMessage response = await httpClient.GetAsync(_options.AssetUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        long totalBytes = response.Content.Headers.ContentLength ?? -1;
        using Stream contentStream = await response.Content.ReadAsStreamAsync();
        using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920];
        long downloadedBytes = 0;
        int read;
        while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, read);
            downloadedBytes += read;

            if (totalBytes > 0)
            {
                int progress = 5 + (int)(65L * downloadedBytes / totalBytes);
                SetStatus("Downloading update... " + (downloadedBytes * 100L / totalBytes) + "%", progress);
            }
        }
    }

    private void ApplyExtractedFiles(string extractRoot, string installRoot)
    {
        SetStatus("Applying files...", 80);

        string[] legacyRootLauncherFiles =
        {
            "Launch Ranked.exe",
            "Launch Ranked.dll",
            "Launch Ranked.deps.json",
            "Launch Ranked.runtimeconfig.json"
        };

        for (int i = 0; i < legacyRootLauncherFiles.Length; i++)
        {
            string path = Path.Combine(installRoot, legacyRootLauncherFiles[i]);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        string[] allFiles = Directory.GetFiles(extractRoot, "*", SearchOption.AllDirectories);
        int copiedCount = 0;
        for (int i = 0; i < allFiles.Length; i++)
        {
            string sourcePath = allFiles[i];
            string relativePath = sourcePath.Substring(extractRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(relativePath, "INSTALL.txt", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string destinationPath = Path.Combine(installRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? installRoot);
            File.Copy(sourcePath, destinationPath, true);

            copiedCount++;
            int progress = 80 + (int)(18L * copiedCount / Math.Max(1, allFiles.Length));
            SetStatus("Applying files... " + copiedCount + "/" + allFiles.Length, progress);
        }

        SetStatus("Finishing update...", 99);
    }

    private void RelaunchLauncher()
    {
        string launcherPath = Path.Combine(_options.InstallRoot, _options.LauncherRelativePath);
        if (!File.Exists(launcherPath))
        {
            throw new FileNotFoundException("Updated launcher executable was not found.", launcherPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = launcherPath,
            WorkingDirectory = Path.GetDirectoryName(launcherPath) ?? _options.InstallRoot,
            UseShellExecute = true
        };

        if (!string.IsNullOrWhiteSpace(_options.RelaunchArgument))
        {
            startInfo.ArgumentList.Add(_options.RelaunchArgument);
        }

        Process.Start(startInfo);
    }

    private void SetStatus(string text, int progress)
    {
        _statusLabel.Text = text;
        _progressBar.Value = Math.Max(_progressBar.Minimum, Math.Min(_progressBar.Maximum, progress));
        _statusLabel.Refresh();
        _progressBar.Refresh();
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
            // Best-effort cleanup only.
        }
    }
}
