using BetterOtherRoles.Modules.Debug;

namespace BetterOtherRoles.Modules.Commands.Debug;

/// <summary>
///     /endgame [crewmate|impostor]: ends the game despite the Debug Mode, won by the crewmates by default.
///     Same action as "End: crewmates / impostors win" in the Debug menu (<see cref="BorDebugActions.EndGame"/>).
/// </summary>
public sealed class EndGameCommand : BorCommand
{
    public override string Name => "endgame";

    public override CommandAccess Access => CommandAccess.Debug;

    protected override string Key => "EndGame";

    public override void Run(string args)
    {
        switch (BorCommandUtils.Normalize(args))
        {
            case "" or "crewmate" or "crewmates" or "crew":
                BorDebugActions.EndGame(GameOverReason.CrewmatesByVote);
                break;
            case "impostor" or "impostors" or "imp":
                BorDebugActions.EndGame(GameOverReason.ImpostorsByKill);
                break;
            default:
                ReplyUsage();
                break;
        }
    }
}
