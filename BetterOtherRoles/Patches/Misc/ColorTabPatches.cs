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

    // Color ids, row after row (BetterOtherRoles 1.7.1).
    private static readonly int[] Order =
    [
        7, 37, 14, 5, 33, 41, 25,
        4, 30, 0, 35, 3, 27, 17,
        13, 23, 8, 32, 38, 1, 21,
        40, 31, 10, 34, 22, 28, 36,
        2, 11, 26, 29, 20, 19, 18,
        12, 9, 24, 16, 15, 6, 39,
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

        if (!__instance.scroller) return;

        // Rows shown down to the bottom of the screen (the game counts 3 rows of its size): no scrolling while all the
        // colors fit.
        var rowHeight = Mathf.Abs(rowStep) * scale;
        var camera = Camera.main;
        var bottom = camera ? camera!.ViewportToWorldPoint(Vector3.zero).y : first.y - (rowHeight * __instance.NumRowsVisible);
        var visibleRows = Mathf.Max(1, Mathf.FloorToInt((first.y - bottom) / rowHeight));
        __instance.scroller.CalculateAndSetYBounds(chips.Count, Columns, visibleRows, __instance.YOffset * scale);
    }
}
