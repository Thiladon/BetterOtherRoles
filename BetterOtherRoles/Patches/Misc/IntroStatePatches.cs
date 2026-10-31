using HarmonyLib;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Tracks whether the game intro is over, to show some HUD elements only after it.
/// </summary>
[HarmonyPatch]
public static class IntroStatePatches
{
    /// <summary>
    ///     Gets a value indicating whether the game has started and its intro is over.
    /// </summary>
    public static bool IntroEnded { get; private set; }

    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.OnDestroy))]
    [HarmonyPostfix]
    public static void IntroCutsceneOnDestroyPostfix()
    {
        IntroEnded = true;
    }

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    [HarmonyPrefix]
    public static void HudManagerUpdatePrefix()
    {
        if (!AmongUsClient.Instance.IsGameStarted)
        {
            IntroEnded = false;
        }
    }
}
