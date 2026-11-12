using BetterOtherRoles.Modules.Roles;
using BetterOtherRoles.Patches.Roles;
using HarmonyLib;
using MiraAPI.Roles;
using MiraAPI.Translation;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     What the haunt menu says of its target (BetterOtherRoles 1.7.1 HauntMenuMinigamePatch, TOU-Mira 1.7.9
///     HauntMenuMinigamePatch):
///     - "name - role", the role in its color, its role while alive for a dead target, when the local player knows it
///       (<see cref="NameColorPatches.KnowsRole"/>: "Ghosts See Roles" for a dead player); otherwise its team
///       (crewmate, neutral or impostor), as the game did;
///     - under it, "Ghost" for a dead target (modifiers to come with the first modifier);
///     - with the roles seen, the impostor filter, hidden by the game, is shown and also keeps the living neutrals.
/// </summary>
[HarmonyPatch]
public static class HauntMenuInfoPatches
{
    // HauntMenuMinigame.Start: the spacing of the filter buttons.
    private const float FilterSpacing = 1.2f;

    [HarmonyPatch(typeof(HauntMenuMinigame), nameof(HauntMenuMinigame.SetHauntTarget))]
    [HarmonyPostfix]
    public static void SetHauntTargetPostfix(HauntMenuMinigame __instance)
    {
        var target = __instance.HauntTarget;
        if (!target || !target.Data) return;

        __instance.NameText.text = $"<size=90%>{__instance.NameText.text} - {GetRoleText(target)}</size>";
        __instance.FilterText.text = target.Data.IsDead
            ? TranslationController.Instance.GetString(StringNames.Ghost)
            : string.Empty;
    }

    [HarmonyPatch(typeof(HauntMenuMinigame), nameof(HauntMenuMinigame.Start))]
    [HarmonyPostfix]
    public static void StartPostfix(HauntMenuMinigame __instance)
    {
        var buttons = __instance.FilterButtons;
        if (!BorGhostInfo.SeesRoles || buttons.Length == 0 || buttons[0].gameObject.activeSelf) return;

        buttons[0].gameObject.SetActive(true);
        var active = 0;
        foreach (var button in buttons)
        {
            if (button.gameObject.activeSelf)
                active++;
        }

        var edge = FilterSpacing * active / 2f;
        var index = 0;
        foreach (var button in buttons)
        {
            if (!button.gameObject.activeSelf) continue;

            button.transform.SetLocalX(FloatRange.SpreadToEdges(-edge, edge, index, active));
            index++;
        }
    }

    [HarmonyPatch(typeof(HauntMenuMinigame), nameof(HauntMenuMinigame.MatchesFilter))]
    [HarmonyPostfix]
    public static void MatchesFilterPostfix(HauntMenuMinigame __instance, PlayerControl pc, ref bool __result)
    {
        if (__instance.filterMode != HauntMenuMinigame.HauntFilters.Impostor || !pc.Data || pc.Data.IsDead) return;

        __result = BorRoleSelector.GetFaction(pc.Data.Role) is BorFaction.Impostor or BorFaction.Neutral;
    }

    private static string GetRoleText(PlayerControl target)
    {
        var role = AliveRolePatches.GetRole(target);
        if (!role) return string.Empty;

        var localRole = AliveRolePatches.GetRole(PlayerControl.LocalPlayer);
        string name;
        Color color;
        if (localRole && NameColorPatches.KnowsRole(localRole!, target, role!))
        {
            name = role is ICustomRole custom ? custom.RoleName : role!.NiceName;
            color = NameColorPatches.GetRoleColor(role!);
        }
        else
        {
            (name, color) = BorRoleSelector.GetFaction(role!) switch
            {
                BorFaction.Impostor => (TranslationController.Instance.GetString(StringNames.Impostor), Palette.ImpostorRed),
                BorFaction.Neutral => (MiraLocaleManager.Get("MiraApi.RoleTeam.Neutral", "Neutral"), Color.gray),
                _ => (TranslationController.Instance.GetString(StringNames.Crewmate), Palette.CrewmateBlue),
            };
        }

        return $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{name}</color>";
    }
}
