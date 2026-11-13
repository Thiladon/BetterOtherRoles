using HarmonyLib;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     The vanilla "MOD" stamp, shown by the game as soon as a mod asks for it (here Reactor): hidden everywhere.
/// </summary>
[HarmonyPatch(typeof(ModManager), nameof(ModManager.LateUpdate))]
public static class ModStampPatches
{
    [HarmonyPostfix]
    public static void LateUpdatePostfix(ModManager __instance)
    {
        if (__instance.ModStamp && __instance.ModStamp.gameObject.activeSelf)
            __instance.ModStamp.gameObject.SetActive(false);
    }
}
