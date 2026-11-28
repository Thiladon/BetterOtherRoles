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
///     Window of the installer: original game folder (found on Steam or Epic, or chosen), install folder (next to it
///     by default), version (betas on demand, or when there is no other version), install button, progress. The game
///     is copied to the install folder (<see cref="GameCopier"/>), then BepInEx and the mod are installed there; the
///     same folder twice installs in place. In update mode (started by the mod), everything is given: it waits for
///     the game to close, installs in its folder, starts the game again and closes.
/// </summary>
internal sealed class InstallerForm : Form
{
    private const int FolderWidth = 380;

    private readonly InstallerOptions _options;
    private readonly TextBox _source = new() { Width = FolderWidth };
    private readonly Button _browseSource = new() { Text = Texts.Browse, Width = 90 };
    private readonly TextBox _target = new() { Width = FolderWidth };
    private readonly Button _browseTarget = new() { Text = Texts.Browse, Width = 90 };
    private readonly ComboBox _version = new() { Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _betas = new() { Text = Texts.Betas, AutoSize = true };
    private readonly Button _install = new() { Text = Texts.Install, Width = 120, Height = 32, Enabled = false };
    private readonly ProgressBar _progress = new() { Width = 480, Height = 18 };
    private readonly Label _status = new() { Width = 480, Height = 36, Text = Texts.LoadingReleases };
    private List<GithubRelease> _releases = [];

    // The install folder follows the original one until it is changed by hand.
    private bool _targetAuto = true;
    private bool _settingTarget;
    private bool _installing;

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
        ClientSize = new Size(510, 290);
        Font = new Font("Segoe UI", 9f);

        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        layout.Controls.Add(new Label { Text = Texts.GameFolder, AutoSize = true });
        layout.Controls.Add(Row(_source, _browseSource));
        layout.Controls.Add(new Label { Text = Texts.TargetFolder, AutoSize = true });
        layout.Controls.Add(Row(_target, _browseTarget));
        layout.Controls.Add(Row(new Label { Text = Texts.Version, AutoSize = true, Margin = new Padding(0, 6, 6, 0) }, _version, _betas));
        layout.Controls.Add(Row(_install));
        layout.Controls.Add(_progress);
        layout.Controls.Add(_status);
        Controls.Add(layout);

        _browseSource.Click += (_, _) => Browse(_source, Texts.BrowseDescription);
        _browseTarget.Click += (_, _) => Browse(_target, Texts.TargetDescription);
        _source.TextChanged += (_, _) => FollowSource();
        _target.TextChanged += (_, _) => _targetAuto &= _settingTarget;
        _betas.CheckedChanged += (_, _) => FillVersions();
        _install.Click += async (_, _) => await OnInstallClickAsync().ConfigureAwait(true);
        Shown += async (_, _) => await LoadAsync().ConfigureAwait(true);

        if (options.Update)
        {
            // The mod updates its own folder.
            _source.Text = options.GamePath ?? string.Empty;
            SetTarget(_source.Text);
            _source.Enabled = _browseSource.Enabled = _target.Enabled = _browseTarget.Enabled = _version.Enabled = _betas.Enabled = false;
            _betas.Checked = true;
        }
        else
        {
            _source.Text = options.GamePath ?? GameLocator.Find().FirstOrDefault() ?? string.Empty;
        }
    }

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 2, 0, 6) };
        row.Controls.AddRange(controls);
        return row;
    }

    private void FollowSource()
    {
        if (_targetAuto && GameLocator.IsGameFolder(_source.Text.Trim()))
            SetTarget(GameCopier.DefaultTarget(_source.Text.Trim()));
    }

    private void SetTarget(string folder)
    {
        _settingTarget = true;
        _target.Text = folder;
        _settingTarget = false;
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

        // Only betas so far: shown anyway.
        if (_releases.TrueForAll(x => x.Prerelease))
            _betas.Checked = true;

        FillVersions();
        if (_options.Update && _version.Items.Count > 0)
            await InstallAsync().ConfigureAwait(true);
    }

    // The versions, newest first; betas only when asked. The release given in the arguments selected. The install
    // button follows the list.
    private void FillVersions()
    {
        var selected = (_version.SelectedItem as GithubRelease)?.Tag ?? _options.ReleaseTag;
        _version.Items.Clear();
        foreach (var release in _releases.Where(x => _betas.Checked || !x.Prerelease || x.Tag == _options.ReleaseTag))
            _version.Items.Add(release);

        var index = _version.Items.Cast<GithubRelease>().ToList().FindIndex(x => x.Tag == selected);
        if (_version.Items.Count > 0)
            _version.SelectedIndex = Math.Max(0, index);

        _install.Enabled = _version.Items.Count > 0 && !_installing;
        if (!_installing)
        {
            _status.ForeColor = SystemColors.ControlText;
            _status.Text = _version.Items.Count > 0 ? Texts.Ready : Texts.NoRelease;
        }
    }

    private void Browse(TextBox box, string description)
    {
        using var dialog = new FolderBrowserDialog { Description = description, SelectedPath = box.Text };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            box.Text = dialog.SelectedPath;
    }

    // Once installed, the button closes the window.
    private async Task OnInstallClickAsync()
    {
        if (_install.Text == Texts.Close)
            Close();
        else
            await InstallAsync().ConfigureAwait(true);
    }

    private async Task InstallAsync()
    {
        var source = _source.Text.Trim();
        var target = _target.Text.Trim();
        if (!GameLocator.IsGameFolder(source))
        {
            ShowError(Texts.NotGameFolder);
            return;
        }

        if (target.Length == 0)
        {
            ShowError(Texts.NoTarget);
            return;
        }

        if (_version.SelectedItem is not GithubRelease release) return;

        _installing = true;
        _install.Enabled = false;
        if (_options.WaitPid != null)
        {
            await WaitForGameAsync(_options.WaitPid.Value).ConfigureAwait(true);
        }
        else if (Process.GetProcessesByName("Among Us").Length > 0)
        {
            ShowError(Texts.GameRunning);
            _installing = false;
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
            if (!GameCopier.SameFolder(source, target))
                await Task.Run(() => GameCopier.Copy(source, target, progress)).ConfigureAwait(true);

            await new ModInstaller(target, progress).InstallAsync(release).ConfigureAwait(true);
        }
        catch (Exception e) when (e is System.Net.WebException or IOException or InvalidOperationException or InvalidDataException or UnauthorizedAccessException or System.Runtime.Serialization.SerializationException)
        {
            ShowError(e.Message);
            _installing = false;
            _install.Enabled = true;
            return;
        }

        _installing = false;
        _install.Text = Texts.Close;
        _install.Enabled = true;
        if (!_options.Update) return;

        if (_options.Restart)
            Process.Start(new ProcessStartInfo(Path.Combine(target, "Among Us.exe")) { WorkingDirectory = target, UseShellExecute = true });
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
