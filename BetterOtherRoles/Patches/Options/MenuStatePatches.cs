using HarmonyLib;
using MiraAPI.Patches.Options;
using UnityEngine;

namespace BetterOtherRoles.Patches.Options;

/// <summary>
///     Fixes a MiraAPI 0.6.0 bug: MenuState uses the vanilla RoleSettingsButton as the "Roles" button of each mod
///     and moves it, but does not put it back when returning to the "Main" page. When the mod has no Game options,
///     the button ends up on top of the GameSettingsButton.
/// </summary>
[HarmonyPatch(typeof(MenuState), "UpdateUi")]
public static class MenuStatePatches
{
    private static IntPtr _cachedButton;
    private static Vector3 _rolesButtonPosition;

    [HarmonyPrefix]
    public static void UpdateUiPrefix(MenuState __instance)
    {
        // The first UpdateUi call of a new menu (from MenuState.Awake) is on the "Main" page, before any move.
        var button = __instance.GetComponent<GameSettingMenu>()?.RoleSettingsButton;
        if (button == null || button.Pointer == _cachedButton || __instance.CurrentModIdx != 0) return;

        _cachedButton = button.Pointer;
        _rolesButtonPosition = button.transform.localPosition;
    }

    [HarmonyPostfix]
    public static void UpdateUiPostfix(MenuState __instance)
    {
        var button = __instance.GetComponent<GameSettingMenu>()?.RoleSettingsButton;
        if (button != null && button.Pointer == _cachedButton && __instance.CurrentModIdx == 0)
            button.transform.localPosition = _rolesButtonPosition;
    }
}
