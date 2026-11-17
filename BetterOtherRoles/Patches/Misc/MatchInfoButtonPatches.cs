using HarmonyLib;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     MiraAPI shows the vanilla Match Info (role guide) button on every FixedUpdate, even during the intro.
///     Hide it until the intro is over, like the rest of the HUD, and during the end of a meeting (exile animation):
///     the buttons under it (Debug, Zoom) go with it, MiraAPI shows them again once the animation is over.
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class MatchInfoButtonPatches
{
    [HarmonyPostfix]
    public static void UpdatePostfix(HudManager __instance)
    {
        if (AmongUsClient.Instance.IsGameStarted && (!IntroStatePatches.IntroEnded || ExileController.Instance) &&
            __instance.MatchInfoButton && __instance.MatchInfoButton.gameObject.activeSelf)
            __instance.MatchInfoButton.gameObject.SetActive(false);
    }
}
