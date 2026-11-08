using BetterOtherRoles.LocalSettings;
using BetterOtherRoles.Modules.Roles;
using BetterOtherRoles.Patches.Roles;
using BetterOtherRoles.Roles;
using HarmonyLib;
using MiraAPI.LocalSettings;
using MiraAPI.Roles;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     The role of a player shown with its name when the local player knows it (<see cref="NameColorPatches.KnowsRole"/>),
///     in the color of the role, then its progress (TOU-Mira 1.7.9): the progress of the role (<see cref="IBorProgress"/>),
///     or the task counter of a crewmate (<see cref="BorTaskInfo"/>, own counter only for now), on the players chosen in
///     the local setting <see cref="BorLocalSettingsTab.PlayerProgress"/>. At the places of BetterOtherRoles 1.7.1
///     (PlayerControlPatch, "Info" text), above or below the name as chosen in the local settings
///     <see cref="BorLocalSettingsTab.RoleInGame"/> and <see cref="BorLocalSettingsTab.RoleInMeeting"/>. In game: above
///     by default (75 % of its size), written in the name text itself as TOU; set again when the game writes the name
///     back. In meetings: below by default (60 %), in a text of its own as BOR. The name never moves. A dead player is
///     shown with its role while alive. The tasks tab shows the counter too.
/// </summary>
[HarmonyPatch]
public static class RoleNamePatches
{
    // Size of the line in game, and the meeting text next to the name (BOR: 0.2 lower, 60 % of the name).
    private const string GameSize = "75%";
    private const float MeetingOffset = 0.2f;
    private const float MeetingScale = 0.6f;
    private const string MeetingTextName = "BorRoleName";
    private const string TasksTabTextName = "TabText_TMP";

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    [HarmonyPostfix]
    public static void HudUpdatePostfix(HudManager __instance)
    {
        if (!TryGetLocalRole(out var localRole)) return;

        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (!player || !player.Data || !player.cosmetics || !player.cosmetics.nameText) continue;

            // Grows away from the name, which stays in place: from its bottom with the line above, from its top below.
            var above = Settings.RoleInGame.Value == RolePlace.Above;
            if (SetText(player.cosmetics.nameText, Compose(localRole, player, above)))
                player.cosmetics.nameText.alignment = above ? TextAlignmentOptions.Bottom : TextAlignmentOptions.Top;
        }

        RefreshTasksTab(__instance);
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
    [HarmonyPostfix]
    public static void MeetingUpdatePostfix(MeetingHud __instance)
    {
        if (__instance.playerStates == null || !TryGetLocalRole(out var localRole)) return;

        var offset = Settings.RoleInMeeting.Value == RolePlace.Above ? Vector3.up * MeetingOffset : Vector3.down * MeetingOffset;

        foreach (var area in __instance.playerStates)
        {
            if (!area || !area.NameText) continue;

            var data = GameData.Instance ? GameData.Instance.GetPlayerById(area.PlayerId) : null;
            var player = data ? data!.Object : null;
            if (!player || !player!.Data) continue;

            var line = GetInfoLine(localRole, player, true, null);
            var text = GetMeetingText(area, line.Length > 0);
            if (!text) continue;

            text!.transform.localPosition = area.NameText.transform.localPosition + offset;
            SetText(text, line);
        }
    }

    // In game only, with the role of the local player (while alive, when dead).
    private static bool TryGetLocalRole(out RoleBehaviour localRole)
    {
        localRole = null!;
        if (!AmongUsClient.Instance || !AmongUsClient.Instance.IsGameStarted || !PlayerControl.LocalPlayer) return false;

        var role = AliveRolePatches.GetRole(PlayerControl.LocalPlayer);
        if (!role) return false;

        localRole = role!;
        return true;
    }

    private static BorLocalSettingsTab Settings => LocalSettingsTabSingleton<BorLocalSettingsTab>.Instance;

    // The name, with the line of the role and progress above or below it.
    private static string Compose(RoleBehaviour localRole, PlayerControl player, bool above)
    {
        var line = GetInfoLine(localRole, player, false, GameSize);
        if (line.Length == 0) return player.Data.PlayerName;

        return above ? line + "\n" + player.Data.PlayerName : player.Data.PlayerName + "\n" + line;
    }

    // The role in its color when it is known, then the progress when it is shown (at a size, or at the size of the
    // text); empty when there is nothing.
    private static string GetInfoLine(RoleBehaviour localRole, PlayerControl player, bool inMeeting, string? size)
    {
        var role = AliveRolePatches.GetRole(player);
        var line = string.Empty;
        if (role && NameColorPatches.KnowsRole(localRole, player, role!))
        {
            var roleName = role is ICustomRole custom ? custom.RoleName : role!.NiceName;
            line = $"<color=#{ColorUtility.ToHtmlStringRGBA(NameColorPatches.GetRoleColor(role!))}>{roleName}</color>";
        }

        if (ShowsProgress(player) && TryGetProgress(role, player, inMeeting, out var progress))
            line = line.Length == 0 ? progress : $"{line} {progress}";

        if (line.Length == 0 || size == null) return line;

        return $"<size={size}>{line}</size>";
    }

    // The local setting: on me, on the others, on everyone or never.
    private static bool ShowsProgress(PlayerControl player) => Settings.PlayerProgress.Value switch
    {
        ProgressDisplay.Always => true,
        ProgressDisplay.OnSelf => player.AmOwner,
        ProgressDisplay.OnOthers => !player.AmOwner,
        _ => false,
    };

    // The progress of the role, or the task counter of a crewmate: its own only (the dead: Ghost settings, later).
    private static bool TryGetProgress(RoleBehaviour? role, PlayerControl player, bool inMeeting, out string progress)
    {
        if (role is IBorProgress tally) return tally.TryGetProgress(player, inMeeting, out progress);

        progress = string.Empty;
        return player.AmOwner && BorTaskInfo.TryGet(player, out progress);
    }

    // "Tasks (2/5)" on the tab of the task list, for a crewmate.
    private static void RefreshTasksTab(HudManager hud)
    {
        var panel = hud.TaskPanel;
        var tab = panel && panel.tab ? panel.tab.transform.Find(TasksTabTextName)?.GetComponent<TextMeshPro>() : null;
        if (!tab) return;

        var label = TranslationController.Instance.GetString(StringNames.Tasks);
        var text = BorTaskInfo.TryGet(PlayerControl.LocalPlayer, out var counter) ? $"{label} {counter}" : label;
        if (tab!.text != text)
            tab.text = text;
    }

    // The text next to the name of a vote area, made the first time a line is shown there (a copy of the name).
    private static TextMeshPro? GetMeetingText(PlayerVoteArea area, bool create)
    {
        var parent = area.NameText.transform.parent;
        var existing = parent.Find(MeetingTextName);
        if (existing) return existing.GetComponent<TextMeshPro>();
        if (!create) return null;

        var text = Object.Instantiate(area.NameText, parent);
        text.name = MeetingTextName;
        text.fontSize = area.NameText.fontSize * MeetingScale;
        text.color = Color.white;
        text.text = string.Empty;
        return text;
    }

    // Written only when it changed.
    private static bool SetText(TextMeshPro text, string value)
    {
        if (text.text == value) return false;

        text.text = value;
        return true;
    }
}
