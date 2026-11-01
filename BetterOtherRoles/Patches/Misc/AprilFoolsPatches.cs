using HarmonyLib;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     April Fools modes of Among Us (horse, long neck, classic look) are turned off, even on their event dates: the
///     horse kept the collision of a normal player, the long neck showed nothing, and a player could keep a wrong size
///     after leaving the horse mode. The reversed Skeld (dlekS) is a map of its own (<see cref="DleksMapPatches"/>).
///     <see cref="AprilFoolsMode.ShouldLongAround"/>, <see cref="AprilFoolsMode.ShouldClassicMode"/> and
///     <see cref="AprilFoolsMode.ShouldFlipSkeld"/> are never patched: the game shares one compiled function between
///     them (and surely many other methods), so a patch on one changes all of them (seen: a player starting the game
///     as a ghost). The shape given by <see cref="GameManager.GetBodyType"/> is changed instead.
/// </summary>
[HarmonyPatch]
public static class AprilFoolsPatches
{
    [HarmonyPatch(typeof(AprilFoolsMode), nameof(AprilFoolsMode.ShouldHorseAround))]
    [HarmonyPatch(typeof(AprilFoolsMode), nameof(AprilFoolsMode.ShouldHorseModeMainMenu))]
    [HarmonyPatch(typeof(AprilFoolsMode), nameof(AprilFoolsMode.ShouldShowAprilFoolsToggle))]
    [HarmonyPrefix]
    public static bool NeverPrefix(ref bool __result)
    {
        __result = false;
        return false;
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.GetBodyType))]
    [HarmonyPostfix]
    public static void GetBodyTypePostfix(ref PlayerBodyTypes __result)
    {
        __result = __result switch
        {
            PlayerBodyTypes.Horse or PlayerBodyTypes.Long or PlayerBodyTypes.Classic => PlayerBodyTypes.Normal,
            PlayerBodyTypes.LongSeeker => PlayerBodyTypes.Seeker,
            _ => __result,
        };
    }
}
