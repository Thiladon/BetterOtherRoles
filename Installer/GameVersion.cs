using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace BorInstaller;

/// <summary>
///     Version of Among Us in a game folder ("2026.9.29"), read from Among Us_Data/globalgamemanagers, where Unity keeps
///     the version of the application (the version of Unity, "2022.3.44f1", ends with a letter and is skipped).
/// </summary>
internal static class GameVersion
{
    private static readonly Regex Pattern = new(@"(?<![\d.])(20\d{2}\.\d{1,2}\.\d{1,2})(?![\d.]|[a-z])", RegexOptions.None, TimeSpan.FromSeconds(2));

    /// <summary>
    ///     Gets the version of the game in a folder, or null.
    /// </summary>
    /// <param name="gamePath">The game folder.</param>
    /// <returns>The version.</returns>
    public static string? Read(string gamePath)
    {
        var file = Path.Combine(gamePath, "Among Us_Data", "globalgamemanagers");
        if (!File.Exists(file)) return null;

        try
        {
            // Latin-1: one char per byte, the ASCII of the version kept as is.
            var text = Encoding.GetEncoding(28591).GetString(File.ReadAllBytes(file));
            var match = Pattern.Match(text);
            return match.Success ? match.Groups[1].Value : null;
        }
        catch (IOException)
        {
            // Being written (Steam download).
            return null;
        }
    }

    /// <summary>
    ///     Compares two versions of the game.
    /// </summary>
    /// <param name="a">A version.</param>
    /// <param name="b">The other version.</param>
    /// <returns>Below 0 when a is older, 0 when the same, above 0 when newer.</returns>
    public static int Compare(string a, string b) =>
        Version.TryParse(a, out var va) && Version.TryParse(b, out var vb) ? va.CompareTo(vb) : string.CompareOrdinal(a, b);
}
