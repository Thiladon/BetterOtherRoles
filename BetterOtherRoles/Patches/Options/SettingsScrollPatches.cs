using HarmonyLib;
using UnityEngine;

namespace BetterOtherRoles.Patches.Options;

/// <summary>
///     Scrolled height of the vanilla game settings tab with the rows added by BetterOtherRoles (lobby size
///     <see cref="LobbySizeOptionPatches"/>, kill start cooldown <see cref="KillCooldownPatches"/>). MiraAPI and the game
///     set it in several places, without these rows: each time someone else sets it, the room of the rows shown is added
///     once.
/// </summary>
[HarmonyPatch]
public static class SettingsScrollPatches
{
    // Height last set here, to tell it from a height set by someone else.
    private static float _lastSet = float.NaN;

    [HarmonyPatch(typeof(GameOptionsMenu), nameof(GameOptionsMenu.Update))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    public static void UpdatePostfix(GameOptionsMenu __instance)
    {
        if (!__instance.scrollBar) return;

        var height = __instance.scrollBar.ContentYBounds.max;
        if (Mathf.Approximately(height, _lastSet)) return;

        var room = 0f;
        if (LobbySizeOptionPatches.RowShown) room += LobbySizeOptionPatches.RowHeight;
        if (KillCooldownPatches.RowShown) room += KillCooldownPatches.RowHeight;

        _lastSet = height + room;
        if (room > 0f) __instance.scrollBar.SetYBoundsMax(_lastSet);
    }
}
