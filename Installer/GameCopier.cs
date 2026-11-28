using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BorInstaller;

/// <summary>
///     Copies the game from its folder (Steam or Epic) to the folder of the install, without the files of a mod
///     (BepInEx, doorstop, folders of the mods): the original folder is never changed. A file already there with the
///     same size and date is not copied again (a new copy after an update of the game only copies what changed). A
///     copy of the Steam game gets steam_appid.txt, to start outside of the Steam library.
/// </summary>
internal static class GameCopier
{
    private const string SteamAppId = "945360";

    // Files and folders of the game folder that belong to mods (top level).
    private static readonly HashSet<string> Skipped = new(StringComparer.OrdinalIgnoreCase)
    {
        "BepInEx", "dotnet", "doorstop_config.ini", "winhttp.dll", ".doorstop_version", "changelog.txt",
        "CorsacCosmetics", "AmongStat", "BetterOtherRolesResources", "TheOtherHats", "Launch-Instances.ps1",
    };

    /// <summary>
    ///     Copies the game.
    /// </summary>
    /// <param name="source">The original game folder.</param>
    /// <param name="target">The folder of the install.</param>
    /// <param name="progress">The progress.</param>
    public static void Copy(string source, string target, IProgress<InstallStep> progress)
    {
        var root = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar);
        var files = Directory.EnumerateFileSystemEntries(root)
            .Where(x => !Skipped.Contains(Path.GetFileName(x)))
            .SelectMany(x => Directory.Exists(x) ? Directory.EnumerateFiles(x, "*", SearchOption.AllDirectories) : [x])
            .ToList();

        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            var destination = Path.Combine(target, file.Substring(root.Length + 1));
            if (!IsSame(file, destination))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination, true);
                File.SetLastWriteTimeUtc(destination, File.GetLastWriteTimeUtc(file));
            }

            if (i % 20 == 0)
                progress.Report(new InstallStep(Texts.CopyingGame, i * 100 / files.Count));
        }

        var appId = Path.Combine(target, "steam_appid.txt");
        if (IsSteam(root) && !File.Exists(appId))
            File.WriteAllText(appId, SteamAppId);
    }

    /// <summary>
    ///     Gets a value indicating whether two folders are the same.
    /// </summary>
    /// <param name="a">A folder.</param>
    /// <param name="b">The other folder.</param>
    /// <returns>Whether they are the same.</returns>
    public static bool SameFolder(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

    // The default folder of the install: next to the original game.
    public static string DefaultTarget(string source)
    {
        var full = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar);
        return Path.Combine(Path.GetDirectoryName(full) ?? full, Path.GetFileName(full) + " - BOR-MiraAPI");
    }

    private static bool IsSame(string file, string destination)
    {
        if (!File.Exists(destination)) return false;

        var a = new FileInfo(file);
        var b = new FileInfo(destination);
        return a.Length == b.Length && a.LastWriteTimeUtc == b.LastWriteTimeUtc;
    }

    // A game of a Steam library, or downloaded by Steam (steamapps/content).
    private static bool IsSteam(string folder) =>
        folder.IndexOf("steamapps", StringComparison.OrdinalIgnoreCase) >= 0;
}
