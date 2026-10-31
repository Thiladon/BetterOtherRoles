using BetterOtherRoles.Roles.Crewmate;
using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Translation;
using MiraAPI.Utilities;

namespace BetterOtherRoles.Options.Roles.Crewmate;

/// <summary>
///     Sheriff options, taken from BetterOtherRoles 1.7.1 (ids 100 to 103).
///     The spawn chance is handled by MiraAPI (<see cref="SheriffRole.Configuration"/>).
/// </summary>
public sealed class SheriffOptions : AbstractRoleOptionGroup<SheriffRole>
{
    public override string GroupName => MiraLocaleManager.Get("BetterOtherRoles.Role.Sheriff", "Sheriff");

    [ModdedNumberOption("BetterOtherRoles.Option.Sheriff.Cooldown", 10f, 60f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float Cooldown { get; set; } = 30f;

    [ModdedToggleOption("BetterOtherRoles.Option.Sheriff.CanKillNeutrals")]
    public bool CanKillNeutrals { get; set; } = false;

    [ModdedToggleOption("BetterOtherRoles.Option.Sheriff.HasDeputy")]
    public bool HasDeputy { get; set; } = false;

    public ModdedNumberOption DeputyChance { get; } =
        new("BetterOtherRoles.Option.Sheriff.DeputyChance", 100f, 0f, 100f, 10f, MiraNumberSuffixes.Percent)
        {
            Visible = () => OptionGroupSingleton<SheriffOptions>.Instance.HasDeputy,
        };

    /// <summary>
    ///     Gets the options of the Deputy, shown on the Deputy wiki page instead of the Sheriff one.
    /// </summary>
    [MiraIgnore]
    public IModdedOption[] DeputyOptions =>
        [DeputyHandcuffs, DeputyHandcuffCooldown, DeputyHandcuffDuration, DeputyKnowsSheriff, DeputyPromotion, DeputyKeepsHandcuffs];

    // Deputy options (BetterOtherRoles 1.7.1, ids 104 to 109).
    public ModdedNumberOption DeputyHandcuffs { get; } =
        new("BetterOtherRoles.Option.Deputy.Handcuffs", 3f, 1f, 10f, 1f, MiraNumberSuffixes.None, "0")
        {
            Visible = () => OptionGroupSingleton<SheriffOptions>.Instance.HasDeputy,
        };

    public ModdedNumberOption DeputyHandcuffCooldown { get; } =
        new("BetterOtherRoles.Option.Deputy.HandcuffCooldown", 30f, 10f, 60f, 2.5f, MiraNumberSuffixes.Seconds)
        {
            Visible = () => OptionGroupSingleton<SheriffOptions>.Instance.HasDeputy,
        };

    public ModdedNumberOption DeputyHandcuffDuration { get; } =
        new("BetterOtherRoles.Option.Deputy.HandcuffDuration", 15f, 5f, 60f, 2.5f, MiraNumberSuffixes.Seconds)
        {
            Visible = () => OptionGroupSingleton<SheriffOptions>.Instance.HasDeputy,
        };

    public ModdedToggleOption DeputyKnowsSheriff { get; } =
        new("BetterOtherRoles.Option.Deputy.KnowsSheriff", true)
        {
            Visible = () => OptionGroupSingleton<SheriffOptions>.Instance.HasDeputy,
        };

    public ModdedEnumOption DeputyPromotion { get; } =
        new("BetterOtherRoles.Option.Deputy.Promotion", (int)DeputyPromotionMode.Off, typeof(DeputyPromotionMode),
            ["BetterOtherRoles.Option.Deputy.PromotionOff", "BetterOtherRoles.Option.Deputy.PromotionImmediately", "BetterOtherRoles.Option.Deputy.PromotionAfterMeeting"])
        {
            Visible = () => OptionGroupSingleton<SheriffOptions>.Instance.HasDeputy,
        };

    public ModdedToggleOption DeputyKeepsHandcuffs { get; } =
        new("BetterOtherRoles.Option.Deputy.KeepsHandcuffs", true)
        {
            Visible = () => OptionGroupSingleton<SheriffOptions>.Instance.HasDeputy &&
                            OptionGroupSingleton<SheriffOptions>.Instance.DeputyPromotion.Value != (int)DeputyPromotionMode.Off,
        };
}

/// <summary>
///     When the Deputy becomes Sheriff after the Sheriff's death.
/// </summary>
public enum DeputyPromotionMode
{
    Off,
    Immediately,
    AfterMeeting,
}
