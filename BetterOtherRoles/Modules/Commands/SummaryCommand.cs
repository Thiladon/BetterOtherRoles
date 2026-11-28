using BetterOtherRoles.LocalSettings;
using BetterOtherRoles.Modules.Summary;
using MiraAPI.LocalSettings;
using MiraAPI.Translation;

namespace BetterOtherRoles.Modules.Commands;

/// <summary>
///     /summary shows the summary of the last game in the chat (TOU-Mira 1.7.9 /summary,
///     <see cref="BorSummaryText.GetChatMessage"/>), in the format of the local settings; /summary {format} in the given
///     one (short, normal or complete, in English, in the game language or by its first letter).
///     In the lobby only: in game, the history is the one of the game being played.
/// </summary>
public sealed class SummaryCommand : BorCommand
{
    public override string Name => "summary";

    public override CommandPlace Place => CommandPlace.Lobby;

    public override void Run(string args)
    {
        var format = LocalSettingsTabSingleton<BorLocalSettingsTab>.Instance.LobbySummaryFormat.Value;
        if (args.Length > 0)
        {
            var asked = ParseFormat(args);
            if (asked == null)
            {
                ReplyUsage();
                return;
            }

            format = asked.Value;
        }

        if (BorGameHistory.Players.Count == 0)
        {
            BorCommandUtils.Reply(MiraLocaleManager.Get("BetterOtherRoles.Command.Summary.None", "No game to sum up yet."));
            return;
        }

        BorCommandUtils.Reply(BorSummaryText.GetChatMessage(format));
    }

    // The English name of the format, its first letter, or its name in the local settings.
    private static SummaryFormat? ParseFormat(string text)
    {
        foreach (var format in Enum.GetValues<SummaryFormat>())
        {
            var english = format.ToString();
            var translated = MiraLocaleManager.Get("BetterOtherRoles.LocalSettings.LobbySummaryFormat." + english, english);
            if (string.Equals(text, english, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, translated, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, english[..1], StringComparison.OrdinalIgnoreCase))
                return format;
        }

        return null;
    }
}
