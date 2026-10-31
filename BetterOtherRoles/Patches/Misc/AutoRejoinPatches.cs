using BetterOtherRoles.LocalSettings;
using BetterOtherRoles.Modules.AutoRejoin;
using HarmonyLib;
using MiraAPI.LocalSettings;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Starts the auto rejoin countdown on the end game screen (<see cref="AutoRejoinBehaviour"/>).
/// </summary>
[HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.Start))]
public static class AutoRejoinPatches
{
    [HarmonyPostfix]
    public static void StartPostfix(EndGameManager __instance)
    {
        if (!AmongUsClient.Instance || TutorialManager.InstanceExists || !LocalSettingsTabSingleton<BorLocalSettingsTab>.Instance.AutoRejoin.Value)
        {
            return;
        }

        __instance.gameObject.AddComponent<AutoRejoinBehaviour>();
    }
}
