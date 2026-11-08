using BetterOtherRoles.LocalSettings;
using MiraAPI.LocalSettings;

namespace BetterOtherRoles.Modules.Roles;

/// <summary>
///     What the local player sees once dead, from its local settings (BetterOtherRoles 1.7.1 "Ghosts See Tasks &amp; Other
///     Info", "Ghosts Can See Roles", "Ghosts Can See Votes"; all on by default). False while alive.
/// </summary>
public static class BorGhostInfo
{
    /// <summary>
    ///     Gets whether the local player is dead and sees the task counter of every crewmate.
    /// </summary>
    public static bool SeesTasks => IsDead && Settings.GhostsSeeTasks.Value;

    /// <summary>
    ///     Gets whether the local player is dead and sees the role of every player.
    /// </summary>
    public static bool SeesRoles => IsDead && Settings.GhostsSeeRoles.Value;

    /// <summary>
    ///     Gets whether the local player is dead and sees who voted, even with anonymous votes.
    /// </summary>
    public static bool SeesVotes => IsDead && Settings.GhostsSeeVotes.Value;

    private static bool IsDead => PlayerControl.LocalPlayer && PlayerControl.LocalPlayer.Data && PlayerControl.LocalPlayer.Data.IsDead;

    private static BorLocalSettingsTab Settings => LocalSettingsTabSingleton<BorLocalSettingsTab>.Instance;
}
