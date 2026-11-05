using BetterOtherRoles.Modules.Debug;
using MiraAPI.Translation;

namespace BetterOtherRoles.Modules.Commands.Debug;

/// <summary>
///     /revive [player]: brings the player (the host by default) back to life where their body lies, with the role they
///     had alive. Same action as "Revive" in the Debug menu (<see cref="BorDebugActions.RpcRevive"/>).
/// </summary>
public sealed class ReviveCommand : BorCommand
{
    public override string Name => "revive";

    public override CommandAccess Access => CommandAccess.Debug;

    public override void Run(string args)
    {
        var player = BorCommandUtils.FindPlayerOrSelf(args);
        if (!player)
        {
            BorCommandUtils.Reply(MiraLocaleManager.Get("BetterOtherRoles.Command.PlayerNotFound", "Player \"{0}\" not found.")
                .Replace("{0}", args));
            return;
        }

        if (!player!.Data.IsDead)
        {
            BorCommandUtils.Reply(MiraLocaleManager.Get("BetterOtherRoles.Command.Revive.NotDead", "{0} is not dead.")
                .Replace("{0}", player.Data.PlayerName));
            return;
        }

        player.RpcRevive();
        BorCommandUtils.Reply(MiraLocaleManager.Get("BetterOtherRoles.Command.Revive.Done", "{0} is alive again.")
            .Replace("{0}", player.Data.PlayerName));
    }
}
