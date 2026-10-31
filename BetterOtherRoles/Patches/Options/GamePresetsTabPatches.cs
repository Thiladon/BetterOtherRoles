using HarmonyLib;
using MiraAPI.Patches.Options;
using UnityEngine;

namespace BetterOtherRoles.Patches.Options;

/// <summary>
///     Fixes a MiraAPI 0.6.0 bug: the preset buttons of a mod are clones of the vanilla GameSettingsButton,
///     placed by a vanilla GridArrange. Their spacing depends on the screen ratio (and on resolution changes while the
///     menu is open): the second column ends up shifted, or on top of the save button.
///     The buttons are placed here in local space, which does not depend on the resolution.
/// </summary>
[HarmonyPatch(typeof(GamePresetsTab), nameof(GamePresetsTab.OnEnable))]
[HarmonyAfter("mira.api")]
public static class GamePresetsTabPatches
{
    // 2 columns like MiraAPI. In the container space, a button is about 2.67 x 0.52: cells leave a small gap around it.
    private const int Columns = 2;
    private const float CellWidth = 2.8f;
    private const float CellHeight = 0.6f;

    [HarmonyPostfix]
    public static void OnEnablePostfix()
    {
        if (!GameSettingMenu.Instance || !MenuState.Instance || MenuState.Instance.CurrentModIdx == 0)
        {
            return;
        }

        var container = MenuState.Instance.CurrentContainer;
        if (!container)
        {
            return;
        }

        foreach (var aspectPosition in container.GetComponentsInChildren<AspectPosition>(true))
        {
            UnityEngine.Object.DestroyImmediate(aspectPosition);
        }

        var grid = container.GetComponent<GridArrange>();
        if (grid)
        {
            grid.enabled = false;
        }

        var index = 0;
        for (var i = 0; i < container.transform.childCount; i++)
        {
            var child = container.transform.GetChild(i);
            if (!child.gameObject.activeSelf)
            {
                continue;
            }

            child.localPosition = new Vector3(index % Columns * CellWidth, -(index / Columns) * CellHeight, child.localPosition.z);
            index++;
        }
    }
}
