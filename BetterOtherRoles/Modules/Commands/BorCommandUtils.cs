using System.Globalization;
using System.Text;
using BetterOtherRoles.Utilities;

namespace BetterOtherRoles.Modules.Commands;

/// <summary>
///     Tools shared by the chat commands: answers, player names, text comparison.
/// </summary>
public static class BorCommandUtils
{
    /// <summary>
    ///     Answers the player who typed the command, with a message of the mod ("[BOR] : ...").
    /// </summary>
    /// <param name="message">The message, already translated.</param>
    public static void Reply(string message)
    {
        BorChat.AddSystemMessage(message);
    }

    /// <summary>
    ///     Gets the players still in the game, the longest names first (a name may start with another one).
    /// </summary>
    /// <returns>The players.</returns>
    public static IEnumerable<PlayerControl> GetPlayers()
    {
        return PlayerControl.AllPlayerControls.ToArray()
            .Where(x => x.Data != null && !x.Data.Disconnected)
            .OrderByDescending(x => x.Data.PlayerName.Length);
    }

    /// <summary>
    ///     Gets the player with this exact name (any case), or the local player without a name.
    /// </summary>
    /// <param name="args">The arguments of the command: a player name, or empty.</param>
    /// <returns>The player, or null if no player has this name.</returns>
    public static PlayerControl? FindPlayerOrSelf(string args)
    {
        return args.Length == 0
            ? PlayerControl.LocalPlayer
            : GetPlayers().FirstOrDefault(x => string.Equals(x.Data.PlayerName, args, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Reads a player name at the start of the arguments ("[player] rest"). Names may contain spaces: the longest
    ///     name matching the start wins. Without a player name, gives the local player and all the arguments.
    /// </summary>
    /// <param name="args">The arguments of the command.</param>
    /// <param name="rest">The arguments after the player name.</param>
    /// <returns>The player named, or the local player.</returns>
    public static PlayerControl TakePlayerOrSelf(string args, out string rest)
    {
        var player = GetPlayers()
            .FirstOrDefault(x => args.StartsWith(x.Data.PlayerName + " ", StringComparison.OrdinalIgnoreCase));
        rest = player ? args[(player!.Data.PlayerName.Length + 1)..].Trim() : args;
        return player ? player! : PlayerControl.LocalPlayer;
    }

    /// <summary>
    ///     Lower case, without spaces nor accents ("Shérif" == "sherif").
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text to compare.</returns>
    public static string Normalize(string text)
    {
        var builder = new StringBuilder();
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (!char.IsWhiteSpace(c) && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }

    /// <summary>
    ///     A whole number written without culture ("10").
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    public static string ToText(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
