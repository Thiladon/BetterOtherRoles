using BetterOtherRoles.Modules.Chat;
using MiraAPI.Translation;

namespace BetterOtherRoles.Modules.Commands;

/// <summary>
///     /info replays the role information received in the last round that has some (<see cref="BorInfoHistory"/>);
///     /info {round} in that round (TOU-Mira 1.7.9 /info, which replays the current round only).
/// </summary>
public sealed class InfoCommand : BorCommand
{
    public override string Name => "info";

    public override CommandPlace Place => CommandPlace.Game;

    public override void Run(string args)
    {
        int? round = BorInfoHistory.LastRound;
        if (args.Length > 0)
        {
            if (!int.TryParse(args, out var asked) || asked < 1)
            {
                ReplyUsage();
                return;
            }

            round = asked;
        }

        var messages = round == null ? [] : BorInfoHistory.Get(round.Value);
        if (messages.Count == 0)
        {
            BorCommandUtils.Reply(args.Length == 0
                ? MiraLocaleManager.Get("BetterOtherRoles.Command.Info.None", "No role information to replay.")
                : MiraLocaleManager.Get("BetterOtherRoles.Command.Info.NoneInRound", "No role information in round {0}.")
                    .Replace("{0}", BorCommandUtils.ToText(round!.Value)));
            return;
        }

        BorCommandUtils.Reply(MiraLocaleManager.Get("BetterOtherRoles.Command.Info.Round", "Round {0}:")
            .Replace("{0}", BorCommandUtils.ToText(round!.Value)));
        foreach (var message in messages)
        {
            BorCommandUtils.Reply(message);
        }
    }
}
