using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     During the meeting animation (KillOverlay), the game hides the settings button but not the chat and role guide
///     buttons: hide them too until the animation is over. During a kill animation (same overlay), they all stay shown.
///     They are shrunk to nothing instead of deactivated: the game and MiraAPI show them again on their own, and the
///     TopRight container would rearrange its buttons.
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class KillOverlayHudPatches
{
    private static readonly Dictionary<IntPtr, (Transform Button, Vector3 Scale)> HiddenButtons = [];

    // Last animation started on the overlay: a meeting (true) or a kill (false).
    private static bool _meetingShown;

    /// <summary>
    ///     Gets a value indicating whether a meeting animation is shown.
    /// </summary>
    public static bool IsMeetingAnimation { get; private set; }

    [HarmonyPrefix]
    public static void UpdatePrefix(HudManager __instance)
    {

        IsMeetingAnimation = _meetingShown && __instance.KillOverlay && __instance.KillOverlay.IsOpen;
        if (IsMeetingAnimation)
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

    [HarmonyPatch]
    public static class ShowMeetingPatch
    {
        public static MethodBase TargetMethod() => AccessTools.Method(typeof(KillOverlay), nameof(KillOverlay.ShowMeeting));

        public static void Prefix() => _meetingShown = true;
    }

    // Every overload (with or without a kill animation given).
    [HarmonyPatch]
    public static class ShowKillAnimationPatch
    {
        public static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredMethods(typeof(KillOverlay))
            .Where(x => x.Name == nameof(KillOverlay.ShowKillAnimation));

        public static void Prefix() => _meetingShown = false;
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
