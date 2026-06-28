using SubnauticaSpeedrunningMod.Shared;

namespace SubnauticaSpeedrunningModInstaller;

internal sealed class MainForm : Form
{
    private readonly TextBox _gameFolderTextBox;
    private readonly Button _browseButton;
    private readonly Button _installButton;
    private readonly Button _closeButton;
    private readonly Label _latestVersionLabel;
    private readonly Label _statusLabel;
    private readonly ProgressBar _progressBar;

    private bool _installInProgress;
    private bool _installFinished;
    private string _selectedGameRoot = string.Empty;
    private GameInstallValidationReport? _lastValidationReport;
    private ReleasePackageInfo? _latestPackage;

    public MainForm()
    {
        Text = "Subnautica Speedrun Mod Installer";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(640, 240);

        Label titleLabel = new Label
        {
            Left = 20,
            Top = 16,
            Width = 600,
            Height = 32,
            Text = "Subnautica Speedrun Mod",
            Font = new Font("Segoe UI", 18f, FontStyle.Bold)
        };

        Label pathLabel = new Label
        {
            Left = 20,
            Top = 70,
            Width = 120,
            Height = 20,
            Text = "Install Path"
        };

        _gameFolderTextBox = new TextBox
        {
            Left = 20,
            Top = 94,
            Width = 470,
            Height = 26,
            ReadOnly = true,
            TabStop = false
        };

        _browseButton = new Button
        {
            Left = 500,
            Top = 92,
            Width = 104,
            Height = 30,
            Text = "Browse"
        };
        _browseButton.Click += BrowseButton_Click;

        _latestVersionLabel = new Label
        {
            Left = 20,
            Top = 132,
            Width = 584,
            Height = 20,
            Text = "Latest Version: Checking..."
        };

        _progressBar = new ProgressBar
        {
            Left = 20,
            Top = 160,
            Width = 584,
            Height = 18,
            Minimum = 0,
            Maximum = 100
        };

        _statusLabel = new Label
        {
            Left = 20,
            Top = 184,
            Width = 584,
            Height = 20,
            Text = "Choose your Subnautica install folder."
        };

        _installButton = new Button
        {
            Left = 418,
            Top = 206,
            Width = 90,
            Height = 28,
            Text = "Install",
            Enabled = false
        };
        _installButton.Click += InstallButton_Click;

        _closeButton = new Button
        {
            Left = 514,
            Top = 206,
            Width = 90,
            Height = 28,
            Text = "Cancel"
        };
        _closeButton.Click += CloseButton_Click;

        Controls.Add(titleLabel);
        Controls.Add(pathLabel);
        Controls.Add(_gameFolderTextBox);
        Controls.Add(_browseButton);
        Controls.Add(_latestVersionLabel);
        Controls.Add(_progressBar);
        Controls.Add(_statusLabel);
        Controls.Add(_installButton);
        Controls.Add(_closeButton);

        AcceptButton = _installButton;
        CancelButton = _closeButton;
        Shown += async (_, _) => await LoadLatestVersionAsync();
    }

    private async Task LoadLatestVersionAsync()
    {
        try
        {
            _latestPackage = await ReleaseManifestClient.GetLatestPackageAsync(CancellationToken.None);
            _latestVersionLabel.Text = "Latest Version: " + _latestPackage.Version;
            RefreshInstallButtonState();
        }
        catch (Exception ex)
        {
            _latestVersionLabel.Text = "Latest Version: Could not be checked";
            _statusLabel.Text = "Could not check the latest version right now.";
            MessageBox.Show(
                this,
                "The installer could not check the latest version right now." + Environment.NewLine + Environment.NewLine + ex.Message,
                "Release Check Failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        using OpenFileDialog dialog = new OpenFileDialog
        {
            Title = "Select Subnautica.exe",
            Filter = "Subnautica.exe|Subnautica.exe",
            CheckFileExists = true,
            Multiselect = false,
            FileName = "Subnautica.exe"
        };

        string initialDirectory = !string.IsNullOrWhiteSpace(_selectedGameRoot) && Directory.Exists(_selectedGameRoot)
            ? _selectedGameRoot
            : TryResolveDefaultGameRoot();
        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        string folderPath = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
        SetSelectedFolder(folderPath, notifyOnInvalid: true);
    }

    private async void InstallButton_Click(object? sender, EventArgs e)
    {
        if (_installInProgress || !EnsureValidSelection(notifyOnInvalid: true))
        {
            return;
        }

        try
        {
            _installFinished = false;
            SetInstallInProgress(true);
            _progressBar.Value = 0;
            _statusLabel.Text = "Starting install...";

            Progress<InstallProgress> progress = new Progress<InstallProgress>(UpdateProgress);
            ReleasePackageInfo package = await ModInstallService.InstallLatestAsync(_selectedGameRoot, progress, CancellationToken.None);
            _latestPackage = package;

            _progressBar.Value = 100;
            _statusLabel.Text = "Install finished successfully.";
            _installFinished = true;
            _closeButton.Text = "Close";

            MessageBox.Show(
                this,
                "Installed " + package.Version + " successfully." + Environment.NewLine + Environment.NewLine +
                "You can now launch the game normally with Subnautica.exe.",
                "Install Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Install failed.";
            _installFinished = true;
            _closeButton.Text = "Close";

            MessageBox.Show(
                this,
                "The installer could not complete the install." + Environment.NewLine + Environment.NewLine + ex.Message,
                "Install Failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            SetInstallInProgress(false);
        }
    }

    private void CloseButton_Click(object? sender, EventArgs e)
    {
        if (_installInProgress)
        {
            return;
        }

        Close();
    }

    private void SetSelectedFolder(string folderPath, bool notifyOnInvalid)
    {
        _selectedGameRoot = NormalizeGameFolder(folderPath);
        _gameFolderTextBox.Text = _selectedGameRoot;
        EnsureValidSelection(notifyOnInvalid);
    }

    private bool EnsureValidSelection(bool notifyOnInvalid)
    {
        if (_installInProgress)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_selectedGameRoot))
        {
            _lastValidationReport = null;
            _statusLabel.Text = "Choose your Subnautica install folder.";
            RefreshInstallButtonState();
            return false;
        }

        _lastValidationReport = GameInstallValidator.Validate(_selectedGameRoot);
        if (_lastValidationReport.IsValid)
        {
            _statusLabel.Text = _latestPackage is null
                ? "Valid Subnautica install selected."
                : "Ready to install " + _latestPackage.Version + ".";
            RefreshInstallButtonState();
            return true;
        }

        _statusLabel.Text = "That folder is not the supported game version.";
        RefreshInstallButtonState();

        if (notifyOnInvalid)
        {
            MessageBox.Show(
                this,
                _lastValidationReport.ToUserFacingMessage(),
                "Unsupported Game Version",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        return false;
    }

    private void SetInstallInProgress(bool value)
    {
        _installInProgress = value;
        _browseButton.Enabled = !value;
        _closeButton.Enabled = !value;
        RefreshInstallButtonState();
    }

    private void RefreshInstallButtonState()
    {
        _installButton.Enabled = !_installInProgress &&
                                 !_installFinished &&
                                 _latestPackage is not null &&
                                 _lastValidationReport is not null &&
                                 _lastValidationReport.IsValid;
    }

    private void UpdateProgress(InstallProgress progress)
    {
        _progressBar.Value = Math.Max(_progressBar.Minimum, Math.Min(_progressBar.Maximum, progress.Percent));
        _statusLabel.Text = progress.Message;
    }

    private static string NormalizeGameFolder(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(rawValue.Trim());
        }
        catch
        {
            return rawValue.Trim();
        }
    }

    private static string TryResolveDefaultGameRoot()
    {
        string[] candidates =
        {
            @"C:\Program Files (x86)\Steam\steamapps\common\Subnautica",
            @"C:\Program Files (x86)\Steam\steamapps\common\Subnautica2018"
        };

        foreach (string candidate in candidates)
        {
            if (File.Exists(Path.Combine(candidate, "Subnautica.exe")))
            {
                return candidate;
            }
        }

        return string.Empty;
    }
}
