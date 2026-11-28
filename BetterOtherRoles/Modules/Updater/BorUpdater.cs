using System.Diagnostics;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BetterOtherRoles.LocalSettings;
using MiraAPI.LocalSettings;
using UnityEngine;

namespace BetterOtherRoles.Modules.Updater;

/// <summary>
///     Updater of the mod (BetterOtherRoles 1.7.1 PluginUpdater and BepInExUpdater): the releases of BOR-MiraAPI made for
///     the running version of Among Us (<see cref="BorReleases"/>), checked once on the main menu. Installing one starts
///     the installer embedded in the mod (Installer/) in update mode, then closes the game: the installer waits for it
///     to close, installs the plugins of the release (and its BepInEx when another one), and starts the game again.
///     Windows does not let the game replace (nor rename) the DLLs it has loaded, MiraAPI and Reactor included.
/// </summary>
public static class BorUpdater
{
    private const string InstallerResource = "BetterOtherRoles.Resources.Installer.BOR-MiraAPI-Installer.exe";

    private static Task<List<BorRelease>>? _check;

    // Update the player chose not to be offered again ("Don't offer it again" of the question).
    private static ConfigEntry<string>? _skippedVersion;

    /// <summary>
    ///     Binds the config of the updater.
    /// </summary>
    /// <param name="config">The config of the mod.</param>
    public static void Initialize(ConfigFile config)
    {
        _skippedVersion = config.Bind("Updater", "SkippedVersion", string.Empty, "Update not offered again on the main menu (still in the updater).");
    }

    /// <summary>
    ///     Gets or sets the update not offered again.
    /// </summary>
    public static string SkippedVersion
    {
        get => _skippedVersion?.Value ?? string.Empty;
        set
        {
            if (_skippedVersion != null)
                _skippedVersion.Value = value;
        }
    }

    /// <summary>
    ///     Gets the update to offer on the main menu: the newest one, unless the player chose not to see it again.
    /// </summary>
    public static BorRelease? UpdateToOffer => Update is { } update && update.Version != SkippedVersion ? update : null;

    /// <summary>
    ///     Gets the state of the check.
    /// </summary>
    public static UpdaterState State { get; private set; } = UpdaterState.NotChecked;

    /// <summary>
    ///     Gets the releases made for the running version of Among Us, newest first.
    /// </summary>
    public static List<BorRelease> Releases { get; private set; } = [];

    /// <summary>
    ///     Gets the running version of Among Us ("2026.9.29").
    /// </summary>
    public static string GameVersion => Application.version;

    /// <summary>
    ///     Gets the installed version of the mod.
    /// </summary>
    public static string InstalledVersion => BetterOtherRolesPlugin.Version;

    /// <summary>
    ///     Gets a value indicating whether the betas are shown: when asked in the local settings, or when the mod
    ///     installed is a beta.
    /// </summary>
    public static bool ShowBetas =>
        LocalSettingsTabSingleton<BorLocalSettingsTab>.Instance.UpdaterBetas.Value || BetterOtherRolesPlugin.IsDevBuild;

    /// <summary>
    ///     Gets the newest release newer than the installed version, or null.
    /// </summary>
    public static BorRelease? Update =>
        GetReleases(ShowBetas).FirstOrDefault(x => BorReleases.Compare(x.Version, InstalledVersion) > 0);

    /// <summary>
    ///     Gets the releases to choose from, newest first.
    /// </summary>
    /// <param name="betas">With the betas.</param>
    /// <returns>The releases.</returns>
    public static List<BorRelease> GetReleases(bool betas) => Releases.Where(x => betas || !x.Github.Prerelease).ToList();

    /// <summary>
    ///     Checks the releases once (on the main menu), unless turned off in the local settings.
    /// </summary>
    public static void Check()
    {
        if (_check != null || !LocalSettingsTabSingleton<BorLocalSettingsTab>.Instance.CheckUpdates.Value) return;

        State = UpdaterState.Checking;
        _check = Task.Run(BorReleases.GetAsync);
    }

    /// <summary>
    ///     Takes the result of the check once it is there (on the main thread).
    /// </summary>
    /// <returns>Whether the check has just ended.</returns>
    public static bool Poll()
    {
        if (State != UpdaterState.Checking || _check is not { IsCompleted: true }) return false;

        if (_check.IsFaulted)
        {
            State = UpdaterState.Failed;
            Warning($"[Updater] Releases cannot be read: {_check.Exception?.GetBaseException().Message}");
            return true;
        }

        Releases = [.. _check.Result.Where(x => x.Info.GameVersion == GameVersion).OrderByDescending(x => x.Version, Comparer<string>.Create(BorReleases.Compare))];
        State = UpdaterState.Checked;
        Info($"[Updater] Among Us {GameVersion}, mod {InstalledVersion}: {Releases.Count} releases of {_check.Result.Count} for this version of the game, update {Update?.Github.Tag ?? "none"}");
        return true;
    }

    /// <summary>
    ///     Starts the installer embedded in the mod in update mode (it waits for the game to close, installs the release
    ///     with its BepInEx, and starts the game again), then closes the game.
    /// </summary>
    /// <param name="release">The release.</param>
    public static void StartInstaller(BorRelease release)
    {
        var path = Path.Combine(Path.GetTempPath(), "BOR-MiraAPI-Installer.exe");
        using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(InstallerResource)
                              ?? throw new InvalidOperationException("The installer is missing from the mod."))
        using (var file = File.Create(path))
        {
            resource.CopyTo(file);
        }

        var args = $"--update --game \"{Paths.GameRootPath}\" --release \"{release.Github.Tag}\" --wait-pid {Environment.ProcessId} --restart";
        Info($"[Updater] Installer started: {args}");

        // Without the variables of Doorstop (the loader of BepInEx): it does not start in a process that has them, and
        // the game started again by the installer would have them, so it would start without the mods.
        var start = new ProcessStartInfo(path, args) { UseShellExecute = false };
        foreach (var key in start.Environment.Keys.Where(x => x.StartsWith("DOORSTOP", StringComparison.OrdinalIgnoreCase)).ToList())
            start.Environment.Remove(key);

        using var process = Process.Start(start);
        Application.Quit();
    }
}

/// <summary>
///     State of the check of the releases.
/// </summary>
public enum UpdaterState
{
    NotChecked,
    Checking,
    Checked,
    Failed,
}
