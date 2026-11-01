using AmongUs.GameOptions;
using HarmonyLib;

namespace BetterOtherRoles.Patches.Roles;

/// <summary>
///     Last role of each player while alive (the game's RoleWhenAlive reads as Crewmate through Il2Cpp),
///     recorded each time a role that is not a ghost role is given.
/// </summary>
[HarmonyPatch(typeof(RoleBehaviour), nameof(RoleBehaviour.Initialize))]
public static class AliveRolePatches
{
    private static readonly Dictionary<byte, RoleTypes> AliveRoles = [];

    /// <summary>
    ///     Gets the last role of a player while alive.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The role, or null if none was recorded.</returns>
    public static RoleTypes? Get(PlayerControl player)
    {
        return AliveRoles.TryGetValue(player.PlayerId, out var role) ? role : null;
    }

    [HarmonyPostfix]
    public static void InitializePostfix(RoleBehaviour __instance, PlayerControl player)
    {
        if (player && !__instance.IsDead)
        {
            AliveRoles[player.PlayerId] = __instance.Role;
        }
    }
}
