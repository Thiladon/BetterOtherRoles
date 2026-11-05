using BetterOtherRoles.Modules.Debug;
using MiraAPI.Translation;

namespace BetterOtherRoles.Modules.Commands.Debug;

/// <summary>
///     /resetcd [player]: makes the buttons of the player (the host by default) ready again: role buttons, kill button,
///     and for an impostor the sabotages and doors.
///     Same action as "Reset cooldowns" in the Debug menu (<see cref="BorDebugActions.RpcResetCooldowns"/>).
/// </summary>
public sealed class ResetCooldownCommand : BorCommand
{
    public override string Name => "resetcd";

    public override CommandAccess Access => CommandAccess.Debug;

    protected override string Key => "ResetCooldown";

    public override void Run(string args)
    {
        var player = BorCommandUtils.FindPlayerOrSelf(args);
        if (!player)
        {
            BorCommandUtils.Reply(MiraLocaleManager.Get("BetterOtherRoles.Command.PlayerNotFound", "Player \"{0}\" not found.")
                .Replace("{0}", args));
            return;
        }

        var target = player!;
        target.RpcResetCooldowns();
        BorCommandUtils.Reply(MiraLocaleManager.Get("BetterOtherRoles.Command.ResetCooldown.Done", "Cooldowns of {0} reset.")
            .Replace("{0}", target.Data.PlayerName));
    }
}
