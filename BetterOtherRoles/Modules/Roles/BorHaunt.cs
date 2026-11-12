using BetterOtherRoles.Options;
using HarmonyLib;
using MiraAPI.GameOptions;
using MiraAPI.Translation;
using MiraAPI.Utilities;
using UnityEngine;

namespace BetterOtherRoles.Modules.Roles;

/// <summary>
///     Whether a dead player may haunt, from <see cref="GameplayOptions.DeadCanHaunt"/> (TOU-Mira 1.7.9 DeadCanHaunt)
///     and <see cref="GameplayOptions.FinishTasksBeforeHaunt"/> (BetterOtherRoles 1.7.1
///     finishTasksBeforeHauntingOrZoomingOut). While it may not, the haunt button is greyed out and a click shows why
///     (TOU-Mira 1.7.9 HauntMenuMinigamePatch), instead of opening the haunt menu. Hiding the button, as BOR did, would
///     fight the game, which shows it again by itself (meetings, map, tasks).
///     "From the next round" (TOU-Mira 1.7.9 DiedOtherRound): a player who died out of a meeting may haunt once the
///     next meeting has started; a player voted out may haunt right away.
/// </summary>
[HarmonyPatch]
public static class BorHaunt
{
    private enum Refusal
    {
        None,
        Never,
        NextRound,
        Tasks,
    }

    // The local player died during this round (out of a meeting and of the ejection).
    private static bool _diedThisRound;

    // The haunt button was greyed out here (given back once haunting is allowed).
    private static bool _greyedOut;

    /// <summary>
    ///     Gets whether the local player may haunt now.
    /// </summary>
    public static bool CanHaunt => GetRefusal() == Refusal.None;

    private static GameplayOptions Options => OptionGroupSingleton<GameplayOptions>.Instance;

    private static Refusal GetRefusal()
    {
        if (TutorialManager.InstanceExists) return Refusal.None;

        switch ((HauntMode)Options.DeadCanHaunt.Value)
        {
            case HauntMode.Never:
                return Refusal.Never;
            case HauntMode.NextRound when _diedThisRound:
                return Refusal.NextRound;
        }

        var player = PlayerControl.LocalPlayer;
        if (Options.FinishTasksBeforeHaunt.Value && player && !BorTaskInfo.AreDone(player)) return Refusal.Tasks;

        return Refusal.None;
    }

    private static bool IsHauntRole(RoleBehaviour? role) => role && (role!.TryCast<CrewmateGhostRole>() || role.TryCast<ImpostorGhostRole>());

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Die))]
    [HarmonyPostfix]
    public static void DiePostfix(PlayerControl __instance)
    {
        if (__instance.AmOwner)
            _diedThisRound = !MeetingHud.Instance && !ExileController.Instance;
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    [HarmonyPostfix]
    public static void MeetingStartPostfix() => _diedThisRound = false;

    [HarmonyPatch(typeof(CrewmateGhostRole), nameof(CrewmateGhostRole.UseAbility))]
    [HarmonyPrefix]
    public static bool CrewmateUseAbilityPrefix() => TryOpen();

    [HarmonyPatch(typeof(ImpostorGhostRole), nameof(ImpostorGhostRole.UseAbility))]
    [HarmonyPrefix]
    public static bool ImpostorUseAbilityPrefix() => TryOpen();

    // The haunt menu opens only when allowed; closing it always works.
    private static bool TryOpen()
    {
        if (Minigame.Instance || HudManager.Instance.Chat.IsOpenOrOpening) return true;

        var refusal = GetRefusal();
        if (refusal == Refusal.None) return true;

        ShowRefusal(refusal);
        return false;
    }

    private static void ShowRefusal(Refusal refusal)
    {
        var text = refusal switch
        {
            Refusal.Never => MiraLocaleManager.Get("BetterOtherRoles.Haunt.Never", "Haunting was disabled by the host!"),
            Refusal.NextRound => MiraLocaleManager.Get("BetterOtherRoles.Haunt.NextRound", "You must wait until the next round to haunt!"),
            _ => MiraLocaleManager.Get("BetterOtherRoles.Haunt.Tasks", "Finish your tasks to haunt!"),
        };

        Helpers.CreateAndShowNotification($"<b>{text}</b>", Color.white, new Vector3(0f, 1f, -20f));
    }

    // Greyed out while haunting is not allowed, given back once it is (the haunt menu greys it out itself while open).
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    [HarmonyPostfix]
    public static void HudUpdatePostfix(HudManager __instance)
    {
        var player = PlayerControl.LocalPlayer;
        if (!player || !player.Data || !IsHauntRole(player.Data.Role))
        {
            _greyedOut = false;
            return;
        }

        var button = __instance.AbilityButton;
        if (!button || Minigame.Instance) return;

        if (!CanHaunt)
        {
            if (button.CanInteract())
                button.SetDisabled();
            _greyedOut = true;
        }
        else if (_greyedOut)
        {
            button.SetEnabled();
            _greyedOut = false;
        }
    }
}
