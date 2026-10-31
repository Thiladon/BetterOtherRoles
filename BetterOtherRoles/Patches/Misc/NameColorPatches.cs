using BetterOtherRoles.Options.Roles.Crewmate;
using BetterOtherRoles.Roles.Crewmate;
using HarmonyLib;
using MiraAPI.GameOptions;
using MiraAPI.Roles;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Name colors, based on BetterOtherRoles 1.7.1 (HudManagerUpdatePatch.resetNameTagsAndColors / setNameColors):
///     own name in the role color, impostors see the other impostors in red.
///     BOR refreshed them every frame; here they are only refreshed when needed:
///     game start, each meeting, and each role change (<see cref="Utilities.BorPlayerExtensions.ChangeRole"/>).
/// </summary>
[HarmonyPatch]
public static class NameColorPatches
{
    private static int _coloredMeetingId;

    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.OnDestroy))]
    [HarmonyPostfix]
    public static void IntroCutsceneOnDestroyPostfix()
    {
        Refresh();
    }

    /// <summary>
    ///     The vote areas may not exist yet in MeetingHud.Start: color them once, as soon as they are there.
    /// </summary>
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
    [HarmonyPostfix]
    public static void MeetingHudUpdatePostfix(MeetingHud __instance)
    {
        if (_coloredMeetingId == __instance.GetInstanceID() || __instance.playerStates == null || __instance.playerStates.Length == 0)
        {
            return;
        }

        _coloredMeetingId = __instance.GetInstanceID();
        Refresh();
    }

    public static void Refresh()
    {
        var localRole = PlayerControl.LocalPlayer ? PlayerControl.LocalPlayer.Data?.Role : null;
        if (!AmongUsClient.Instance.IsGameStarted || !localRole)
        {
            return;
        }

        var amImpostor = localRole!.IsImpostor;
        foreach (var player in PlayerControl.AllPlayerControls)
        {
            var role = player ? player.Data?.Role : null;
            if (!role)
            {
                continue;
            }

            var color = amImpostor && role!.IsImpostor ? Palette.ImpostorRed : Color.white;
            if (player.AmOwner)
            {
                color = GetRoleColor(role!);
            }
            else if (KnowsEachOther(localRole, role!))
            {
                color = BorColors.Sheriff;
            }

            SetPlayerNameColor(player, color);
        }
    }

    /// <summary>
    ///     "Sheriff And Deputy Know Each Other" (BOR: setNameColors): the Sheriff sees the Deputy, and the Deputy sees the Sheriff.
    /// </summary>
    private static bool KnowsEachOther(RoleBehaviour localRole, RoleBehaviour otherRole)
    {
        var options = OptionGroupSingleton<SheriffOptions>.Instance;
        if (!options.HasDeputy || !options.DeputyKnowsSheriff.Value)
        {
            return false;
        }

        return (localRole is SheriffRole && otherRole is DeputyRole) || (localRole is DeputyRole && otherRole is SheriffRole);
    }

    private static Color GetRoleColor(RoleBehaviour role)
    {
        if (role is ICustomRole customRole)
        {
            return customRole.RoleColor;
        }

        return role.IsImpostor ? Palette.ImpostorRed : Color.white;
    }

    private static void SetPlayerNameColor(PlayerControl player, Color color)
    {
        player.cosmetics.nameText.color = color;

        if (!MeetingHud.Instance)
        {
            return;
        }

        foreach (var voteArea in MeetingHud.Instance.playerStates)
        {
            if (voteArea.PlayerId == player.PlayerId && voteArea.NameText)
            {
                voteArea.NameText.color = color;
            }
        }
    }
}
