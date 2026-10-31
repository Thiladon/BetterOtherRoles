using HarmonyLib;
using MiraAPI.LocalSettings;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Fixes a MiraAPI 0.6.0 bug: LocalSettingsTab.CreateTab computes the scroll limit with
///     (Settings + Buttons) * 0.5 - 5, which ignores category labels and rows of two options.
///     The last options of a tab (e.g. Accessibility in the MiraAPI tab) can't be reached.
///     The limit is computed again from the lowest element of the tab.
/// </summary>
[HarmonyPatch(typeof(LocalSettingsTab), nameof(LocalSettingsTab.CreateTab))]
public static class LocalSettingsTabPatches
{
    // Y of the first element in MiraAPI's layout, visible height of the tab and margin below the last element
    // (positions are element centers: half an element plus some space).
    private const float ContentTop = 1.85f;
    private const float VisibleHeight = 5f;
    private const float BottomMargin = 1f;

    [HarmonyPostfix]
    public static void CreateTabPostfix(GameObject __result)
    {
        var scroller = __result.GetComponentInChildren<Scroller>(true);
        if (scroller == null || scroller.Inner == null || scroller.Inner.childCount == 0)
        {
            return;
        }

        var lowestY = float.MaxValue;
        for (var i = 0; i < scroller.Inner.childCount; i++)
        {
            lowestY = Mathf.Min(lowestY, scroller.Inner.GetChild(i).localPosition.y);
        }

        var contentHeight = ContentTop - lowestY + BottomMargin;
        scroller.SetBounds(new FloatRange(0, Mathf.Max(0f, contentHeight - VisibleHeight)), new FloatRange(0, 0));
    }
}
