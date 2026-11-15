using BetterOtherRoles.Modules.Bodies;
using BetterOtherRoles.Options;
using HarmonyLib;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Vitals of the players cleaned (<see cref="BorCleanedBodies"/>, TOU-Mira 1.7.9 VitalsBodyPatches): "MIS"
///     background when missing, and disconnected when the body is destroyed while the vitals are open (the game only
///     looks at the bodies when they open), and alive again when revived while they are open. Applied after the game's
///     own update, which leaves a dead panel as it is.
/// </summary>
[HarmonyPatch(typeof(VitalsMinigame))]
public static class VitalsCleanedPatches
{
    [HarmonyPatch(nameof(VitalsMinigame.Begin))]
    [HarmonyPostfix]
    public static void BeginPostfix(VitalsMinigame __instance) => Refresh(__instance);

    [HarmonyPatch(nameof(VitalsMinigame.Update))]
    [HarmonyPostfix]
    public static void UpdatePostfix(VitalsMinigame __instance) => Refresh(__instance);

    private static void Refresh(VitalsMinigame minigame)
    {
        if (minigame.vitals == null) return;

        foreach (var panel in minigame.vitals)
        {
            if (!panel || panel.PlayerInfo == null) continue;

            if (!panel.PlayerInfo.IsDead)
            {
                if (!panel.PlayerInfo.Disconnected && (panel.IsDead || panel.IsDiscon))
                    SetAlive(minigame, panel);
                continue;
            }

            if (!BorCleanedBodies.TryGet(panel.PlayerInfo.PlayerId, out var mode)) continue;

            if (mode == CleanedBody.Missing && panel.Background.sprite != BorCleanedBodies.MissingSprite)
                SetMissing(panel);
            else if (mode == CleanedBody.Disconnected && !panel.IsDiscon)
                panel.SetDisconnected();
        }
    }

    // A player revived while the vitals are open (the game never expects it): back to the background of a new panel,
    // with a beating line.
    private static void SetAlive(VitalsMinigame minigame, VitalsPanel panel)
    {
        panel.IsDead = false;
        panel.IsDiscon = false;
        panel.Background.sprite = minigame.PanelPrefab.Background.sprite;
        panel.Cardio.gameObject.SetActive(true);
        panel.SetAlive();
    }

    // As the game's SetDead, without a flat line: IsDead stays on, so the game leaves the panel as it is.
    private static void SetMissing(VitalsPanel panel)
    {
        panel.IsDead = true;
        panel.IsDiscon = false;
        panel.Background.sprite = BorCleanedBodies.MissingSprite;
        panel.Cardio.gameObject.SetActive(false);
    }
}
