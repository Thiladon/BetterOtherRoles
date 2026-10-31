using System.Collections;
using BetterOtherRoles.Options.Roles.Crewmate;
using BetterOtherRoles.Roles.Crewmate;
using BetterOtherRoles.Utilities;
using HarmonyLib;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Events.Vanilla.Player;
using MiraAPI.GameOptions;
using MiraAPI.Roles;
using Reactor.Networking.Attributes;
using Reactor.Utilities;

namespace BetterOtherRoles.Modules.Roles;

/// <summary>
///     Promotion of the Deputy to Sheriff when the Sheriff is dead or gone, as in BetterOtherRoles 1.7.1
///     (PlayerControlFixedUpdatePatch.deputyCheckPromotion, RPCProcedure.deputyPromotes).
///     BOR checked it on every update; here it is only checked when it can change:
///     - "Immediately": when the Sheriff dies (kill, misfire, ejection) or leaves the game;
///     - "After meeting": when the game is back after a meeting.
/// </summary>
[HarmonyPatch]
public static class DeputyPromotion
{
    /// <summary>
    ///     Gets the Deputy who was promoted to Sheriff this game (BOR: Sheriff.formerDeputy).
    /// </summary>
    public static PlayerControl? FormerDeputy { get; private set; }

    /// <summary>
    ///     Gets the Sheriff replaced by the promoted Deputy this game (BOR: Sheriff.formerSheriff).
    ///     <see langword="null"/> if they left the game.
    /// </summary>
    public static PlayerControl? FormerSheriff { get; private set; }

    // Last Sheriff who died or left, on every client: once dead, their role is a ghost role.
    private static PlayerControl? _lostSheriff;

    private static bool _checkPending;

    private static DeputyPromotionMode Mode => (DeputyPromotionMode)OptionGroupSingleton<SheriffOptions>.Instance.DeputyPromotion.Value;

    /// <summary>
    ///     A player dies (kill, Sheriff misfire, ejection): their role is still the living one before Die.
    /// </summary>
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Die))]
    [HarmonyPrefix]
    public static void PlayerControlDiePrefix(PlayerControl __instance)
    {
        if (__instance.Data?.Role is SheriffRole)
        {
            _lostSheriff = __instance;
        }
    }

    [RegisterEvent]
    public static void PlayerDeathHandler(PlayerDeathEvent @event)
    {
        if (@event.Player == _lostSheriff && Mode == DeputyPromotionMode.Immediately)
        {
            RequestCheck();
        }
    }

    [RegisterEvent]
    public static void PlayerLeaveHandler(PlayerLeaveEvent @event)
    {
        var player = @event.ClientData?.Character;
        if (!player || player!.Data?.Role is not SheriffRole)
        {
            return;
        }

        // Gone from the game: no former Sheriff to keep.
        _lostSheriff = null;
        if (Mode == DeputyPromotionMode.Immediately)
        {
            RequestCheck();
        }
    }

    /// <summary>
    ///     Start of the game: reset. After a meeting: promotion "after meeting" (BOR: ExileController wrap up).
    /// </summary>
    [RegisterEvent]
    public static void RoundStartHandler(RoundStartEvent @event)
    {
        if (@event.TriggeredByIntro)
        {
            FormerDeputy = null;
            FormerSheriff = null;
            _lostSheriff = null;
            return;
        }

        if (Mode == DeputyPromotionMode.AfterMeeting)
        {
            RequestCheck();
        }
    }

    /// <summary>
    ///     Promotes the Deputy, for everyone.
    /// </summary>
    [MethodRpc((uint)BorRpc.DeputyPromotes)]
    public static void RpcPromote(PlayerControl deputy)
    {
        // Kept before the role change: the Deputy's buttons use them (handcuffs kept after the promotion).
        FormerDeputy = deputy;
        FormerSheriff = _lostSheriff;

        deputy.ChangeRole(RoleId.Get<SheriffRole>());
        Info($"[Deputy] {deputy.Data.PlayerName} promoted to Sheriff (former Sheriff: {(FormerSheriff ? FormerSheriff!.Data.PlayerName : "gone")})");
    }

    /// <summary>
    ///     Checks the promotion on the Deputy's side, once there is no meeting nor exile screen: a role change
    ///     while they are there would hide the HUD of the new role.
    /// </summary>
    private static void RequestCheck()
    {
        if (_checkPending || PlayerControl.LocalPlayer?.Data?.Role is not DeputyRole)
        {
            return;
        }

        _checkPending = true;
        Coroutines.Start(CoCheckPromotion());
    }

    private static IEnumerator CoCheckPromotion()
    {
        // At least one frame: the death or the departure is applied, and RoundStartEvent comes during ReEnableGameplay.
        do
        {
            yield return null;
        }
        while (MeetingHud.Instance || ExileController.Instance);

        _checkPending = false;
        CheckPromotion();
    }

    private static void CheckPromotion()
    {
        var local = PlayerControl.LocalPlayer;
        if (!local || local.Data == null || local.Data.IsDead || local.Data.Role is not DeputyRole)
        {
            return;
        }

        if (!PlayerControl.AllPlayerControls.ToArray().Any(IsAliveSheriff))
        {
            RpcPromote(local);
        }
    }

    private static bool IsAliveSheriff(PlayerControl player)
    {
        return player && player.Data != null && !player.Data.IsDead && !player.Data.Disconnected && player.Data.Role is SheriffRole;
    }
}
