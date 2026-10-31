using HarmonyLib;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     During the meeting and kill animations (KillOverlay), the game hides the settings button but not the chat and role
///     guide buttons: hide them too until the animation is over.
///     They are shrunk to nothing instead of deactivated: the game and MiraAPI show them again on their own, and the
///     TopRight container would rearrange its buttons.
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class KillOverlayHudPatches
{
    private static readonly Dictionary<IntPtr, (Transform Button, Vector3 Scale)> HiddenButtons = [];

    /// <summary>
    ///     Gets a value indicating whether a meeting or kill animation is shown.
    /// </summary>
    public static bool IsOverlayOpen { get; private set; }

    [HarmonyPrefix]
    public static void UpdatePrefix(HudManager __instance)
    {
        IsOverlayOpen = __instance.KillOverlay && __instance.KillOverlay.IsOpen;
        if (IsOverlayOpen)
        {
            Hide(__instance.Chat ? __instance.Chat.chatButton.transform : null);
            Hide(__instance.MatchInfoButton ? __instance.MatchInfoButton.transform : null);
            return;
        }

        foreach (var (button, scale) in HiddenButtons.Values)
        {
            if (button)
            {
                button.localScale = scale;
            }
        }

        HiddenButtons.Clear();
    }

    private static void Hide(Transform? button)
    {
        if (!button || HiddenButtons.ContainsKey(button!.Pointer))
        {
            return;
        }

        HiddenButtons[button.Pointer] = (button, button.localScale);
        button.localScale = Vector3.zero;
    }
}
