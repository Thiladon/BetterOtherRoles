using System.Text;
using BetterOtherRoles.LocalSettings;
using MiraAPI.Translation;
using UnityEngine;

namespace BetterOtherRoles.Modules.Summary;

/// <summary>
///     Text of the summary of the last game (<see cref="BorGameHistory"/>), for the end game screen and the chat
///     (TOU-Mira 1.7.9 EndGamePatches.BuildEndGameSummary): one line per player with the roles held (A &gt; B), the
///     modifiers, the tasks, the victims with their round and how the player died (cause, killer, round), the winners'
///     names in gold; or, in the complete format, a title and a list per player.
/// </summary>
public static class BorSummaryText
{
    // Colors of the end game screen (dark background), and darker ones for the chat (light bubbles).
    private static readonly Palette ScreenColors = new("#EFBF04", "#FAD934FF", "#FF0000FF", "#FFEB04FF", "#8CFFFFFF", "#FF1919FF");
    private static readonly Palette ChatColors = new("#A67C00", "#8A6D00", "#C00000", "#B35900", "#007A8A", "#C00000");

    /// <summary>
    ///     Gets the names of the winners of the last game (kept by the game until the end of the next one).
    /// </summary>
    /// <returns>The names.</returns>
    public static HashSet<string> GetWinners() => [.. EndGameResult.CachedWinners.ToArray().Select(x => x.PlayerName)];

    /// <summary>
    ///     Gets the line of a player in the summary.
    /// </summary>
    /// <param name="record">The player.</param>
    /// <param name="winner">Whether the player won.</param>
    /// <param name="full">With the killer's name.</param>
    /// <returns>The line, with its colors.</returns>
    public static string GetLine(PlayerRecord record, bool winner, bool full) => BuildLine(record, winner, full, ScreenColors);

    private static string BuildLine(PlayerRecord record, bool winner, bool full, Palette colors)
    {
        var line = new StringBuilder(GetName(record, winner, colors));
        var roles = GetRoles(record);
        if (roles.Length > 0)
            line.Append(" - ").Append(roles);

        if (record.Modifiers.Count > 0)
            line.Append(" (").Append(string.Join(", ", record.Modifiers)).Append(')');

        if (record.TasksTotal > 0)
            line.Append(' ').Append(GetTasks(record, colors));

        if (record.Victims.Count > 0)
            line.Append(" | ").Append(GetVictims(record, colors));

        return line.Append(" | ").Append(GetDeath(record, full, colors)).ToString();
    }

    /// <summary>
    ///     Gets the summary of the last game for the chat: the winning team, then the players in the given format, in
    ///     colors made for the light chat bubbles.
    /// </summary>
    /// <param name="format">The format.</param>
    /// <returns>The text, with its colors and sizes.</returns>
    public static string GetChatSummary(SummaryFormat format)
    {
        var winners = GetWinners();
        var text = new StringBuilder($"<size=80%>{GetResult(ChatColors)}</size>\n");
        foreach (var record in BorGameHistory.Players.OrderBy(x => x.PlayerId))
        {
            var winner = winners.Contains(record.PlayerName);
            if (format == SummaryFormat.Complete)
                AppendComplete(text, record, winner, ChatColors);
            else
                text.AppendLine(BuildLine(record, winner, format == SummaryFormat.Normal, ChatColors));
        }

        return text.ToString().TrimEnd('\n');
    }

    /// <summary>
    ///     Gets the message of the summary for the chat: its title, then the summary in black (as the players' messages,
    ///     the chat bubbles are light) and small.
    /// </summary>
    /// <param name="format">The format.</param>
    /// <returns>The message, for <see cref="Utilities.BorChat.AddSystemMessage"/>.</returns>
    public static string GetChatMessage(SummaryFormat format) =>
        $"{Get("ChatTitle", "Summary of the last game:")}\n<color=#000000><size=62%>{GetChatSummary(format)}</size></color>";

    /// <summary>
    ///     Gets a text of the summary, translated.
    /// </summary>
    /// <param name="key">The key, after "BetterOtherRoles.Summary.".</param>
    /// <param name="fallback">The text without translation.</param>
    /// <returns>The text.</returns>
    public static string Get(string key, string fallback) => MiraLocaleManager.Get("BetterOtherRoles.Summary." + key, fallback);

    // A title "name - last role (modifiers)", then a list: roles held when more than one, tasks, victims, death.
    private static void AppendComplete(StringBuilder text, PlayerRecord record, bool winner, Palette colors)
    {
        var title = new StringBuilder(GetName(record, winner, colors));
        var last = record.Roles.LastOrDefault();
        if (last != null)
            title.Append(" - ").Append(GetRole(last));

        if (record.Modifiers.Count > 0)
            title.Append(" (").Append(string.Join(", ", record.Modifiers)).Append(')');

        text.Append("<size=115%>").Append(title).AppendLine("</size>");
        if (record.Roles.Count > 1)
            text.Append("• ").AppendLine(string.Format(BetterOtherRolesPlugin.Culture, Get("Roles", "Roles: {0}"), GetRoles(record)));

        if (record.TasksTotal > 0)
            text.Append("• ").AppendLine(string.Format(BetterOtherRolesPlugin.Culture, Get("Tasks", "Tasks: {0}"), GetTasks(record, colors)));

        if (record.Victims.Count > 0)
            text.Append("• ").AppendLine(GetVictims(record, colors));

        text.Append("• ").AppendLine(GetDeath(record, true, colors));
    }

    private static string GetName(PlayerRecord record, bool winner, Palette colors) =>
        winner ? $"<color={colors.Winner}>{record.PlayerName}</color>" : record.PlayerName;

    private static string GetRole(RoleRecord role) => $"<color=#{ColorUtility.ToHtmlStringRGBA(role.Color)}>{role.Name}</color>";

    private static string GetRoles(PlayerRecord record) => string.Join(" > ", record.Roles.Select(GetRole));

    private static string GetTasks(PlayerRecord record, Palette colors) =>
        string.Format(BetterOtherRolesPlugin.Culture, "<color={0}>({1}/{2})</color>", colors.Task, record.TasksDone, record.TasksTotal);

    private static string GetVictims(PlayerRecord record, Palette colors)
    {
        var victims = string.Join(", ", record.Victims.Select(x => $"{x.Name} ({Round(x.Round)})"));
        return $"<color={colors.Victim}>{string.Format(BetterOtherRolesPlugin.Culture, Get("Victims", "Victims: {0}"), victims)}</color>";
    }

    // Cause, killer (when full) and round of the death.
    private static string GetDeath(PlayerRecord record, bool full, Palette colors)
    {
        var death = new StringBuilder($"<color={colors.Death}>").Append(GetCause(record));
        if (full && record.KillerName != null)
            death.Append(' ').Append(string.Format(BetterOtherRolesPlugin.Culture, Get("KilledBy", "by {0}"), record.KillerName));

        if (record.Death != DeathCause.Alive)
            death.Append(" (").Append(Round(record.DeathRound)).Append(')');

        return death.Append("</color>").ToString();
    }

    private static string GetCause(PlayerRecord record)
    {
        var part = record.KillerRolePart;
        return record.Death switch
        {
            DeathCause.Killed => part == null ? Get("Killed", "Killed") : Get("DiedTo" + part, Get("Killed", "Killed")),
            DeathCause.Suicide => part == null ? Get("Suicide", "Suicide") : Get("Suicide" + part, Get("Suicide", "Suicide")),
            DeathCause.Ejected => Get("Ejected", "Ejected"),
            DeathCause.Disconnected => Get("Disconnected", "Disconnected"),
            _ => Get("Alive", "Alive"),
        };
    }

    // The winning team, from the reason of the end of the game (the neutral roles will add their own).
    private static string GetResult(Palette colors) => EndGameResult.CachedGameOverReason switch
    {
        GameOverReason.CrewmatesByVote or GameOverReason.CrewmatesByTask or GameOverReason.ImpostorDisconnect =>
            $"<color={colors.Crewmates}>{Get("CrewmatesWin", "Crewmates win")}</color>",
        _ => $"<color={colors.Impostors}>{Get("ImpostorsWin", "Impostors win")}</color>",
    };

    private static string Round(int round) => string.Format(BetterOtherRolesPlugin.Culture, Get("Round", "R{0}"), round);

    private sealed record Palette(string Winner, string Task, string Victim, string Death, string Crewmates, string Impostors);
}
