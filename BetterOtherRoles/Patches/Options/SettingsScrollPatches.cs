using HarmonyLib;
using UnityEngine;

namespace BetterOtherRoles.Patches.Options;

/// <summary>
///     Scrolled height of the vanilla game settings tab with the rows added by BetterOtherRoles (lobby size
///     <see cref="LobbySizeOptionPatches"/>, kill start cooldown <see cref="KillCooldownPatches"/>, sabotages
///     <see cref="SabotageSettingsPatches"/>). MiraAPI and the game set it in several places, without these rows: the
///     height they set is kept, and the room of the rows shown added to it (again when that room changes).
/// </summary>
[HarmonyPatch]
public static class SettingsScrollPatches
{
    // Height last set here, to tell it from a height set by someone else, and the last height set by someone else.
    private static float _lastSet = float.NaN;
    private static float _baseHeight;

    [HarmonyPatch(typeof(GameOptionsMenu), nameof(GameOptionsMenu.Update))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    public static void UpdatePostfix(GameOptionsMenu __instance)
    {
        if (!__instance.scrollBar) return;

        var height = __instance.scrollBar.ContentYBounds.max;
        if (!Mathf.Approximately(height, _lastSet)) _baseHeight = height;

        var room = 0f;
        if (LobbySizeOptionPatches.RowShown) room += LobbySizeOptionPatches.RowHeight;
        if (KillCooldownPatches.RowShown) room += KillCooldownPatches.RowHeight;
        room += SabotageSettingsPatches.Room;

        _lastSet = _baseHeight + room;
        if (!Mathf.Approximately(height, _lastSet)) __instance.scrollBar.SetYBoundsMax(_lastSet);
    }
}
