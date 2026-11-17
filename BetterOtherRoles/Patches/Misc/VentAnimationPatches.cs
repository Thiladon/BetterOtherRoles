using BetterOtherRoles.Options;
using BetterOtherRoles.Utilities;
using HarmonyLib;
using MiraAPI.GameOptions;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Vent animations hidden (TOU-Mira 1.7.9 HideVentAnimationNotInVision, <see cref="GameplayOptions.HideVentAnimations"/>),
///     every map: when another player goes in or out of a vent, its lid only moves for a living player who sees a part of
///     the vent (in Same room, only from the room of the vent), or sees it on a cam watched now (<see cref="BorSight"/>).
///     TOU checks the player against the walls, from its feet only. The player who vents and the dead always see it;
///     nothing changes in the lobby, without shadows or in Hide and Seek.
/// </summary>
[HarmonyPatch(typeof(Vent))]
public static class VentAnimationPatches
{
    [HarmonyPatch(nameof(Vent.EnterVent))]
    [HarmonyPrefix]
    public static bool EnterVentPrefix(Vent __instance, PlayerControl pc) => !__instance.EnterVentAnim || IsShown(__instance, pc);

    // A coroutine: skipped, the player reappears without waiting for the lid.
    [HarmonyPatch(nameof(Vent.ExitVent))]
    [HarmonyPrefix]
    public static bool ExitVentPrefix(Vent __instance, PlayerControl pc) => !__instance.ExitVentAnim || IsShown(__instance, pc);

    private static bool IsShown(Vent vent, PlayerControl pc)
    {
        var mode = (VentSight)OptionGroupSingleton<GameplayOptions>.Instance.HideVentAnimations.Value;
        if (mode == VentSight.Never || !pc || pc.AmOwner) return true;

        var local = PlayerControl.LocalPlayer;
        var hud = HudManager.InstanceExists ? HudManager.Instance : null;
        if (!ShipStatus.Instance || LobbyBehaviour.Instance || !local || local.Data == null || local.Data.IsDead ||
            !hud || !hud!.ShadowQuad || !hud.ShadowQuad.gameObject.activeInHierarchy ||
            (GameManager.Instance && GameManager.Instance.IsHideAndSeek()))
            return true;

        var bounds = vent.myRend ? vent.myRend.bounds : new Bounds(vent.transform.position, Vector3.one * 0.5f);
        var room = mode != VentSight.SameRoom || BorSight.InSameRoom(local.GetTruePosition(), vent.transform.position);
        var sight = room && BorSight.InSight(bounds);
        return sight || BorSight.OnWatchedCams(bounds);
    }
}
