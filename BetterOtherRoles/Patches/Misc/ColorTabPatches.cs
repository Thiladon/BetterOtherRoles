using HarmonyLib;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Colors tab of the customization menu: the colors of the game and of BetterOtherRoles
///     (<see cref="Modules.Colors.BorPlayerColors"/>) sorted by hue, 7 per row (BetterOtherRoles 1.7.1 CustomColors
///     ORDER), instead of the color id order of the game, 4 per row. After MiraAPI, which puts the chips in a scroller.
///     A color that is not in the order (added by another mod) comes after the others.
/// </summary>
[HarmonyPatch]
public static class ColorTabPatches
{
    private const int Columns = 7;

    // Space between two columns, from the space between two rows.
    private const float ColumnGap = 1.05f;

    // 10 pixels at 600 pixels high (6 world units on screen).
    private const float TitleRaise = 0.1f;

    private const string RaisedMark = " (BOR)";

    // Color ids, row after row: the order of BetterOtherRoles 1.7.1, each color of TOU-Mira 1.7.9 (42 to 68) right
    // after the closest one.
    private static readonly int[] Order =
    [
        7, 57, 37, 14, 5, 33, 51,
        41, 25, 44, 64, 4, 30, 52,
        0, 35, 55, 3, 27, 63, 17,
        13, 23, 65, 8, 56, 32, 59,
        38, 1, 43, 21, 40, 53, 61,
        31, 67, 10, 46, 54, 34, 49,
        22, 68, 28, 36, 2, 60, 11,
        26, 66, 29, 47, 20, 19, 50,
        18, 45, 12, 48, 9, 24, 62,
        16, 15, 58, 6, 42, 39,
    ];

    [HarmonyPatch(typeof(PlayerTab), nameof(PlayerTab.OnEnable))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyAfter(MiraAPI.MiraApiPlugin.Id)]
    public static void OnEnablePostfix(PlayerTab __instance)
    {
        var chips = __instance.ColorChips;
        if (chips.Count < 5) return;

        // The game grid, in world space (the chips are in the scroller of MiraAPI): first row from chip 0 to chip 3,
        // next row at chip 4. Smaller chips and rows; the columns a little further apart than the rows (room for the
        // selection outline), from the first column of the game (the left edge of the screen is close), wider to the
        // right.
        var first = chips[0].transform.position;
        var rowStep = chips[4].transform.position.y - first.y;
        var scale = 4f / Columns * 1.3f;
        var columnStep = Mathf.Abs(rowStep) * scale * ColumnGap;
        var left = first.x;

        var slots = Order.Where(x => x < chips.Count).Concat(Enumerable.Range(0, chips.Count).Where(x => !Order.Contains(x))).ToList();
        for (var slot = 0; slot < slots.Count; slot++)
        {
            var chip = chips[slots[slot]];
            var row = slot / Columns;
            var column = slot % Columns;
            var position = chip.transform.position;
            chip.transform.position = new Vector3(left + (columnStep * column),first.y + (rowStep * row * scale), position.z);
            chip.transform.localScale *= scale;

            // MiraAPI only masks the chip background: the color, the selection outline and the marks too.
            foreach (var renderer in chip.GetComponentsInChildren<SpriteRenderer>(true))
                renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }

        RaiseTitle(__instance, chips);
        if (!__instance.scroller) return;

        // Rows shown down to the bottom of the screen (the game counts 3 rows of its size): no scrolling while all the
        // colors fit; otherwise half a row more, out of the shade at the bottom of the screen.
        var rowHeight = Mathf.Abs(rowStep) * scale;
        var camera = Camera.main;
        var bottom = camera ? camera!.ViewportToWorldPoint(Vector3.zero).y : first.y - (rowHeight * __instance.NumRowsVisible);
        var visibleRows = Mathf.Max(1, Mathf.FloorToInt((first.y - bottom) / rowHeight));
        var rows = Mathf.CeilToInt(chips.Count / (float)Columns);
        var spacing = __instance.YOffset * scale;
        var scroller = __instance.scroller;
        scroller.SetBoundsMax(rows > visibleRows ? (rows - visibleRows + 0.5f) * spacing : 0f, scroller.ContentXBounds.max);
    }

    // The "Colors" title raised by 10 pixels (at 600 pixels high), once: the tab is enabled again each time it is shown.
    private static void RaiseTitle(PlayerTab tab, Il2CppSystem.Collections.Generic.List<ColorChip> chips)
    {
        foreach (var text in tab.GetComponentsInChildren<TMPro.TMP_Text>(true))
        {
            if (text.name.EndsWith(RaisedMark, StringComparison.Ordinal) || IsInChip(text.transform, chips)) continue;

            text.transform.localPosition += new Vector3(0f, TitleRaise / text.transform.parent.lossyScale.y, 0f);
            text.name += RaisedMark;
            return;
        }
    }

    private static bool IsInChip(Transform transform, Il2CppSystem.Collections.Generic.List<ColorChip> chips)
    {
        foreach (var chip in chips)
        {
            if (transform.IsChildOf(chip.transform)) return true;
        }

        return false;
    }
}
