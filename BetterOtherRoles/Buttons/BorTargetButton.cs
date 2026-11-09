using System.Globalization;
using BetterOtherRoles.Modules.Cooldowns;
using MiraAPI.Hud;
using MiraAPI.Utilities;
using UnityEngine;

namespace BetterOtherRoles.Buttons;

/// <summary>
///     Base class for BetterOtherRoles buttons with a target.
///     Same as <see cref="CustomActionButton{T}"/>, except that:
///     - the cooldown shadow starts full from the value the countdown started at (not from <see cref="CustomActionButton.Cooldown"/>);
///     - kill buttons count down only when the impostors' kill timer does (<see cref="FollowsKillTimer"/>);
///     - the cooldown pauses in a vent and while the player is busy, as the impostors' one (<see cref="BorCooldowns"/>).
/// </summary>
/// <typeparam name="T">The type of the target object.</typeparam>
[MiraIgnore]
public abstract class BorTargetButton<T> : CustomActionButton<T>
    where T : MonoBehaviour
{
    private float _lastTimer;
    private float _cooldownStart;

    /// <summary>
    ///     Gets a value indicating whether the cooldown only runs when the vanilla kill timer runs
    ///     (paused during the intro, minigames, ...), to stay in sync with the impostors.
    /// </summary>
    protected virtual bool FollowsKillTimer => false;

    public override void FixedUpdateHandler(PlayerControl playerControl)
    {
        if (Timer >= 0)
        {
            // An effect keeps running; a cooldown pauses in a vent (always) and while busy (BorCooldowns).
            var effectRunning = HasEffect && EffectActive;
            if (!TimerPaused &&
                (effectRunning || (!playerControl.inVent && !BorCooldowns.PausesWhileBusy(playerControl))) &&
                (!FollowsKillTimer || playerControl.IsKillTimerEnabled))
                Timer -= Time.deltaTime;
        }
        else if (HasEffect && EffectActive && EffectDuration > 0)
        {
            EffectActive = false;
            Timer = Cooldown;
            OnEffectEnd();
        }

        // A timer going up means a new countdown: it becomes the reference of the shadow.
        if (Timer > _lastTimer)
            _cooldownStart = Timer;

        _lastTimer = Timer;

        if (Button)
        {
            if (CanUse())
            {
                Button!.SetEnabled();
            }
            else
            {
                Button!.SetDisabled();
            }

            if (EffectActive && EffectDuration > 0)
            {
                Button.SetFillUp(Timer, EffectDuration);
                BorCooldownText.SetText(Button, BorCooldownText.UsesDecimals(Timer)
                    ? BorCooldownText.Format(Timer)
                    : Timer.ToString(CooldownTimerFormatString, NumberFormatInfo.InvariantInfo));
                Button.cooldownTimerText.gameObject.SetActive(true);
            }
            else
            {
                Button.SetCooldownFormat(Timer, _cooldownStart > 0f ? _cooldownStart : Cooldown, CooldownTimerFormatString);
                BorCooldownText.Apply(Button, Timer);
            }
        }

        FixedUpdate(playerControl);
    }
}
