using BetterOtherRoles.Patches.Options;
using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Translation;
using MiraAPI.Utilities;

namespace BetterOtherRoles.Options.Maps;

/// <summary>
///     Durations of the sabotages of each map (BetterOtherRoles 1.7.1 Polus Reactor Cooldown, TOU-Mira 1.7.9 Better
///     Maps sabotage countdowns), the values of the game by default. Shown in the vanilla game settings tab, only for
///     the map that can be played (<see cref="SabotageSettingsPatches"/>), not in the tab of BetterOtherRoles.
/// </summary>
public sealed class SabotageOptions : AbstractOptionGroup
{
    // Critical sabotages (countdown before the impostors win), and the mixup of The Fungle (until it heals).
    private const float CriticalMin = 10f;
    private const float CriticalMax = 120f;
    private const float CriticalStep = 1f;
    private const float MixupMin = 5f;
    private const float MixupMax = 60f;
    private const float MixupStep = 1f;

    public override string GroupName => MiraLocaleManager.Get("BetterOtherRoles.Setting.Sabotages", "Sabotages");

    public override uint GroupPriority => 100;

    public override Func<bool> GroupVisible => () => false;

    public override bool ShowInMatchInfoGuide => false;

    public ModdedNumberOption SkeldReactor { get; } = Critical("SkeldReactor", 30f);

    public ModdedNumberOption SkeldOxygen { get; } = Critical("SkeldOxygen", 30f);

    public ModdedNumberOption MiraReactor { get; } = Critical("MiraReactor", 45f);

    public ModdedNumberOption MiraOxygen { get; } = Critical("MiraOxygen", 45f);

    public ModdedNumberOption PolusSeismic { get; } = Critical("PolusSeismic", 60f);

    public ModdedNumberOption AirshipCrash { get; } = Critical("AirshipCrash", 90f);

    public ModdedNumberOption FungleReactor { get; } = Critical("FungleReactor", 60f);

    public ModdedNumberOption FungleMixup { get; } =
        New("FungleMixup", 10f, MixupMin, MixupMax, MixupStep);

    /// <summary>
    ///     Gets each option with the maps it is used on (<see cref="MapNames"/>), in the order shown. The Skeld and dlekS
    ///     share their options.
    /// </summary>
    public IEnumerable<(MapNames[] Maps, ModdedNumberOption Option)> All =>
    [
        ([MapNames.Skeld, MapNames.Dleks], SkeldReactor),
        ([MapNames.Skeld, MapNames.Dleks], SkeldOxygen),
        ([MapNames.MiraHQ], MiraReactor),
        ([MapNames.MiraHQ], MiraOxygen),
        ([MapNames.Polus], PolusSeismic),
        ([MapNames.Airship], AirshipCrash),
        ([MapNames.Fungle], FungleReactor),
        ([MapNames.Fungle], FungleMixup),
    ];

    /// <summary>
    ///     Gets the duration of the reactor of a map (seismic stabilizers on Polus), or null when it has none here.
    /// </summary>
    public float? Reactor(MapNames map) => map switch
    {
        MapNames.Skeld or MapNames.Dleks => SkeldReactor.Value,
        MapNames.MiraHQ => MiraReactor.Value,
        MapNames.Polus => PolusSeismic.Value,
        MapNames.Fungle => FungleReactor.Value,
        _ => null,
    };

    /// <summary>
    ///     Gets the duration of the oxygen of a map, or null when it has none here.
    /// </summary>
    public float? Oxygen(MapNames map) => map switch
    {
        MapNames.Skeld or MapNames.Dleks => SkeldOxygen.Value,
        MapNames.MiraHQ => MiraOxygen.Value,
        _ => null,
    };

    private static ModdedNumberOption Critical(string name, float defaultValue) =>
        New(name, defaultValue, CriticalMin, CriticalMax, CriticalStep);

    private static ModdedNumberOption New(string name, float defaultValue, float min, float max, float step) =>
        new($"BetterOtherRoles.Setting.Sabotage.{name}", defaultValue, min, max, step, MiraNumberSuffixes.Seconds, "0")
        {
            ChangedEvent = _ => SabotageSettingsPatches.RefreshValues(),
        };
}
