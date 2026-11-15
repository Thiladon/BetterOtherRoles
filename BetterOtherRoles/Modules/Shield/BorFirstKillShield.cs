using System.Collections;
using BetterOtherRoles.Buttons;
using BetterOtherRoles.Modifiers.Universal;
using BetterOtherRoles.Options;
using BetterOtherRoles.Utilities;
using HarmonyLib;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Events.Vanilla.Meeting;
using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Modifiers;
using Reactor.Utilities;
using UnityEngine;

namespace BetterOtherRoles.Modules.Shield;

/// <summary>
///     Shield of the first player murdered in the previous game (BetterOtherRoles 1.7.1 FirstKillShield, with parts of
///     TOU-Mira 1.7.9 FirstShieldEvents; <see cref="ShieldOptions.FirstKillShield"/>):
///     <list type="bullet">
///         <item>the host remembers the first players murdered (murders only, as BOR), in order;</item>
///         <item>when the game starts, the first one still here gets the shield (<see cref="FirstKillShieldModifier"/>),
///         or the player chosen with /shield;</item>
///         <item>the shield ends after its duration, at the first meeting, or the first of both;</item>
///         <item>a kill on the shielded player is cancelled; the attacker may see a flash and get a short kill cooldown.</item>
///     </list>
///     To do with the Bounty Hunter (not ported yet): never give the shielded player as a bounty (BOR).
/// </summary>
[HarmonyPatch]
public static class BorFirstKillShield
{
    // Players kept (TOU: the first four), in case the first ones left the lobby.
    private const int MaxRemembered = 4;

    // Flash of the attacker (TOU-Mira 1.7.9: NeutralWiki color, half transparent).
    private static readonly Color FlashColor = new(0.65f, 0.65f, 0.65f, 0.5f);
    private const float FlashTime = 0.5f;

    // Names of the first players murdered in the game (host), for the next one.
    private static readonly List<string> Murdered = [];

    // Player chosen with /shield, for the next game (host).
    private static string? _chosen;

    // Time left of the shield (host), 0 without timer.
    private static float _timeLeft;

    private static ShieldOptions Options => OptionGroupSingleton<ShieldOptions>.Instance;

    /// <summary>
    ///     Gets the color of the outline: the rainbow of BOR.
    /// </summary>
    public static Color OutlineColor => Color.HSVToRGB(Mathf.PingPong(Time.time * 0.3f, 1f), 1f, 1f);

    /// <summary>
    ///     Gets the name of the player shielded at the start of the next game, if known now (host).
    /// </summary>
    public static string? NextShielded => _chosen ?? Murdered.FirstOrDefault();

    /// <summary>
    ///     Chooses the player shielded at the start of the next game (/shield).
    /// </summary>
    /// <param name="name">The name of the player.</param>
    public static void Choose(string name)
    {
        _chosen = name;
    }

    /// <summary>
    ///     Rainbow outline of the shielded player, hidden when nobody can tell who it is (mushroom mix-up).
    /// </summary>
    /// <param name="player">The shielded player.</param>
    public static void ShowOutline(PlayerControl player)
    {
        var hidden = player.CurrentOutfitType == PlayerOutfitType.MushroomMixup;
        player.cosmetics.SetOutline(!hidden, new Il2CppSystem.Nullable<Color>(OutlineColor));
    }

    /// <summary>
    ///     First players murdered (host): names, as the players get new ids in the next game.
    /// </summary>
    [RegisterEvent]
    public static void AfterMurderHandler(AfterMurderEvent @event)
    {
        if (!AmongUsClient.Instance.AmHost || !@event.Target || @event.Target.Data == null) return;

        var name = @event.Target.Data.PlayerName;
        if (Murdered.Count >= MaxRemembered || Murdered.Contains(name)) return;

        Murdered.Add(name);
    }

    /// <summary>
    ///     Start of the game: the host gives the shield. After a meeting: the shield ends if it ends at the first meeting.
    /// </summary>
    [RegisterEvent]
    public static void RoundStartHandler(RoundStartEvent @event)
    {
        if (!@event.TriggeredByIntro || !AmongUsClient.Instance.AmHost) return;

        // The players murdered in the game that just ended, then a new list for this one.
        List<string> candidates = _chosen != null ? [_chosen] : [.. Murdered];
        Murdered.Clear();
        _chosen = null;
        _timeLeft = 0f;
        if (!Options.FirstKillShield.Value) return;

        var found = candidates
            .Select(name => PlayerControl.AllPlayerControls.ToArray().FirstOrDefault(x => x && x.Data != null && !x.Data.Disconnected && x.Data.PlayerName == name))
            .FirstOrDefault(x => x);
        if (!found) return;

        var player = found!;
        player.RpcAddModifier<FirstKillShieldModifier>();
        if ((ShieldExpiry)Options.FirstKillShieldExpiry.Value != ShieldExpiry.FirstMeeting)
            _timeLeft = Options.FirstKillShieldDuration.Value;
    }

    [RegisterEvent]
    public static void StartMeetingHandler(StartMeetingEvent @event)
    {
        if (!AmongUsClient.Instance.AmHost || (ShieldExpiry)Options.FirstKillShieldExpiry.Value == ShieldExpiry.Duration) return;

        RemoveShield();
    }

    /// <summary>
    ///     Duration of the shield (host), meetings included (BOR).
    /// </summary>
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    [HarmonyPostfix]
    public static void HudUpdatePostfix()
    {
        if (_timeLeft <= 0f || !AmongUsClient.Instance || !AmongUsClient.Instance.AmHost) return;

        _timeLeft -= Time.deltaTime;
        if (_timeLeft > 0f) return;

        RemoveShield();
    }

    /// <summary>
    ///     Kills on the shielded player cancelled: kill of the game, and kills of the mod (RpcCustomMurder). The event is
    ///     also raised on the attacker's client: the attacker gets the effects there.
    /// </summary>
    [RegisterEvent]
    public static void BeforeMurderHandler(BeforeMurderEvent @event)
    {
        if (@event.IgnoreDefense || @event.Source == @event.Target || !@event.Target || !@event.Target.HasModifier<FirstKillShieldModifier>()) return;

        @event.Cancel();
        if (@event.Source.AmOwner) Stopped(@event.Target);
    }

    // Attacker: the shield animation of the game and / or a flash, as chosen; short kill cooldown, if chosen; otherwise
    // the kill stays ready (BOR).
    private static void Stopped(PlayerControl target)
    {
        var feedback = (ShieldFeedback)Options.FirstKillShieldFeedback.Value;
        if (feedback is ShieldFeedback.Animation or ShieldFeedback.FlashAndAnimation) BorShieldEffects.PlayFailedKill(target);
        if (feedback is ShieldFeedback.Flash or ShieldFeedback.FlashAndAnimation) Coroutines.Start(CoFlash());

        // Kill buttons of the mod (the Sheriff): their cooldown starts again on the click.
        var cooldown = Options.FirstKillShieldCooldown.Value ? Options.FirstKillShieldCooldownValue.Value : 0f;
        var role = PlayerControl.LocalPlayer.Data.Role;
        foreach (var button in CustomButtonManager.Buttons.Where(x => x is IBorKillButton && x.Enabled(role)))
        {
            button.SetTimer(cooldown);
        }

        if (Options.FirstKillShieldCooldown.Value) PlayerControl.LocalPlayer.SetKillTimer(cooldown);
    }

    private static void RemoveShield()
    {
        _timeLeft = 0f;
        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (player && player.HasModifier<FirstKillShieldModifier>()) player.RpcRemoveModifier<FirstKillShieldModifier>();
        }
    }

    private static IEnumerator CoFlash()
    {
        var screen = HudManager.Instance ? HudManager.Instance.FullScreen : null;
        if (!screen) yield break;

        var wasActive = screen!.gameObject.activeSelf;
        screen.gameObject.SetActive(true);
        for (var time = 0f; time < FlashTime; time += Time.deltaTime)
        {
            if (!screen) yield break;

            screen.color = new Color(FlashColor.r, FlashColor.g, FlashColor.b, FlashColor.a * (1f - (time / FlashTime)));
            yield return null;
        }

        if (!screen) yield break;

        screen.color = Color.clear;
        screen.gameObject.SetActive(wasActive);
    }
}
