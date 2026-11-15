using BetterOtherRoles.Patches.Options;
using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Translation;
using MiraAPI.Utilities;

namespace BetterOtherRoles.Options;

/// <summary>
///     Options of the shield of the first player murdered in the previous game (<see cref="Modules.Shield.BorFirstKillShield"/>).
/// </summary>
public sealed class ShieldOptions : AbstractOptionGroup
{
    public override string GroupName => MiraLocaleManager.Get("BetterOtherRoles.Options.Groups.Shield", "Shield Options");

    public ShieldOptions()
    {
        FirstKillShieldExpiry.SetNotificationParent(FirstKillShield);
        FirstKillShieldDuration.SetNotificationParent(FirstKillShield);
        FirstKillShieldInfo.SetNotificationParent(FirstKillShield);
        FirstKillShieldFeedback.SetNotificationParent(FirstKillShield);
        FirstKillShieldCooldown.SetNotificationParent(FirstKillShield);
        FirstKillShieldCooldownValue.SetNotificationParent(FirstKillShield);
    }

    public override uint GroupPriority => 4;

    /// <summary>
    ///     Gets whether the first player murdered in the previous game is shielded at the start of this one
    ///     (BetterOtherRoles 1.7.1 Shield Last Game First Kill, <see cref="Modules.Shield.BorFirstKillShield"/>).
    /// </summary>
    public ModdedToggleOption FirstKillShield { get; } = new("BetterOtherRoles.Option.Gameplay.FirstKillShield", true);

    /// <summary>
    ///     Gets when the shield ends: after its duration, at the first meeting, or the first of both.
    /// </summary>
    public ModdedEnumOption FirstKillShieldExpiry { get; } =
        new("BetterOtherRoles.Option.Gameplay.FirstKillShieldExpiry", (int)ShieldExpiry.FirstMeeting, typeof(ShieldExpiry),
            ["BetterOtherRoles.Option.Gameplay.FirstKillShieldExpiryDuration", "BetterOtherRoles.Option.Gameplay.FirstKillShieldExpiryFirstMeeting", "BetterOtherRoles.Option.Gameplay.FirstKillShieldExpiryBoth"])
        {
            Visible = () => OptionGroupSingleton<ShieldOptions>.Instance.FirstKillShield.Value,
        };

    public ModdedNumberOption FirstKillShieldDuration { get; } =
        new("BetterOtherRoles.Option.Gameplay.FirstKillShieldDuration", 90f, 10f, 300f, 5f, MiraNumberSuffixes.Seconds)
        {
            Visible = () => OptionGroupSingleton<ShieldOptions>.Instance.FirstKillShield.Value &&
                            OptionGroupSingleton<ShieldOptions>.Instance.FirstKillShieldExpiry.Value != (int)ShieldExpiry.FirstMeeting,
        };

    /// <summary>
    ///     Gets whether the shielded player sees the icon and the description of the shield (TOU-Mira 1.7.9).
    /// </summary>
    public ModdedToggleOption FirstKillShieldInfo { get; } =
        new("BetterOtherRoles.Option.Gameplay.FirstKillShieldInfo", true)
        {
            Visible = () => OptionGroupSingleton<ShieldOptions>.Instance.FirstKillShield.Value,
        };

    /// <summary>
    ///     Gets what the attacker sees when the kill is stopped by the shield: nothing, a flash (TOU-Mira 1.7.9), the
    ///     shield animation of the game, or both.
    /// </summary>
    public ModdedEnumOption FirstKillShieldFeedback { get; } =
        new("BetterOtherRoles.Option.Gameplay.FirstKillShieldFeedback", (int)ShieldFeedback.FlashAndAnimation, typeof(ShieldFeedback),
            ["BetterOtherRoles.Option.Gameplay.FirstKillShieldFeedbackNothing", "BetterOtherRoles.Option.Gameplay.FirstKillShieldFeedbackFlash",
                "BetterOtherRoles.Option.Gameplay.FirstKillShieldFeedbackAnimation", "BetterOtherRoles.Option.Gameplay.FirstKillShieldFeedbackBoth"])
        {
            Visible = () => OptionGroupSingleton<ShieldOptions>.Instance.FirstKillShield.Value,
        };

    /// <summary>
    ///     Gets whether the kill of the attacker starts again from a short cooldown when stopped by the shield
    ///     (TOU-Mira 1.7.9 Temp Save Cooldown Reset). Off: the kill is only cancelled, ready again at once (BOR).
    /// </summary>
    public ModdedToggleOption FirstKillShieldCooldown { get; } =
        new("BetterOtherRoles.Option.Gameplay.FirstKillShieldCooldown", false)
        {
            Visible = () => OptionGroupSingleton<ShieldOptions>.Instance.FirstKillShield.Value,
        };

    public ModdedNumberOption FirstKillShieldCooldownValue { get; } =
        new("BetterOtherRoles.Option.Gameplay.FirstKillShieldCooldownValue", 5f, 0f, 15f, 0.5f, MiraNumberSuffixes.Seconds)
        {
            Visible = () => OptionGroupSingleton<ShieldOptions>.Instance.FirstKillShield.Value &&
                            OptionGroupSingleton<ShieldOptions>.Instance.FirstKillShieldCooldown.Value,
        };
}

/// <summary>
///     When the shield of the first player murdered in the previous game ends.
/// </summary>
public enum ShieldExpiry
{
    Duration,
    FirstMeeting,
    DurationAndFirstMeeting,
}

/// <summary>
///     What the attacker sees when a kill is stopped by a shield.
/// </summary>
public enum ShieldFeedback
{
    Nothing,
    Flash,
    Animation,
    FlashAndAnimation,
}
