using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Translation;
using MiraAPI.Utilities;

namespace BetterOtherRoles.Options;

/// <summary>
///     Cooldown options of the buttons: the cooldown after each use and each meeting, and under it the start cooldown,
///     used once when the game starts (end of the intro). From "Instant" (0) to 120 s, by 2.5 s.
/// </summary>
public static class BorCooldownOptions
{
    public const float Min = 0f;
    public const float Max = 120f;
    public const float Step = 2.5f;

    /// <summary>
    ///     Start cooldown by default: the 10 s of the game for the kill of the impostors.
    /// </summary>
    public const float DefaultStart = 10f;

    /// <summary>
    ///     Gets the text shown for 0 ("Instant"). MiraAPI shows it as given: in the language of the game when the mod loads.
    /// </summary>
    public static string InstantText => MiraLocaleManager.Get("BetterOtherRoles.Setting.Instant", "Instant");

    /// <summary>
    ///     Cooldown after each use and each meeting.
    /// </summary>
    /// <param name="key">Locale key of the option.</param>
    /// <param name="defaultValue">Cooldown by default.</param>
    /// <param name="visible">When the option is shown (always by default).</param>
    /// <returns>The option.</returns>
    public static ModdedNumberOption Cooldown(string key, float defaultValue, Func<bool>? visible = null) =>
        Make(key, defaultValue, visible);

    /// <summary>
    ///     Cooldown when the game starts, then the normal one.
    /// </summary>
    /// <param name="key">Locale key of the option.</param>
    /// <param name="visible">When the option is shown (always by default).</param>
    /// <param name="changed">Called when the value changes (none by default).</param>
    /// <returns>The option.</returns>
    public static ModdedNumberOption StartCooldown(string key, Func<bool>? visible = null, Action<float>? changed = null)
    {
        var option = Make(key, DefaultStart, visible);
        if (changed != null) option.ChangedEvent = changed;
        return option;
    }

    private static ModdedNumberOption Make(string key, float defaultValue, Func<bool>? visible)
    {
        var option = new ModdedNumberOption(key, defaultValue, Min, Max, Step, InstantText, "#", MiraNumberSuffixes.Seconds);
        if (visible != null) option.Visible = visible;
        return option;
    }
}
