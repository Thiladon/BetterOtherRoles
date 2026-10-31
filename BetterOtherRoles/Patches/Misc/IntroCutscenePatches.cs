using HarmonyLib;
using MiraAPI.Hud;

namespace BetterOtherRoles.Patches.Misc;

[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.OnDestroy))]
public static class IntroCutscenePatches
{
    /// <summary>
    ///     MiraAPI sets the initial cooldown when the button is created and lets it run during the intro,
    ///     so it is almost over when the game starts. Restart it for BetterOtherRoles buttons.
    /// </summary>
    [HarmonyPostfix]
    public static void OnDestroyPostfix()
    {
        ActionButtonCooldownPatches.Clear();

        if (AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay)
        {
            return;
        }

        var borAssembly = typeof(BetterOtherRolesPlugin).Assembly;
        foreach (var button in CustomButtonManager.Buttons)
        {
            if (button.GetType().Assembly == borAssembly)
            {
                button.SetTimer(button.InitialCooldown);
            }
        }
    }
}
