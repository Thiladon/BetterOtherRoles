using BetterOtherRoles.Patches.Roles;

namespace BetterOtherRoles.Modules.Roles;

/// <summary>
///     Task counter of a crewmate, "(done/total)", in the yellow of BetterOtherRoles 1.7.1 (PlayerControlPatch,
///     taskInfo). Read from the tasks of the player data, known for every player.
/// </summary>
public static class BorTaskInfo
{
    private const string Color = "#FAD934FF";

    /// <summary>
    ///     Gets the task counter of a player of the crewmates' team with tasks (its role while alive for a dead player).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="text">The counter, colored.</param>
    /// <returns>False for a player without tasks to count (impostor, neutral, no tasks).</returns>
    public static bool TryGet(PlayerControl player, out string text)
    {
        text = string.Empty;
        var role = AliveRolePatches.GetRole(player);
        var tasks = player.Data ? player.Data.Tasks : null;
        if (!role || role!.TeamType != RoleTeamTypes.Crewmate || tasks == null || tasks.Count == 0) return false;

        var done = 0;
        foreach (var task in tasks)
        {
            if (task.Complete)
                done++;
        }

        text = $"<color={Color}>({done}/{tasks.Count})</color>";
        return true;
    }
}
