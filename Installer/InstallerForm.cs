using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BorInstaller;

/// <summary>
///     Window of the installer: game folder (found on Steam or Epic, or chosen), version (betas on demand), install
///     button, progress. In update mode (started by the mod), everything is given: it waits for the game to close,
///     installs, starts the game again and closes.
/// </summary>
internal sealed class InstallerForm : Form
{
    private readonly InstallerOptions _options;
    private readonly TextBox _folder = new() { Width = 360 };
    private readonly Button _browse = new() { Text = Texts.Browse, Width = 100 };
    private readonly ComboBox _version = new() { Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _betas = new() { Text = Texts.Betas, AutoSize = true };
    private readonly Button _install = new() { Text = Texts.Install, Width = 120, Height = 32, Enabled = false };
    private readonly ProgressBar _progress = new() { Width = 470, Height = 18 };
    private readonly Label _status = new() { Width = 470, Height = 36, Text = Texts.LoadingReleases };
    private List<GithubRelease> _releases = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="InstallerForm"/> class.
    /// </summary>
    /// <param name="options">The arguments.</param>
    public InstallerForm(InstallerOptions options)
    {
        _options = options;
        Text = Texts.Title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(500, 210);
        Font = new Font("Segoe UI", 9f);

        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        layout.Controls.Add(new Label { Text = Texts.GameFolder, AutoSize = true });
        layout.Controls.Add(Row(_folder, _browse));
        layout.Controls.Add(Row(new Label { Text = Texts.Version, AutoSize = true, Margin = new Padding(0, 6, 6, 0) }, _version, _betas));
        layout.Controls.Add(Row(_install));
        layout.Controls.Add(_progress);
        layout.Controls.Add(_status);
        Controls.Add(layout);

        _browse.Click += (_, _) => BrowseFolder();
        _betas.CheckedChanged += (_, _) => FillVersions();
        _install.Click += async (_, _) => await InstallAsync().ConfigureAwait(true);
        Shown += async (_, _) => await LoadAsync().ConfigureAwait(true);

        _folder.Text = options.GamePath ?? GameLocator.Find().FirstOrDefault() ?? string.Empty;
        if (options.Update)
        {
            _folder.Enabled = _browse.Enabled = _version.Enabled = _betas.Enabled = false;
            _betas.Checked = true;
        }
    }

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 2, 0, 6) };
        row.Controls.AddRange(controls);
        return row;
    }

    private async Task LoadAsync()
    {
        try
        {
            _releases = await ReleaseSource.GetReleasesAsync().ConfigureAwait(true);
        }
        catch (Exception e) when (e is System.Net.WebException or InvalidOperationException or System.Runtime.Serialization.SerializationException)
        {
            ShowError(e.Message);
            return;
        }

        FillVersions();
        if (_version.Items.Count == 0)
        {
            _status.Text = Texts.NoRelease;
            return;
        }

        _status.Text = Texts.Ready;
        _install.Enabled = true;
        if (_options.Update)
            await InstallAsync().ConfigureAwait(true);
    }

    // The versions, newest first; betas only when asked. The release given in the arguments selected.
    private void FillVersions()
    {
        var selected = (_version.SelectedItem as GithubRelease)?.Tag ?? _options.ReleaseTag;
        _version.Items.Clear();
        foreach (var release in _releases.Where(x => _betas.Checked || !x.Prerelease || x.Tag == _options.ReleaseTag))
            _version.Items.Add(release);

        var index = _version.Items.Cast<GithubRelease>().ToList().FindIndex(x => x.Tag == selected);
        if (_version.Items.Count > 0)
            _version.SelectedIndex = Math.Max(0, index);
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = Texts.BrowseDescription, SelectedPath = _folder.Text };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _folder.Text = dialog.SelectedPath;
    }

    private async Task InstallAsync()
    {
        var gamePath = _folder.Text.Trim();
        if (!GameLocator.IsGameFolder(gamePath))
        {
            ShowError(Texts.NotGameFolder);
            return;
        }

        if (_version.SelectedItem is not GithubRelease release) return;

        _install.Enabled = false;
        if (_options.WaitPid != null)
            await WaitForGameAsync(_options.WaitPid.Value).ConfigureAwait(true);
        else if (Process.GetProcessesByName("Among Us").Length > 0)
        {
            ShowError(Texts.GameRunning);
            _install.Enabled = true;
            return;
        }

        var progress = new Progress<InstallStep>(step =>
        {
            _status.ForeColor = SystemColors.ControlText;
            _status.Text = step.Text;
            _progress.Value = Math.Max(0, Math.Min(100, step.Percent));
        });

        try
        {
            await new ModInstaller(gamePath, progress).InstallAsync(release).ConfigureAwait(true);
        }
        catch (Exception e) when (e is System.Net.WebException or IOException or InvalidOperationException or InvalidDataException or UnauthorizedAccessException or System.Runtime.Serialization.SerializationException)
        {
            ShowError(e.Message);
            _install.Enabled = true;
            return;
        }

        _install.Text = Texts.Close;
        _install.Click += (_, _) => Close();
        _install.Enabled = true;
        if (!_options.Update) return;

        if (_options.Restart)
            Process.Start(new ProcessStartInfo(Path.Combine(gamePath, "Among Us.exe")) { WorkingDirectory = gamePath, UseShellExecute = true });
        await Task.Delay(1500).ConfigureAwait(true);
        Close();
    }

    private async Task WaitForGameAsync(int pid)
    {
        _status.Text = Texts.WaitingForGame;
        try
        {
            using var game = Process.GetProcessById(pid);
            await Task.Run(() => game.WaitForExit(60_000)).ConfigureAwait(true);
        }
        catch (ArgumentException)
        {
            // Already closed.
        }

        // The game files are released a moment after the process ends.
        await Task.Delay(1000).ConfigureAwait(true);
    }

    private void ShowError(string message)
    {
        _status.ForeColor = Color.Firebrick;
        _status.Text = string.Format(Texts.Culture, Texts.Error, message);
    }
}
