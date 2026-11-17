using BetterOtherRoles.Options;
using BetterOtherRoles.Utilities;
using HarmonyLib;
using MiraAPI.GameOptions;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Names of the other players hidden out of sight (TOU-Mira 1.7.9 HideNamesOutOfSight,
///     <see cref="GameplayOptions.HideNamesOutOfSight"/>), for a living player:
///     <list type="bullet">
///         <item>basic: as in the game, every name is shown;</item>
///         <item>room-based: shown when the player is in sight, or in the same room, even behind a wall;</item>
///         <item>harsh: shown only when the player is in sight, the room does not count.</item>
///     </list>
///     Either way, shown while a part of the player is on a cam watched now (<see cref="BorSight"/>). The name text is
///     turned off (the role and progress written in it with it, and the color blind text) rather than emptied, as the
///     name is written again each frame (<see cref="RoleNamePatches"/>). Only the names hidden here are shown again;
///     none in the lobby, in meetings or in Hide and Seek.
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class HiddenNamesPatches
{
    private static readonly HashSet<byte> Hidden = [];

    public static void Postfix(HudManager __instance)
    {
        var mode = GetMode(__instance);
        if (mode == NameSight.Basic)
        {
            if (Hidden.Count > 0) ShowAll();
            return;
        }

        if (MeetingHud.Instance) return;

        var local = PlayerControl.LocalPlayer;
        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (!player || player.AmOwner || player.Data == null || !player.cosmetics) continue;

            SetShown(player, IsShown(local, player, mode));
        }
    }

    // Basic when the names are not hidden: not in game, Hide and Seek, dead or without shadows (the dead see all).
    private static NameSight GetMode(HudManager hud)
    {
        var local = PlayerControl.LocalPlayer;
        if (!ShipStatus.Instance || LobbyBehaviour.Instance || !local || local.Data == null || local.Data.IsDead ||
            !local.lightSource || !hud.ShadowQuad || !hud.ShadowQuad.gameObject.activeInHierarchy ||
            (GameManager.Instance && GameManager.Instance.IsHideAndSeek()))
            return NameSight.Basic;

        return (NameSight)OptionGroupSingleton<GameplayOptions>.Instance.HideNamesOutOfSight.Value;
    }

    private static bool IsShown(PlayerControl local, PlayerControl player, NameSight mode) =>
        BorSight.InSight(player) || (mode == NameSight.RoomBased && BorSight.InSameRoom(local, player)) ||
        BorSight.OnWatchedCams(player);

    private static void SetShown(PlayerControl player, bool shown)
    {
        if (shown)
        {
            if (Hidden.Remove(player.PlayerId))
                Toggle(player, true);
            return;
        }

        Hidden.Add(player.PlayerId);
        Toggle(player, false);
    }

    private static void Toggle(PlayerControl player, bool shown)
    {
        var name = player.cosmetics.nameText;
        if (name && name.enabled != shown)
            name.enabled = shown;

        var colorBlind = player.cosmetics.colorBlindText;
        if (colorBlind && colorBlind.enabled != shown)
            colorBlind.enabled = shown;
    }

    // Out of the game, dead or option off: every hidden name back.
    private static void ShowAll()
    {
        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (player && player.cosmetics && Hidden.Contains(player.PlayerId))
                Toggle(player, true);
        }

        Hidden.Clear();
    }
}
