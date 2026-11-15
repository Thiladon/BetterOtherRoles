using BetterOtherRoles.Modules.Shield;
using MiraAPI.Translation;

namespace BetterOtherRoles.Modules.Commands.Lobby;

/// <summary>
///     /shield shows the player shielded at the start of the next game, /shield {player} chooses it (BetterOtherRoles
///     1.7.1 ChatControllerPatches.ShieldCommand). Host only: the host keeps the first players murdered and gives the
///     shield (<see cref="BorFirstKillShield"/>).
/// </summary>
public sealed class ShieldCommand : BorCommand
{
    public override string Name => "shield";

    public override CommandAccess Access => CommandAccess.Host;

    public override CommandPlace Place => CommandPlace.Lobby;

    public override void Run(string args)
    {
        if (args.Length == 0)
        {
            var next = BorFirstKillShield.NextShielded ?? MiraLocaleManager.Get("BetterOtherRoles.Command.Shield.Nobody", "nobody");
            BorCommandUtils.Reply(MiraLocaleManager.Get("BetterOtherRoles.Command.Shield.Current", "Shielded at the start of the next game: {0}.")
                .Replace("{0}", next));
            return;
        }

        var player = BorCommandUtils.FindPlayerOrSelf(args);
        if (!player || player!.Data == null)
        {
            BorCommandUtils.Reply(MiraLocaleManager.Get("BetterOtherRoles.Command.Shield.NotFound", "No player named {0} in the lobby.")
                .Replace("{0}", args));
            return;
        }

        BorFirstKillShield.Choose(player.Data.PlayerName);
        BorCommandUtils.Reply(MiraLocaleManager.Get("BetterOtherRoles.Command.Shield.Set", "{0} will be shielded at the start of the next game.")
            .Replace("{0}", player.Data.PlayerName));
    }
}
