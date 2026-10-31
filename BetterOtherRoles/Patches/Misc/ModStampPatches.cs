using HarmonyLib;
using UnityEngine.SceneManagement;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     The vanilla "MOD" stamp (shown by the game as soon as a mod asks for it, here Reactor) stays on screen everywhere,
///     even during the intro. Only keep it on the main menu.
/// </summary>
[HarmonyPatch(typeof(ModManager), nameof(ModManager.LateUpdate))]
public static class ModStampPatches
{
    private const string MainMenuScene = "MainMenu";

    [HarmonyPostfix]
    public static void LateUpdatePostfix(ModManager __instance)
    {
        if (!__instance.ModStamp)
        {
            return;
        }

        var visible = SceneManager.GetActiveScene().name == MainMenuScene;
        if (__instance.ModStamp.gameObject.activeSelf != visible)
        {
            __instance.ModStamp.gameObject.SetActive(visible);
        }
    }
}
