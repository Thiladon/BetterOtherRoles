using HarmonyLib;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     MiraAPI shows the vanilla Match Info (role guide) button on every FixedUpdate, even during the intro.
///     Hide it until the intro is over, like the rest of the HUD.
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class MatchInfoButtonPatches
{
    [HarmonyPostfix]
    public static void UpdatePostfix(HudManager __instance)
    {
        if (AmongUsClient.Instance.IsGameStarted && !IntroStatePatches.IntroEnded &&
            __instance.MatchInfoButton && __instance.MatchInfoButton.gameObject.activeSelf)
            __instance.MatchInfoButton.gameObject.SetActive(false);
    }
}
