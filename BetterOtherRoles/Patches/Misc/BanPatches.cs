using AmongUs.Data.Player;
using HarmonyLib;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     No vanilla ban for leaving games (TOU-Mira 1.7.9 BanPatch; BetterOtherRoles 1.7.1 patched StatsManager.AmBanned,
///     gone from the game): the game gives a ban point at each game start (-1.5 at the end, -1 at the death) and blocks
///     creating or joining a game 5 minutes per point above 2. In Debug Mode a game never ends, so leaving it is common.
///     - IsBanned is always false: every screen checks it (join, create, local game, main menu);
///     - no point is counted at all: BanPoints is always written as 0, so the mod profile stays clean (the points already
///       saved go back to 0 at the next game start).
/// </summary>
[HarmonyPatch(typeof(PlayerBanData))]
public static class BanPatches
{
    [HarmonyPatch(nameof(PlayerBanData.IsBanned), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool IsBannedPrefix(ref bool __result)
    {
        __result = false;
        return false;
    }

    // Only reached by the calls the game did not inline: BanPoints++ at the game start (AmongUsClient.CoStartGame) is
    // inlined by IL2CPP and still writes 1, put back to 0 before the profile is saved (below).
    [HarmonyPatch(nameof(PlayerBanData.BanPoints), MethodType.Setter)]
    [HarmonyPrefix]
    public static void BanPointsSetterPrefix(ref float value)
    {
        value = 0f;
    }

    /// <summary>
    ///     Before the profile is serialized (virtual method, never inlined): no ban point written to the mod profile.
    /// </summary>
    [HarmonyPatch(typeof(PlayerData), "OnSaveStart")]
    [HarmonyPrefix]
    public static void OnSaveStartPrefix(PlayerData __instance)
    {
        if (__instance.Ban != null && __instance.Ban.BanPoints != 0f)
        {
            Info($"Ban points reset before saving the profile (was {__instance.Ban.BanPoints}).");
            __instance.Ban.BanPoints = 0f;
        }
    }
}
