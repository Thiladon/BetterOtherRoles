using BetterOtherRoles.Modules.Lobby;
using MiraAPI.Translation;

namespace BetterOtherRoles.Modules.Commands;

/// <summary>
///     /rules shows the rules of the lobby (<see cref="BorLobbyRules"/>): the host its own, another player asks the host;
///     /rules show sends them to everyone (host only).
/// </summary>
public sealed class RulesCommand : BorCommand
{
    private const string ShowArgument = "show";

    public override string Name => "rules";

    // Only the host can send them to everyone: the other players see "/rules" alone.
    public override string Usage => AmongUsClient.Instance.AmHost ? base.Usage : string.Empty;

    public override string Description => AmongUsClient.Instance.AmHost
        ? base.Description
        : MiraLocaleManager.Get("BetterOtherRoles.Command.Rules.DescriptionPlayer", "shows the rules of the lobby.");

    public override void Run(string args)
    {
        if (args.Length == 0)
        {
            BorLobbyRules.ShowOwn();
            return;
        }

        if (!string.Equals(args, ShowArgument, StringComparison.OrdinalIgnoreCase))
        {
            ReplyUsage();
            return;
        }

        if (!AmongUsClient.Instance.AmHost)
        {
            BorCommandUtils.Reply(MiraLocaleManager.Get("BetterOtherRoles.Command.HostOnly", "Host only command."));
            return;
        }

        BorLobbyRules.ShowToAll();
    }
}
