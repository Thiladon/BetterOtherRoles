using HarmonyLib;
using UnityEngine;

namespace BetterOtherRoles.Patches.Options;

/// <summary>
///     Fixes a MiraAPI 0.6.0 bug in the game settings view of the clients (LobbyViewSettingsPane, roles tab):
///     the scroll limit stops at the top of the last row of role panels, without its height.
///     The last options of a long panel (the Sheriff with its Deputy options) can't be reached.
///     The limit is computed again from the lowest element of the tab.
/// </summary>
[HarmonyPatch(typeof(LobbyViewSettingsPane), nameof(LobbyViewSettingsPane.DrawRolesTab))]
public static class LobbyViewRolesScrollPatches
{
    // From the center of the lowest line to the scroll limit, as the Overview tab of MiraAPI
    // (DrawOptions: row spacing 0.85, then 2 less for the visible area).
    private const float BottomMargin = -1.15f;

    [HarmonyPostfix]
    public static void DrawRolesTabPostfix(LobbyViewSettingsPane __instance)
    {
        var container = __instance.settingsContainer;
        var lowestY = float.MaxValue;
        foreach (var info in __instance.settingsInfo)
        {
            if (!info) continue;

            foreach (var child in info.GetComponentsInChildren<Transform>())
            {
                lowestY = Mathf.Min(lowestY, container.InverseTransformPoint(child.position).y);
            }
        }

        if (lowestY == float.MaxValue) return;

        var bound = BottomMargin - lowestY;
        if (bound > __instance.scrollBar.ContentYBounds.max)
            __instance.scrollBar.SetYBoundsMax(bound);
    }
}
