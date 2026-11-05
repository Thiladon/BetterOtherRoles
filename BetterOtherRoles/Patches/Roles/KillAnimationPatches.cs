using AmongUs.GameOptions;
using HarmonyLib;
using MiraAPI.Roles;

namespace BetterOtherRoles.Patches.Roles;

/// <summary>
///     Kill animation sometimes skipped when the killer has a modded role (MethodAccessException in
///     KillOverlay.ShowKillAnimation: "IEnumerable&lt;OverlayKillAnimation&gt;.GetEnumerator on type String[]").
///     MiraAPI gives every modded role an empty array made by the mod (CustomRoleManager, CustomKillAnimations): the game
///     frees it while the roles still point to it (the field is written by Il2CppInterop, not seen by the incremental GC),
///     and the memory is used again by other arrays (String[], Collider2D[]...). The game picks the animation in it.
///     Before each kill animation, a modded killer gets the array of the vanilla role of its team instead (owned by the
///     game, never freed): the same animations as a vanilla crewmate or impostor.
/// </summary>
[HarmonyPatch(typeof(KillOverlay), nameof(KillOverlay.ShowKillAnimation), typeof(NetworkedPlayerInfo), typeof(NetworkedPlayerInfo))]
public static class KillAnimationPatches
{
    [HarmonyPrefix]
    public static void Prefix([HarmonyArgument(0)] NetworkedPlayerInfo killer)
    {
        var role = killer ? killer.Role : null;
        
        if (!role || role is not ICustomRole || !RoleManager.InstanceExists) return;

        var vanilla = RoleManager.Instance.GetRole(role!.IsImpostor ? RoleTypes.Impostor : RoleTypes.Crewmate);
        if (vanilla)
            role.CustomKillAnimations = vanilla.CustomKillAnimations;
    }
}
