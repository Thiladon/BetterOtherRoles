using BetterOtherRoles.Patches.Options;
using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Translation;

namespace BetterOtherRoles.Options.Maps;

/// <summary>
///     Better Skeld (BetterOtherRoles 1.7.1 Modules/BetterSkeld.cs): one main toggle, then each change of the map, shown
///     below it when it is on (<see cref="Patches.BetterMaps.BetterSkeldPatches"/>).
/// </summary>
public sealed class BetterSkeldOptions : AbstractOptionGroup
{
    public override string GroupName => MiraLocaleManager.Get("BetterOtherRoles.Options.Groups.BetterSkeld", "Better Skeld");

    public override uint GroupPriority => 7;

    public BetterSkeldOptions()
    {
        VentNetwork.SetNotificationParent(Enabled);
        DisableAdminTable.SetNotificationParent(Enabled);
        Vitals.SetNotificationParent(Enabled);
        Theme.SetNotificationParent(Enabled);
    }

    /// <summary>
    ///     Gets whether Better Skeld is on: the options below are only applied with it.
    /// </summary>
    public ModdedToggleOption Enabled { get; } = new("BetterOtherRoles.Option.BetterSkeld.Enabled", true);

    /// <summary>
    ///     Gets the network of the vents (<see cref="SkeldVentNetwork"/>): the one of the game, the one of BOR (halves
    ///     of the map linked) or the four groups of TOU-Mira 1.7.9.
    /// </summary>
    public ModdedEnumOption VentNetwork { get; } =
        new("BetterOtherRoles.Option.BetterSkeld.VentNetworkMode", (int)SkeldVentNetwork.Bor, typeof(SkeldVentNetwork),
            ["BetterOtherRoles.Option.BetterSkeld.VentNetworkNormal", "BetterOtherRoles.Option.BetterSkeld.VentNetworkBor",
                "BetterOtherRoles.Option.BetterSkeld.VentNetworkFourGroups"])
        {
            Visible = IsOn,
        };

    /// <summary>
    ///     Gets whether the admin table can no longer be used (BOR: Enable Admin Table, off by default).
    /// </summary>
    public ModdedToggleOption DisableAdminTable { get; } =
        new("BetterOtherRoles.Option.BetterSkeld.DisableAdminTable", true) { Visible = IsOn };

    /// <summary>
    ///     Gets whether the vitals of Polus are added to The Skeld (BOR).
    /// </summary>
    public ModdedToggleOption Vitals { get; } =
        new("BetterOtherRoles.Option.BetterSkeld.Vitals", true) { Visible = IsOn };

    /// <summary>
    ///     Gets the decorations of the map: chosen by the game (date), none, birthday, Halloween or TV show
    ///     (TOU-Mira 1.7.9 MapTheme, <see cref="SkeldTheme"/>).
    /// </summary>
    public ModdedEnumOption Theme { get; } =
        new("BetterOtherRoles.Option.BetterSkeld.Theme", (int)SkeldTheme.Auto, typeof(SkeldTheme),
            ["BetterOtherRoles.Option.BetterSkeld.ThemeAuto", "BetterOtherRoles.Option.BetterSkeld.ThemeNormal",
                "BetterOtherRoles.Option.BetterSkeld.ThemeBirthday", "BetterOtherRoles.Option.BetterSkeld.ThemeHalloween",
                "BetterOtherRoles.Option.BetterSkeld.ThemeTvShow"])
        {
            Visible = IsOn,
        };

    /// <summary>
    ///     Gets whether Better Skeld is on.
    /// </summary>
    /// <returns>Whether the main toggle is on.</returns>
    public static bool IsOn() => OptionGroupSingleton<BetterSkeldOptions>.Instance.Enabled.Value;
}

/// <summary>
///     Networks of the vents of The Skeld.
/// </summary>
public enum SkeldVentNetwork
{
    /// <summary>The links of the game.</summary>
    Normal,

    /// <summary>BOR: the halves of the map linked (Weapons ↔ Cafeteria, Admin ↔ Shields, Upper Engine ↔ MedBay...).</summary>
    Bor,

    /// <summary>TOU: engines and reactor in one group, Weapons, Navigation and Shields in another.</summary>
    FourGroups,
}

/// <summary>
///     Decorations of The Skeld.
/// </summary>
public enum SkeldTheme
{
    Auto,
    Normal,
    Birthday,
    Halloween,
    TvShow,
}
