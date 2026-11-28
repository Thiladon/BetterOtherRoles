using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace BorInstaller;

/// <summary>
///     Finds the Among Us folders: Steam (its libraries, from the registry and libraryfolders.vdf) and Epic Games (the
///     manifests of the launcher). A folder counts when it has "Among Us.exe".
/// </summary>
internal static class GameLocator
{
    private const string GameExe = "Among Us.exe";

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Gets the Among Us folders found.
    /// </summary>
    /// <returns>The folders, without duplicates.</returns>
    public static IList<string> Find()
    {
        var folders = new List<string>();
        folders.AddRange(FindSteam());
        folders.AddRange(FindEpic());
        return folders.Where(IsGameFolder).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    ///     Gets a value indicating whether the folder holds the game.
    /// </summary>
    /// <param name="folder">The folder.</param>
    /// <returns>Whether it has the game executable.</returns>
    public static bool IsGameFolder(string? folder) =>
        !string.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder, GameExe));

    private static IEnumerable<string> FindSteam()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        if (key?.GetValue("SteamPath") is not string steam) yield break;

        var libraries = new List<string> { steam };
        var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdf))
        {
            foreach (Match match in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\"", RegexOptions.None, RegexTimeout))
                libraries.Add(match.Groups[1].Value.Replace(@"\\", @"\"));
        }

        foreach (var library in libraries)
            yield return Path.Combine(library, "steamapps", "common", "Among Us");
    }

    private static IEnumerable<string> FindEpic()
    {
        var manifests = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
        if (!Directory.Exists(manifests)) yield break;

        foreach (var file in Directory.EnumerateFiles(manifests, "*.item"))
        {
            var text = File.ReadAllText(file);
            if (!Regex.IsMatch(text, "\"DisplayName\"\\s*:\\s*\"Among Us\"", RegexOptions.None, RegexTimeout)) continue;

            var location = Regex.Match(text, "\"InstallLocation\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.None, RegexTimeout);
            if (location.Success)
                yield return location.Groups[1].Value.Replace(@"\\", @"\");
        }
    }
}
