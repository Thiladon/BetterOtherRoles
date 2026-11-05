using BetterOtherRoles.Modules.Debug;
using BetterOtherRoles.Modules.Roles;
using BetterOtherRoles.Options;
using HarmonyLib;
using MiraAPI.GameOptions;

namespace BetterOtherRoles.Modules.Cooldowns;

/// <summary>
///     When the cooldowns pause, the same for every role (kill button of the impostors, buttons of the mod):
///     - always in a vent, whatever the role and the options;
///     - while the player is busy (<see cref="IsPlayerBusy"/>), as in BetterOtherRoles 1.7.1, when the option
///       <see cref="GameplayOptions.PauseCooldownsInMinigames"/> is on for the player's faction.
///     The game no longer sets moveable to false in minigames (PlayerControl.CanMove checks Minigame.Instance instead):
///     the vanilla kill timer keeps running there, which this class fixes for the impostors.
/// </summary>
[HarmonyPatch]
public static class BorCooldowns
{
    // Vanilla kill timer before PlayerControl.FixedUpdate, to undo its countdown.
    private static float _killTimerBefore;

    /// <summary>
    ///     Gets a value indicating whether the local player is busy: in a minigame (task, Polus door, cameras, vitals...)
    ///     or at the admin table when it stops the player (IsOpenStopped, as PlayerControl.CanMove). Not with the plain map
    ///     (minimap) nor the Debug menu, a minigame too.
    /// </summary>
    public static bool IsPlayerBusy =>
        (Minigame.Instance && BorPhoneMenu.Current == null) ||
        (MapBehaviour.Instance && MapBehaviour.Instance.IsOpenStopped);

    /// <summary>
    ///     Gets a value indicating whether the cooldowns of the player pause now because they are busy: when the
    ///     option is on for the player's faction. The vent is checked apart (always a pause).
    /// </summary>
    /// <param name="player">The local player.</param>
    /// <returns>True to pause.</returns>
    public static bool PausesWhileBusy(PlayerControl player)
    {
        if (!IsPlayerBusy) return false;

        var options = OptionGroupSingleton<GameplayOptions>.Instance;
        if (!options.PauseCooldownsInMinigames.Value) return false;

        if (player.Data?.Role == null) return true;

        return BorRoleSelector.GetFaction(player.Data.Role) switch
        {
            BorFaction.Impostor => options.PauseCooldownsImpostors.Value,
            BorFaction.Neutral => options.PauseCooldownsNeutrals.Value,
            _ => options.PauseCooldownsCrewmates.Value,
        };
    }

    // -----------------------------------------------------------------------------------------------------------------
    // Vanilla kill timer (impostors): counted down in PlayerControl.FixedUpdate, already paused in a vent
    // (IsKillTimerEnabled). Put back after the countdown when it must pause.
    // -----------------------------------------------------------------------------------------------------------------

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    [HarmonyPrefix]
    public static void FixedUpdatePrefix(PlayerControl __instance)
    {
        if (__instance.AmOwner)
            _killTimerBefore = __instance.killTimer;
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    [HarmonyPostfix]
    public static void FixedUpdatePostfix(PlayerControl __instance)
    {
        if (__instance.AmOwner && __instance.killTimer < _killTimerBefore && PausesWhileBusy(__instance))
            __instance.SetKillTimer(_killTimerBefore);
    }
}
