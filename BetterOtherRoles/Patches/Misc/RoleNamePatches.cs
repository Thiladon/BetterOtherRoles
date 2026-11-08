using BetterOtherRoles.Patches.Roles;
using HarmonyLib;
using MiraAPI.Roles;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     The role of a player shown with its name when the local player knows it (<see cref="NameColorPatches.KnowsRole"/>),
///     in the color of the role, at the places of BetterOtherRoles 1.7.1 (PlayerControlPatch, "Info" text). In game:
///     above the name (75 % of its size), written in the name text itself as TOU-Mira 1.7.9; set again when the game
///     writes the name back. In meetings: below the name (60 %), in a text of its own as BOR, the name left in place.
///     A dead player is shown with its role while alive.
/// </summary>
[HarmonyPatch]
public static class RoleNamePatches
{
    // Size of the role line in game, and the meeting text under the name (BOR: 0.2 lower, 60 % of the name).
    private const string GameSize = "75%";
    private const float MeetingOffset = 0.2f;
    private const float MeetingScale = 0.6f;
    private const string MeetingTextName = "BorRoleName";

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    [HarmonyPostfix]
    public static void HudUpdatePostfix()
    {
        if (!TryGetLocalRole(out var localRole)) return;

        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (!player || !player.Data || !player.cosmetics || !player.cosmetics.nameText) continue;

            // Grows from its bottom: the name stays in place with the role above.
            if (SetText(player.cosmetics.nameText, Compose(localRole, player)))
                player.cosmetics.nameText.alignment = TextAlignmentOptions.Bottom;
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
    [HarmonyPostfix]
    public static void MeetingUpdatePostfix(MeetingHud __instance)
    {
        if (__instance.playerStates == null || !TryGetLocalRole(out var localRole)) return;

        foreach (var area in __instance.playerStates)
        {
            if (!area || !area.NameText) continue;

            var data = GameData.Instance ? GameData.Instance.GetPlayerById(area.PlayerId) : null;
            var player = data ? data!.Object : null;
            if (!player || !player!.Data) continue;

            var line = GetKnownRoleLine(localRole, player, null);
            var text = GetMeetingText(area, line.Length > 0);
            if (text)
                SetText(text!, line);
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

    // The name, with the role above it when it is known.
    private static string Compose(RoleBehaviour localRole, PlayerControl player)
    {
        var line = GetKnownRoleLine(localRole, player, GameSize);
        return line.Length == 0 ? player.Data.PlayerName : line + "\n" + player.Data.PlayerName;
    }

    // The role in its color (at a size, or at the size of the text), or empty when it is not known.
    private static string GetKnownRoleLine(RoleBehaviour localRole, PlayerControl player, string? size)
    {
        var role = AliveRolePatches.GetRole(player);
        if (!role || !NameColorPatches.KnowsRole(localRole, player, role!)) return string.Empty;

        var roleName = role is ICustomRole custom ? custom.RoleName : role!.NiceName;
        var color = ColorUtility.ToHtmlStringRGBA(NameColorPatches.GetRoleColor(role!));
        var line = $"<color=#{color}>{roleName}</color>";
        return size == null ? line : $"<size={size}>{line}</size>";
    }

    // The text under the name of a vote area, made the first time a role is shown there (a copy of the name).
    private static TextMeshPro? GetMeetingText(PlayerVoteArea area, bool create)
    {
        var parent = area.NameText.transform.parent;
        var existing = parent.Find(MeetingTextName);
        if (existing) return existing.GetComponent<TextMeshPro>();
        if (!create) return null;

        var text = Object.Instantiate(area.NameText, parent);
        text.name = MeetingTextName;
        text.transform.localPosition = area.NameText.transform.localPosition + (Vector3.down * MeetingOffset);
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
