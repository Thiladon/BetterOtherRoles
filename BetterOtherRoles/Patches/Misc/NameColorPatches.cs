using BetterOtherRoles.LocalSettings;
using BetterOtherRoles.Options;
using BetterOtherRoles.Options.Roles.Crewmate;
using BetterOtherRoles.Patches.Roles;
using BetterOtherRoles.Roles.Crewmate;
using HarmonyLib;
using MiraAPI.GameOptions;
using MiraAPI.LocalSettings;
using MiraAPI.Roles;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Name colors, based on BetterOtherRoles 1.7.1 (HudManagerUpdatePatch.resetNameTagsAndColors / setNameColors):
///     own name in the role color, impostors see the other impostors in red. A dead player keeps its role while alive
///     (<see cref="Roles.AliveRolePatches.GetRole"/>), as the role with the name and the chat.
///     BOR refreshed them every frame; here they are only refreshed when needed:
///     game start, each meeting, each role change (<see cref="Utilities.BorPlayerExtensions.ChangeRole"/>) and each
///     change of the local setting <see cref="BorLocalSettingsTab.NameColors"/> (never: game colors, on me, on everyone).
/// </summary>
[HarmonyPatch]
public static class NameColorPatches
{
    private static int _coloredMeetingId;
    private static bool _listening;

    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.OnDestroy))]
    [HarmonyPostfix]
    public static void IntroCutsceneOnDestroyPostfix()
    {
        if (!_listening)
        {
            Settings.NameColors.SettingChanged += (_, _) => Refresh();
            _listening = true;
        }

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
            return;

        _coloredMeetingId = __instance.GetInstanceID();
        Refresh();
    }

    public static void Refresh()
    {
        var localRole = PlayerControl.LocalPlayer ? AliveRolePatches.GetRole(PlayerControl.LocalPlayer) : null;
        if (!AmongUsClient.Instance || !AmongUsClient.Instance.IsGameStarted || !localRole) return;

        var display = Settings.NameColors.Value;
        foreach (var player in PlayerControl.AllPlayerControls)
        {
            var role = player ? AliveRolePatches.GetRole(player) : null;
            if (!role) continue;

            SetPlayerNameColor(player, GetNameColor(display, localRole!, player, role!));
        }
    }

    private static BorLocalSettingsTab Settings => LocalSettingsTabSingleton<BorLocalSettingsTab>.Instance;

    // The color known for the role when the setting shows it on this player, else the game color (impostors in red
    // for an impostor).
    private static Color GetNameColor(NameColorDisplay display, RoleBehaviour localRole, PlayerControl player, RoleBehaviour role)
    {
        var shown = display == NameColorDisplay.Everyone || (display == NameColorDisplay.OnSelf && player.AmOwner);
        var known = shown ? GetKnownColor(localRole, player, role) : null;
        if (known.HasValue) return known.Value;

        return localRole.IsImpostor && role.IsImpostor ? Palette.ImpostorRed : Color.white;
    }

    /// <summary>
    ///     Gets the color the local player knows for the name of a player: its own role, the Sheriff and the Deputy who
    ///     know each other, the other impostors for an impostor. Null when nothing is known.
    /// </summary>
    /// <param name="localRole">The role of the local player.</param>
    /// <param name="player">The player.</param>
    /// <param name="role">The role of the player.</param>
    /// <returns>The color, or null.</returns>
    public static Color? GetKnownColor(RoleBehaviour localRole, PlayerControl player, RoleBehaviour role)
    {
        if (player.AmOwner) return GetRoleColor(role);
        if (KnowsEachOther(localRole, role)) return BorColors.Sheriff;
        if (localRole.IsImpostor && role.IsImpostor) return Palette.ImpostorRed;

        return null;
    }

    /// <summary>
    ///     Gets whether the local player knows the role of a player, shown with its name (<see cref="RoleNamePatches"/>):
    ///     its own role, the Sheriff and the Deputy who know each other, the other impostors for an impostor with
    ///     <see cref="GameplayOptions.ImpostorsKnowRoles"/>.
    /// </summary>
    /// <param name="localRole">The role of the local player.</param>
    /// <param name="player">The player.</param>
    /// <param name="role">The role of the player.</param>
    /// <returns>True when the role is known.</returns>
    public static bool KnowsRole(RoleBehaviour localRole, PlayerControl player, RoleBehaviour role)
    {
        if (player.AmOwner || KnowsEachOther(localRole, role)) return true;

        return localRole.IsImpostor && role.IsImpostor && OptionGroupSingleton<GameplayOptions>.Instance.ImpostorsKnowRoles.Value;
    }

    /// <summary>
    ///     "Sheriff And Deputy Know Each Other" (BOR: setNameColors): the Sheriff sees the Deputy, and the Deputy sees the Sheriff.
    /// </summary>
    private static bool KnowsEachOther(RoleBehaviour localRole, RoleBehaviour otherRole)
    {
        var options = OptionGroupSingleton<SheriffOptions>.Instance;
        if (!options.HasDeputy || !options.DeputyKnowsSheriff.Value) return false;

        return (localRole is SheriffRole && otherRole is DeputyRole) || (localRole is DeputyRole && otherRole is SheriffRole);
    }

    /// <summary>
    ///     Gets the color of a role: its own for a mod role, impostor red or white for a game role.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <returns>The color.</returns>
    public static Color GetRoleColor(RoleBehaviour role)
    {
        if (role is ICustomRole customRole) return customRole.RoleColor;

        return role.IsImpostor ? Palette.ImpostorRed : Color.white;
    }

    private static void SetPlayerNameColor(PlayerControl player, Color color)
    {
        player.cosmetics.nameText.color = color;

        if (!MeetingHud.Instance) return;

        foreach (var voteArea in MeetingHud.Instance.playerStates)
        {
            if (voteArea.PlayerId == player.PlayerId && voteArea.NameText)
                voteArea.NameText.color = color;
        }
    }
}
