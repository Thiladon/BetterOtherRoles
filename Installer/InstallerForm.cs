using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BorInstaller;

/// <summary>
///     Window of the installer: original game folder (found on Steam or Epic, or chosen) and its version, install
///     folder (next to it by default), version of the mod with its version of Among Us (betas on demand, or when
///     there is no other version; the newest one made for the game selected), install button, progress.
///     The game is copied to the install folder (<see cref="GameCopier"/>) when it is the version of the mod;
///     otherwise the right version can be downloaded by Steam (<see cref="SteamDownload"/>), then copied. BepInEx and
///     the mod are then installed there; the same folder twice installs in place. In update mode (started by the
///     mod), everything is given: it waits for the game to close, installs in its folder, starts the game again and
///     closes.
/// </summary>
internal sealed class InstallerForm : Form
{
    private const int FolderWidth = 380;

    private readonly InstallerOptions _options;
    private readonly TextBox _source = new() { Width = FolderWidth };
    private readonly Button _browseSource = new() { Text = Texts.Browse, Width = 90 };
    private readonly Label _gameVersion = new() { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 0, 0, 6) };
    private readonly TextBox _target = new() { Width = FolderWidth };
    private readonly Button _browseTarget = new() { Text = Texts.Browse, Width = 90 };
    private readonly ComboBox _version = new() { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _betas = new() { Text = Texts.Betas, AutoSize = true };
    private readonly Button _install = new() { Text = Texts.Install, Width = 120, Height = 32, Enabled = false };
    private readonly Button _steamDownload = new() { Text = Texts.SteamDownload, AutoSize = true, Height = 32, Visible = false };
    private readonly Button _steamUpdate = new() { Text = Texts.SteamUpdate, AutoSize = true, Height = 32, Visible = false };
    private readonly ProgressBar _progress = new() { Width = 480, Height = 18 };
    private readonly Label _status = new() { Width = 480, Height = 52, Text = Texts.LoadingReleases };
    private readonly CancellationTokenSource _closing = new();
    private List<ReleaseItem> _releases = [];

    // The install folder follows the original one until it is changed by hand.
    private bool _targetAuto = true;
    private bool _settingTarget;
    private bool _busy;

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
        ClientSize = new Size(510, 340);
        Font = new Font("Segoe UI", 9f);

        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        layout.Controls.Add(new Label { Text = Texts.GameFolder, AutoSize = true });
        layout.Controls.Add(Row(_source, _browseSource));
        layout.Controls.Add(_gameVersion);
        layout.Controls.Add(new Label { Text = Texts.TargetFolder, AutoSize = true });
        layout.Controls.Add(Row(_target, _browseTarget));
        layout.Controls.Add(Row(new Label { Text = Texts.Version, AutoSize = true, Margin = new Padding(0, 6, 6, 0) }, _version, _betas));
        layout.Controls.Add(Row(_install, _steamDownload, _steamUpdate));
        layout.Controls.Add(_progress);
        layout.Controls.Add(_status);
        Controls.Add(layout);

        _browseSource.Click += (_, _) => Browse(_source, Texts.BrowseDescription);
        _browseTarget.Click += (_, _) => Browse(_target, Texts.TargetDescription);
        _source.TextChanged += (_, _) => OnSourceChanged();
        _target.TextChanged += (_, _) => _targetAuto &= _settingTarget;
        _betas.CheckedChanged += (_, _) => FillVersions();
        _version.SelectedIndexChanged += (_, _) => HideSteamButtons();
        _install.Click += async (_, _) => await OnInstallClickAsync().ConfigureAwait(true);
        _steamDownload.Click += async (_, _) => await DownloadWithSteamAsync().ConfigureAwait(true);
        _steamUpdate.Click += (_, _) => SteamDownload.Validate();
        Shown += async (_, _) => await LoadAsync().ConfigureAwait(true);
        FormClosed += (_, _) => _closing.Cancel();

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

    private string SourceVersion => GameVersion.Read(_source.Text.Trim()) ?? string.Empty;

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _closing.Dispose();
        base.Dispose(disposing);
    }

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 2, 0, 6) };
        row.Controls.AddRange(controls);
        return row;
    }

    private void OnSourceChanged()
    {
        var source = _source.Text.Trim();
        var version = GameLocator.IsGameFolder(source) ? GameVersion.Read(source) : null;
        _gameVersion.Text = version != null ? string.Format(Texts.Culture, Texts.GameVersionOf, version) : Texts.UnknownGameVersion;
        if (_targetAuto && GameLocator.IsGameFolder(source))
            SetTarget(GameCopier.DefaultTarget(source));
        if (_releases.Count > 0)
            FillVersions();
    }

    private void SetTarget(string folder)
    {
        _settingTarget = true;
        _target.Text = folder;
        _settingTarget = false;
    }

    // The releases and their information (version of Among Us, Steam depots), read together.
    private async Task LoadAsync()
    {
        try
        {
            var releases = await ReleaseSource.GetReleasesAsync().ConfigureAwait(true);
            var infos = await Task.WhenAll(releases.Select(ReadInfoAsync)).ConfigureAwait(true);
            _releases = releases.Zip(infos, (release, info) => new ReleaseItem(release, info)).Where(x => x.Info != null).ToList();
        }
        catch (Exception e) when (e is System.Net.WebException or InvalidOperationException or System.Runtime.Serialization.SerializationException)
        {
            ShowError(e.Message);
            return;
        }

        // Only betas so far: shown anyway.
        if (_releases.TrueForAll(x => x.Release.Prerelease))
            _betas.Checked = true;

        FillVersions();
        if (_options.Update && _version.Items.Count > 0)
            await InstallAsync().ConfigureAwait(true);
    }

    private static async Task<ReleaseInfo?> ReadInfoAsync(GithubRelease release)
    {
        try
        {
            return await ReleaseSource.GetInfoAsync(release).ConfigureAwait(false);
        }
        catch (Exception e) when (e is System.Net.WebException or InvalidOperationException or System.Runtime.Serialization.SerializationException)
        {
            return null;
        }
    }

    // The versions, newest first; betas only when asked. Selected: the release given in the arguments, else the newest
    // one made for the original game, else the newest one. The install button follows the list.
    private void FillVersions()
    {
        var gameVersion = SourceVersion;
        _version.Items.Clear();
        foreach (var item in _releases.Where(x => _betas.Checked || !x.Release.Prerelease || x.Release.Tag == _options.ReleaseTag))
            _version.Items.Add(item);

        var items = _version.Items.Cast<ReleaseItem>().ToList();
        var index = items.FindIndex(x => x.Release.Tag == _options.ReleaseTag);
        if (index < 0)
            index = items.FindIndex(x => x.Info!.GameVersion == gameVersion);
        if (_version.Items.Count > 0)
            _version.SelectedIndex = Math.Max(0, index);

        _install.Enabled = _version.Items.Count > 0 && !_busy;
        if (!_busy)
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
        HideSteamButtons();
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

        if (_version.SelectedItem is not ReleaseItem item) return;

        // The original game is copied only when it is the version of the mod.
        var wanted = item.Info!.GameVersion;
        var current = SourceVersion;
        if (wanted.Length > 0 && current != wanted)
        {
            OfferSteam(item, current, wanted, GameCopier.SameFolder(source, target));
            return;
        }

        await RunAsync(item, source, target).ConfigureAwait(true);
    }

    // The game is not the version of the mod: the reason, and the Steam buttons when they can help (not for an
    // install in place, which would change the original game).
    private void OfferSteam(ReleaseItem item, string current, string wanted, bool inPlace)
    {
        var older = current.Length > 0 && GameVersion.Compare(current, wanted) < 0;
        var text = string.Format(Texts.Culture, older ? Texts.GameTooOld : Texts.GameTooNew, current.Length > 0 ? current : "?", wanted);
        var depot = GetDepot(item);
        if (depot == null || inPlace || SteamDownload.SteamPath == null)
            text += " " + Texts.NoSteamDownload;

        ShowError(text);
        _steamDownload.Visible = depot != null && !inPlace && SteamDownload.SteamPath != null;
        _steamUpdate.Visible = older && SteamDownload.SteamPath != null;
    }

    // The depot of the release with the bits of the original game, else the only one (the game can change bits with a
    // version: 2026.8.18 is 32 bits on Steam, 2026.9.29 is 64 bits; BepInEx follows the game downloaded).
    private SteamDepot? GetDepot(ReleaseItem item)
    {
        var depots = item.Info!.SteamDepots.Where(x => x.Depot.Length > 0 && x.Manifest.Length > 0).ToList();
        var game64 = PeArchitecture.Is64Bit(Path.Combine(_source.Text.Trim(), "Among Us.exe"));
        return depots.FirstOrDefault(x => x.X64 == game64) ?? depots.FirstOrDefault();
    }

    private void HideSteamButtons()
    {
        _steamDownload.Visible = false;
        _steamUpdate.Visible = false;
    }

    // The command copied and the Steam console opened; once Steam has downloaded the version, it is installed.
    private async Task DownloadWithSteamAsync()
    {
        if (_version.SelectedItem is not ReleaseItem item || GetDepot(item) is not { } depot) return;

        var steam = SteamDownload.SteamPath;
        if (steam == null)
        {
            ShowError(Texts.SteamNotFound);
            return;
        }

        HideSteamButtons();
        SetBusy(true);
        Clipboard.SetText(SteamDownload.Command(depot));
        SteamDownload.OpenConsole();
        _status.ForeColor = SystemColors.ControlText;
        _status.Text = string.Format(Texts.Culture, Texts.SteamWaiting, item.Info!.GameVersion) + "\n" + SteamDownload.Command(depot);

        var folder = SteamDownload.ContentFolder(steam, depot);
        try
        {
            await SteamDownload.WaitAsync(folder, item.Info.GameVersion, _closing.Token).ConfigureAwait(true);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        SetBusy(false);
        await RunAsync(item, folder, _target.Text.Trim()).ConfigureAwait(true);
    }

    // Copy of the game (when another folder), then BepInEx and the mod.
    private async Task RunAsync(ReleaseItem item, string source, string target)
    {
        SetBusy(true);
        if (_options.WaitPid != null)
        {
            await WaitForGameAsync(_options.WaitPid.Value).ConfigureAwait(true);
        }
        else if (Process.GetProcessesByName("Among Us").Length > 0)
        {
            ShowError(Texts.GameRunning);
            SetBusy(false);
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

            await new ModInstaller(target, progress).InstallAsync(item.Release, item.Info!).ConfigureAwait(true);
        }
        catch (Exception e) when (e is System.Net.WebException or IOException or InvalidOperationException or InvalidDataException or UnauthorizedAccessException or System.Runtime.Serialization.SerializationException)
        {
            ShowError(e.Message);
            SetBusy(false);
            return;
        }

        SetBusy(false);
        _install.Text = Texts.Close;
        if (!_options.Update) return;

        if (_options.Restart)
        {
            using var game = Process.Start(new ProcessStartInfo(Path.Combine(target, "Among Us.exe")) { WorkingDirectory = target, UseShellExecute = true });
        }

        await Task.Delay(1500).ConfigureAwait(true);
        Close();
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _install.Enabled = !busy && _version.Items.Count > 0;
        _source.Enabled = _browseSource.Enabled = _target.Enabled = _browseTarget.Enabled = _version.Enabled = _betas.Enabled = !busy && !_options.Update;
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

    /// <summary>
    ///     A release in the list, with its information (null when it cannot be read: left out).
    /// </summary>
    private sealed class ReleaseItem(GithubRelease release, ReleaseInfo? info)
    {
        public GithubRelease Release { get; } = release;

        public ReleaseInfo? Info { get; } = info;

        public override string ToString() =>
            Info?.GameVersion is { Length: > 0 } game ? string.Format(Texts.Culture, Texts.ReleaseFor, Release.Tag, game) : Release.Tag;
    }
}
