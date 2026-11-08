namespace BetterOtherRoles.Roles;

/// <summary>
///     A role showing its own progress with the name of its player (guesses, charges...), instead of the task counter
///     (TOU-Mira 1.7.9 IProgressTally, <see cref="Patches.Misc.RoleNamePatches"/>). Shown according to the local
///     setting <see cref="LocalSettings.BorLocalSettingsTab.PlayerProgress"/>.
/// </summary>
public interface IBorProgress
{
    /// <summary>
    ///     Gets the progress shown for the player of this role, seen by the local player.
    /// </summary>
    /// <param name="player">The player of the role.</param>
    /// <param name="inMeeting">Whether it is shown in a meeting.</param>
    /// <param name="progress">The progress, rich text allowed ("(2/3)").</param>
    /// <returns>False when nothing is shown (the local player may not see it).</returns>
    bool TryGetProgress(PlayerControl player, bool inMeeting, out string progress);
}
