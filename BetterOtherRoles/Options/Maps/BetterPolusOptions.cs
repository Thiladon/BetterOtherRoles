using BetterOtherRoles.Patches.Options;
using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Translation;
using MiraAPI.Utilities;

namespace BetterOtherRoles.Options.Maps;

/// <summary>
///     Better Polus (BetterOtherRoles 1.7.1 Modules/BetterPolus.cs, TOU-Mira 1.7.9 BetterPolusOptions): one main toggle,
///     then each change of the map, shown below it when it is on (<see cref="Patches.BetterMaps.BetterPolusPatches"/>).
/// </summary>
public sealed class BetterPolusOptions : AbstractOptionGroup
{
    public override string GroupName => MiraLocaleManager.Get("BetterOtherRoles.Options.Groups.BetterPolus", "Better Polus");

    public override uint GroupPriority => 4;

    public BetterPolusOptions()
    {
        VentNetwork.SetNotificationParent(Enabled);
        BathroomVent.SetNotificationParent(Enabled);
        VitalsInLaboratory.SetNotificationParent(Enabled);
        ColdTemperatureOutside.SetNotificationParent(Enabled);
        SwapWifiAndChartCourse.SetNotificationParent(Enabled);
        Theme.SetNotificationParent(Enabled);
        ReactorSabotage.SetNotificationParent(Enabled);
    }

    /// <summary>
    ///     Gets whether Better Polus is on: the options below are only applied with it.
    /// </summary>
    public ModdedToggleOption Enabled { get; } = new("BetterOtherRoles.Option.BetterPolus.Enabled", false);

    /// <summary>
    ///     Gets whether the vents get two new links: Electrical ↔ the building outside it, Science building ↔ Storage (BOR).
    /// </summary>
    public ModdedToggleOption VentNetwork { get; } =
        new("BetterOtherRoles.Option.BetterPolus.VentNetwork", true) { Visible = IsOn };

    /// <summary>
    ///     Gets whether the vent of the bathroom is moved out of the stalls (TOU).
    /// </summary>
    public ModdedToggleOption BathroomVent { get; } =
        new("BetterOtherRoles.Option.BetterPolus.BathroomVent", false) { Visible = IsOn };

    /// <summary>
    ///     Gets whether the vitals are moved to the laboratory, with a copy of the screen of the admin room (BOR). The cold
    ///     temperature, at their new place, is then always moved outside.
    /// </summary>
    public ModdedToggleOption VitalsInLaboratory { get; } =
        new("BetterOtherRoles.Option.BetterPolus.VitalsInLaboratory", true) { Visible = IsOn };

    /// <summary>
    ///     Gets whether the cold temperature (Record Temperature) is moved outside, in the valley below the
    ///     laboratory (BOR). Always moved with the vitals in the laboratory.
    /// </summary>
    public ModdedToggleOption ColdTemperatureOutside { get; } =
        new("BetterOtherRoles.Option.BetterPolus.ColdTemperatureOutside", true)
        {
            Visible = () => IsOn() && !OptionGroupSingleton<BetterPolusOptions>.Instance.VitalsInLaboratory.Value,
        };

    /// <summary>
    ///     Gets whether Reboot Wifi and Chart Course swap places: Wifi in the dropship, Chart Course in Comms (BOR).
    /// </summary>
    public ModdedToggleOption SwapWifiAndChartCourse { get; } =
        new("BetterOtherRoles.Option.BetterPolus.SwapWifiAndChartCourse", true) { Visible = IsOn };

    /// <summary>
    ///     Gets the decorations of the map: chosen by the game (date), none, or Halloween (TOU, <see cref="PolusTheme"/>).
    /// </summary>
    public ModdedEnumOption Theme { get; } =
        new("BetterOtherRoles.Option.BetterPolus.Theme", (int)PolusTheme.Auto, typeof(PolusTheme),
            ["BetterOtherRoles.Option.BetterPolus.ThemeAuto", "BetterOtherRoles.Option.BetterPolus.ThemeNormal", "BetterOtherRoles.Option.BetterPolus.ThemeHalloween"])
        {
            Visible = IsOn,
        };

    /// <summary>
    ///     Gets the countdown of the reactor sabotage (seismic stabilizers of the laboratory), 60 s in the game (BOR:
    ///     Polus Reactor Cooldown).
    /// </summary>
    public ModdedNumberOption ReactorSabotage { get; } =
        new("BetterOtherRoles.Option.BetterPolus.ReactorSabotage", 40f, 10f, 90f, 2.5f, MiraNumberSuffixes.Seconds)
        {
            Visible = IsOn,
        };

    /// <summary>
    ///     Gets whether Better Polus is on.
    /// </summary>
    /// <returns>Whether the main toggle is on.</returns>
    public static bool IsOn() => OptionGroupSingleton<BetterPolusOptions>.Instance.Enabled.Value;
}

/// <summary>
///     Decorations of Polus.
/// </summary>
public enum PolusTheme
{
    Auto,
    Normal,
    Halloween,
}
