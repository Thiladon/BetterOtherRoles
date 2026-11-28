using System.Collections;
using BetterOtherRoles.Utilities;
using MiraAPI.Translation;
using Reactor.Utilities;
using TMPro;

namespace BetterOtherRoles.Modules.Updater;

/// <summary>
///     Windows of the updater (<see cref="BorUpdater"/>), in the style of the game (<see cref="BorPopup"/>):
///     - the question on the main menu when an update is there: "Yes" opens the updater and installs it, "No" closes,
///       and a choice not to offer that update again on the next launches (the player may want to stay on the
///       installed version);
///     - the updater: the installed version and the version of Among Us, the release chosen with arrows (newest first),
///       a button for the betas, the install button (install, reinstall or go back, by the version chosen) and close.
///       Installing starts the installer embedded in the mod (<see cref="BorUpdater.StartInstaller"/>), which closes the
///       game, updates it and starts it again.
/// </summary>
public static class BorUpdaterWindow
{
    private const string ErrorColor = "#C02020";
    private const string UpdateColor = "#B8860B";

    // Places in the frame (7 × 3.5, center 0, 0).
    private const float TextWidth = 6.2f;
    private const float BottomY = -1.15f;
    private const float BottomX = 1.25f;
    private const float BottomWidth = 1.8f;

    private static BorPopup? _popup;
    private static TextMeshPro? _info;
    private static TextMeshPro? _version;
    private static TextMeshPro? _betasText;
    private static PassiveButton? _install;
    private static TextMeshPro? _installText;
    private static List<BorRelease> _releases = [];
    private static int _index;
    private static bool _betas;
    private static bool _busy;
    private static string _status = string.Empty;

    /// <summary>
    ///     Gets a value indicating whether a window of the updater is open.
    /// </summary>
    public static bool IsOpen => _popup is { IsOpen: true };

    /// <summary>
    ///     Asks whether to download an update ("Yes" / "No"), with a choice not to offer it again on the next launches;
    ///     "Yes" opens the updater and installs it.
    /// </summary>
    /// <param name="release">The update.</param>
    public static void Offer(BorRelease release)
    {
        if (IsOpen) return;

        var popup = BorPopup.Create("BorUpdateOffer");
        if (popup == null) return;

        _popup = popup;
        var question = popup.AddText(0.75f, TextWidth, 1.2f, 2.4f);
        question.text = Format("Offer", "A new update is available (v{0}). Do you want to download it?", release.Version);

        var skip = false;
        TextMeshPro? skipText = null;
        skipText = popup.AddButton(SkipLabel(skip), 0f, -0.25f, 4.2f, () =>
        {
            skip = !skip;
            skipText!.text = SkipLabel(skip);
        }).Text;

        popup.AddButton(Get("Yes", "Yes"), -BottomX, BottomY, BottomWidth, () =>
        {
            Save(skip, release);
            popup.Close();
            Coroutines.Start(CoOpenLater(release));
        });
        popup.AddButton(Get("No", "No"), BottomX, BottomY, BottomWidth, () =>
        {
            Save(skip, release);
            popup.Close();
        });
        popup.Show();
    }

    /// <summary>
    ///     Opens the updater (the newest update selected).
    /// </summary>
    public static void Open() => Open(null, false);

    /// <summary>
    ///     Opens the updater on a release, and installs it when asked.
    /// </summary>
    /// <param name="selected">The release selected, or null for the newest update.</param>
    /// <param name="install">Whether to install it at once.</param>
    public static void Open(BorRelease? selected, bool install)
    {
        if (IsOpen) return;

        var popup = BorPopup.Create("BorUpdater");
        if (popup == null) return;

        _popup = popup;
        _info = popup.AddText(1.05f, TextWidth, 0.9f, 2.2f);
        _version = popup.AddText(0.25f, 4f, 0.5f, 2.6f);
        popup.AddButton("<", -2.55f, 0.25f, 0.7f, () => Move(1));
        popup.AddButton(">", 2.55f, 0.25f, 0.7f, () => Move(-1));
        _betasText = popup.AddButton(string.Empty, 0f, -0.45f, 2.4f, ToggleBetas).Text;
        (_install, _installText) = popup.AddButton(string.Empty, -BottomX, BottomY, BottomWidth, OnInstall);
        popup.AddButton(Get("Close", "Close"), BottomX, BottomY, BottomWidth, popup.Close);

        _betas = BorUpdater.ShowBetas || selected is { Github.Prerelease: true };
        _status = string.Empty;
        _busy = false;
        Fill(selected ?? BorUpdater.Update);
        popup.Show();
        Coroutines.Start(CoRefresh(popup));
        if (install)
            OnInstall();
    }

    private static void Save(bool skip, BorRelease release)
    {
        if (skip)
            BorUpdater.SkippedVersion = release.Version;
    }

    private static string SkipLabel(bool skip) => Get(skip ? "SkipOn" : "SkipOff", skip ? "Don't ask me again: yes" : "Don't ask me again: no");

    private static IEnumerator CoOpenLater(BorRelease release)
    {
        // The question closes (animation) before the updater opens.
        while (IsOpen)
            yield return null;
        Open(release, true);
    }

    // While the check runs, the window follows it.
    private static IEnumerator CoRefresh(BorPopup popup)
    {
        while (popup.IsOpen)
        {
            if (BorUpdater.Poll())
                Fill(BorUpdater.Update);
            yield return null;
        }
    }

    // The releases to choose from, the one given selected (else the installed one, else the newest).
    private static void Fill(BorRelease? selected)
    {
        _releases = BorUpdater.GetReleases(_betas);
        _index = selected != null ? _releases.FindIndex(x => x.Github.Tag == selected.Github.Tag) : -1;
        if (_index < 0)
            _index = _releases.FindIndex(x => BorReleases.Compare(x.Version, BorUpdater.InstalledVersion) == 0);
        _index = Math.Max(0, _index);
        Refresh();
    }

    private static void Move(int step)
    {
        if (_busy || _releases.Count == 0) return;

        _index = Math.Clamp(_index + step, 0, _releases.Count - 1);
        _status = string.Empty;
        Refresh();
    }

    private static void ToggleBetas()
    {
        if (_busy) return;

        var selected = _releases.Count > 0 ? _releases[_index] : null;
        _betas = !_betas;
        _status = string.Empty;
        Fill(selected);
    }

    private static void Refresh()
    {
        if (!IsOpen || !_info) return;

        var info = Format("Installed", "Installed: v{0} (Among Us {1})", BorUpdater.InstalledVersion, BorUpdater.GameVersion);
        var state = BorUpdater.State switch
        {
            UpdaterState.Checking => Get("Checking", "Looking for updates…"),
            UpdaterState.Failed => $"<color={ErrorColor}>{Get("Failed", "The updates cannot be read.")}</color>",
            UpdaterState.Checked when _releases.Count == 0 => Get("NoRelease", "No version for this version of Among Us."),
            _ => string.Empty,
        };
        _info!.text = string.Join("\n", new[] { info, _status.Length > 0 ? _status : state }.Where(x => x.Length > 0));
        if (_betasText)
            _betasText!.text = Get(_betas ? "BetasOn" : "BetasOff", _betas ? "Betas: yes" : "Betas: no");

        var release = _releases.Count > 0 ? _releases[_index] : null;
        if (_version)
            _version!.text = release == null ? "—" : ReleaseText(release);
        if (_installText)
            _installText!.text = release != null ? ActionText(release) : string.Empty;
        if (_install)
            _install!.gameObject.SetActive(release != null);
    }

    private static string ReleaseText(BorRelease release)
    {
        var text = "v" + release.Version;
        if (release.Github.Prerelease)
            text += " " + Get("Beta", "(beta)");
        return BorReleases.Compare(release.Version, BorUpdater.InstalledVersion) > 0 ? $"<color={UpdateColor}>{text}</color>" : text;
    }

    private static string ActionText(BorRelease release) => BorReleases.Compare(release.Version, BorUpdater.InstalledVersion) switch
    {
        > 0 => Get("Install", "Install"),
        0 => Get("Reinstall", "Reinstall"),
        _ => Get("Downgrade", "Go back"),
    };

    // The game cannot replace the DLLs it has loaded: the installer does it once the game is closed, then starts it again.
    private static void OnInstall()
    {
        if (_busy || _releases.Count == 0) return;

        _busy = true;
        _status = Get("StartingInstaller", "The installer takes over: the game closes and starts again once updated.");
        Refresh();
        Coroutines.Start(CoStartInstaller(_releases[_index]));
    }

    // One frame for the message to show.
    private static IEnumerator CoStartInstaller(BorRelease release)
    {
        yield return null;
        try
        {
            BorUpdater.StartInstaller(release);
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            Warning($"[Updater] Installer of {release.Github.Tag} cannot start: {e.Message}");
            _busy = false;
            _status = $"<color={ErrorColor}>{Format("InstallFailed", "Install failed: {0}", e.Message)}</color>";
            Refresh();
        }
    }

    private static string Get(string key, string fallback) => MiraLocaleManager.Get("BetterOtherRoles.Updater." + key, fallback);

    private static string Format(string key, string fallback, params object[] values) =>
        string.Format(BetterOtherRolesPlugin.Culture, Get(key, fallback), values);
}
