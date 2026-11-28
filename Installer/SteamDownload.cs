using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace BorInstaller;

/// <summary>
///     Download of a given version of Among Us by the Steam client of the player (already logged in): the console
///     command "download_depot 945360 &lt;depot&gt; &lt;manifest&gt;" (manifests listed on SteamDB) puts the files in
///     Steam/steamapps/content/app_945360/depot_&lt;depot&gt;, without touching the installed game. There is no official way
///     to run a console command: the installer copies it to the clipboard and opens the console, the player pastes
///     it, then the installer waits for the game to be there, at the right version, its files no longer growing.
/// </summary>
internal static class SteamDownload
{
    /// <summary>
    ///     Steam id of Among Us.
    /// </summary>
    public const string AppId = "945360";

    // Links of the Steam client (protocol steam://).
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S1075:URIs should not be hardcoded", Justification = "Link of the Steam client.")]
    private const string ConsoleLink = "steam://open/console";

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S1075:URIs should not be hardcoded", Justification = "Link of the Steam client.")]
    private const string ValidateLink = "steam://validate/";

    private static readonly TimeSpan Poll = TimeSpan.FromSeconds(3);

    /// <summary>
    ///     Gets the Steam folder, or null when Steam is not installed.
    /// </summary>
    public static string? SteamPath
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            return key?.GetValue("SteamPath") is string path ? Path.GetFullPath(path) : null;
        }
    }

    /// <summary>
    ///     Gets the console command that downloads a depot.
    /// </summary>
    /// <param name="depot">The depot.</param>
    /// <returns>The command.</returns>
    public static string Command(SteamDepot depot) => $"download_depot {AppId} {depot.Depot} {depot.Manifest}";

    /// <summary>
    ///     Gets the folder where Steam puts a downloaded depot.
    /// </summary>
    /// <param name="steamPath">The Steam folder.</param>
    /// <param name="depot">The depot.</param>
    /// <returns>The folder.</returns>
    public static string ContentFolder(string steamPath, SteamDepot depot) =>
        Path.Combine(steamPath, "steamapps", "content", "app_" + AppId, "depot_" + depot.Depot);

    /// <summary>
    ///     Opens the Steam console.
    /// </summary>
    public static void OpenConsole()
    {
        using var process = Process.Start(new ProcessStartInfo(ConsoleLink) { UseShellExecute = true });
    }

    /// <summary>
    ///     Asks Steam to check the files of the installed game, which updates it when it is late.
    /// </summary>
    public static void Validate()
    {
        using var process = Process.Start(new ProcessStartInfo(ValidateLink + AppId) { UseShellExecute = true });
    }

    /// <summary>
    ///     Waits for the game to be downloaded in a folder, at a version: the version read and the size of the files the
    ///     same for two checks in a row.
    /// </summary>
    /// <param name="folder">The folder.</param>
    /// <param name="version">The version.</param>
    /// <param name="cancellation">Stops waiting.</param>
    /// <returns>The task.</returns>
    public static async Task WaitAsync(string folder, string version, CancellationToken cancellation)
    {
        long? lastSize = null;
        while (true)
        {
            await Task.Delay(Poll, cancellation).ConfigureAwait(false);
            if (!GameLocator.IsGameFolder(folder) || GameVersion.Read(folder) != version)
            {
                lastSize = null;
                continue;
            }

            var size = FolderSize(folder);
            if (size == lastSize) return;

            lastSize = size;
        }
    }

    private static long FolderSize(string folder)
    {
        try
        {
            return Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Sum(x => new FileInfo(x).Length);
        }
        catch (IOException)
        {
            return -1;
        }
    }
}
