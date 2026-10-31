using BetterOtherRoles.Utilities;
using HarmonyLib;
using MiraAPI.Hud;
using Rewired;
using Rewired.UI.ControlMapper;
using TMPro;
using UnityEngine;

namespace BetterOtherRoles.Patches.Keybinds;

/// <summary>
///     Shows keyboard keys in the player's keyboard layout (see <see cref="BorKeyNames"/>):
///     in Remap Controls (Rewired LanguageData) and on the button keybind icons (MiraAPI writes them every frame).
/// </summary>
[HarmonyPatch]
public static class KeyNamePatches
{
    // MiraAPI.Patches.HudManagerPatches.vanillaKeybindIcons: vanilla button icons, with their Rewired action id.
    private static readonly AccessTools.FieldRef<Dictionary<TextMeshPro, int>>? VanillaKeybindIcons = FindVanillaIcons();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(LanguageData), nameof(LanguageData.GetElementIdentifierName), typeof(ActionElementMap))]
    public static void ElementMapNamePostfix(ActionElementMap actionElementMap, ref string __result)
    {
        if (actionElementMap?.controllerMap?.controllerType == ControllerType.Keyboard)
        {
            __result = BorKeyNames.Localize(__result, actionElementMap.keyCode.ToString());
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(LanguageData), nameof(LanguageData.GetElementIdentifierName), typeof(KeyCode), typeof(ModifierKeyFlags))]
    public static void KeyCodeNamePostfix(KeyCode keyCode, ref string __result)
    {
        __result = BorKeyNames.Localize(__result, keyCode.ToString());
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyAfter("mira.api")]
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    public static void HudUpdatePostfix()
    {
        if (ActiveInputManager.currentControlType != ActiveInputManager.InputType.Keyboard)
        {
            return;
        }

        if (VanillaKeybindIcons != null)
        {
            foreach (var (text, actionId) in VanillaKeybindIcons())
            {
                if (text != null)
                {
                    text.text = BorKeyNames.Get(MiraAPI.Keybinds.KeybindUtils.GetKeycodeByActionId(actionId).ToString());
                }
            }
        }

        foreach (var button in CustomButtonManager.Buttons)
        {
            if (button.Keybind is { CurrentKey: not KeyboardKeyCode.None } keybind && button.KeybindIcon != null && button.KeybindIcon.transform.childCount > 0)
            {
                var text = button.KeybindIcon.transform.GetChild(0).GetComponent<TextMeshPro>();
                if (text != null)
                {
                    text.text = BorKeyNames.Get(keybind.CurrentKey.ToString());
                }
            }
        }
    }

    private static AccessTools.FieldRef<Dictionary<TextMeshPro, int>>? FindVanillaIcons()
    {
        var field = AccessTools.Field(typeof(MiraAPI.Patches.HudManagerPatches), "vanillaKeybindIcons");
        return field != null ? AccessTools.StaticFieldRefAccess<Dictionary<TextMeshPro, int>>(field) : null;
    }
}
