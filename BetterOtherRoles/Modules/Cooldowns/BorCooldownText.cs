using System.Globalization;
using BetterOtherRoles.LocalSettings;
using MiraAPI.LocalSettings;
using UnityEngine;

namespace BetterOtherRoles.Modules.Cooldowns;

/// <summary>
///     Text of the button cooldowns with <see cref="BorLocalSettingsTab.CooldownDisplay"/>: whole seconds as the game,
///     or tenths of a second under 10 s or everywhere. Tenths are rounded up, as the game does with whole seconds,
///     so it never shows 0.0 while the button is cooling down. Used by the game buttons
///     (<see cref="Patches.Misc.ActionButtonCooldownPatches"/>) and the BetterOtherRoles buttons
///     (<see cref="Buttons.BorTargetButton{T}"/>).
/// </summary>
public static class BorCooldownText
{
    private const float DecimalsLimit = 10f;

    /// <summary>
    ///     Gets whether a cooldown is shown with tenths of a second.
    /// </summary>
    /// <param name="timer">The time left, in seconds.</param>
    /// <returns>True with tenths, false with whole seconds (game text).</returns>
    public static bool UsesDecimals(float timer)
    {
        return LocalSettingsTabSingleton<BorLocalSettingsTab>.Instance?.CooldownDisplay.Value switch
        {
            CooldownDisplay.Decimals => true,
            // From 9.9 (rounded up): 11, 10, then 9.9, never 10.0.
            CooldownDisplay.DecimalsUnder10 => timer <= DecimalsLimit - 0.1f,
            _ => false,
        };
    }

    /// <summary>
    ///     Gets the text of a cooldown with tenths of a second.
    /// </summary>
    /// <param name="timer">The time left, in seconds.</param>
    /// <returns>The time left, rounded up to the next tenth.</returns>
    public static string Format(float timer)
    {
        return (Mathf.Ceil(timer * 10f) / 10f).ToString("0.0", NumberFormatInfo.InvariantInfo);
    }

    /// <summary>
    ///     Writes the cooldown of a button with tenths, when the setting asks for it and the button is cooling down.
    /// </summary>
    /// <param name="button">The button.</param>
    /// <param name="timer">The time left, in seconds.</param>
    public static void Apply(ActionButton button, float timer)
    {
        if (!UsesDecimals(timer) || !button.isCoolingDown || !button.cooldownTimerText) return;

        SetText(button, Format(timer));
    }

    /// <summary>
    ///     Writes the cooldown text of a button on one line: the game box is narrow, "18.9" would wrap.
    /// </summary>
    /// <param name="button">The button.</param>
    /// <param name="text">The cooldown text.</param>
    public static void SetText(ActionButton button, string text)
    {
        button.cooldownTimerText.enableWordWrapping = false;
        button.cooldownTimerText.text = text;
    }
}
