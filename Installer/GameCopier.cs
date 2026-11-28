using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

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
    /// <param name="cancellation">Stops the copy (between two files).</param>
    public static void Copy(string source, string target, IProgress<InstallStep> progress, CancellationToken cancellation)
    {
        var root = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar);
        var files = Directory.EnumerateFileSystemEntries(root)
            .Where(x => !Skipped.Contains(Path.GetFileName(x)))
            .SelectMany(x => Directory.Exists(x) ? Directory.EnumerateFiles(x, "*", SearchOption.AllDirectories) : [x])
            .ToList();

        // Cancelled: a folder made by this copy is removed; in a folder already there (settings and mods of the player),
        // only the files added by this copy are.
        var newFolder = IsNewFolder(target);
        var added = new List<string>();
        try
        {
            for (var i = 0; i < files.Count; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var file = files[i];
                var destination = Path.Combine(target, file.Substring(root.Length + 1));
                if (!IsSame(file, destination))
                {
                    if (!File.Exists(destination))
                        added.Add(destination);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(file, destination, true);
                    File.SetLastWriteTimeUtc(destination, File.GetLastWriteTimeUtc(file));
                }

                if (i % 20 == 0)
                    progress.Report(new InstallStep(Texts.CopyingGame, i * 100 / files.Count));
            }
        }
        catch (OperationCanceledException)
        {
            Undo(target, newFolder, added);
            throw;
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
        return Path.Combine(Path.GetDirectoryName(full) ?? full, DefaultFolderName);
    }

    /// <summary>
    ///     Name of the folder of an install, made next to the original game or in the folder chosen.
    /// </summary>
    public const string DefaultFolderName = "Among Us - BOR-MiraAPI";

    /// <summary>
    ///     Gets a value indicating whether a folder is missing or empty (an install made from nothing).
    /// </summary>
    /// <param name="folder">The folder.</param>
    /// <returns>Whether it is missing or empty.</returns>
    public static bool IsNewFolder(string folder) => !Directory.Exists(folder) || !Directory.EnumerateFileSystemEntries(folder).Any();

    /// <summary>
    ///     Removes a folder and everything in it, read-only files too (File.Copy keeps that attribute of the game files,
    ///     and Directory.Delete refuses them).
    /// </summary>
    /// <param name="folder">The folder.</param>
    public static void DeleteFolder(string folder)
    {
        if (!Directory.Exists(folder)) return;

        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(folder, true);
    }

    private static void Undo(string target, bool newFolder, List<string> added)
    {
        if (newFolder)
        {
            DeleteFolder(target);
            return;
        }

        foreach (var file in added.Where(File.Exists))
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }
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
