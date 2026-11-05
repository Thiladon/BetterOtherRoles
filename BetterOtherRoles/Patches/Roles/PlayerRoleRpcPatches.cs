using HarmonyLib;

namespace BetterOtherRoles.Patches.Roles;

/// <summary>
///     A role RPC can reach a client before the player's role is set (first game of a client): the game threw a
///     NullReferenceException. It is skipped, as TOU-Mira 1.7.9 does (PlayerRoleRpcPatch).
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRoleRpc))]
public static class PlayerRoleRpcPatches
{
    [HarmonyPrefix]
    public static bool HandleRoleRpcPrefix(PlayerControl __instance, byte callId)
    {
        if (__instance.Data != null && __instance.Data.Role != null) return true;

        Warning($"Role RPC {callId} skipped: no role yet for {(__instance.Data != null ? __instance.Data.PlayerName : "?")}.");
        return false;
    }
}
