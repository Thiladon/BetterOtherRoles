using BetterOtherRoles.Patches.Options;
using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Translation;
using MiraAPI.Utilities;

namespace BetterOtherRoles.Options.Maps;

/// <summary>
///     Random map (TOU-Mira 1.7.9 TownOfUsMapOptions, BetterOtherRoles 1.7.1 dynamicMap): a weight per map, and a preset
///     of the host loaded for the game on each map (<see cref="Modules.Presets.BorRandomMap"/>).
///     Left out of the presets: loading the preset of a map must not change the draw.
/// </summary>
public sealed class RandomMapOptions : AbstractOptionGroup
{
    public override string GroupName => MiraLocaleManager.Get("BetterOtherRoles.Options.Groups.RandomMap", "Random Map");

    public override uint GroupPriority => 7;

    public RandomMapOptions()
    {
        PresetPerMap.SetNotificationParent(Enabled);
        foreach (var map in Maps)
        {
            map.Chance.SetNotificationParent(Enabled);
            map.Preset.SetNotificationParent(Enabled);
        }
    }

    /// <summary>
    ///     Gets whether the host draws the map at the start of each game.
    /// </summary>
    public ModdedToggleOption Enabled { get; } = new("BetterOtherRoles.Option.RandomMap.Enabled", false, false);

    /// <summary>
    ///     Gets whether a preset can be chosen for each map (BOR: Use Random Map Setting Presets), shown under it.
    /// </summary>
    public ModdedToggleOption PresetPerMap { get; } =
        new("BetterOtherRoles.Option.RandomMap.PresetPerMap", false, false) { Visible = IsOn };

    public ModdedNumberOption SkeldChance { get; } = NewChance("Skeld");

    public BorPresetOption SkeldPreset { get; } = NewPreset("Skeld", 0);

    public ModdedNumberOption MiraChance { get; } = NewChance("Mira");

    public BorPresetOption MiraPreset { get; } = NewPreset("Mira", 1);

    public ModdedNumberOption PolusChance { get; } = NewChance("Polus");

    public BorPresetOption PolusPreset { get; } = NewPreset("Polus", 2);

    public ModdedNumberOption DleksChance { get; } = NewChance("Dleks");

    public BorPresetOption DleksPreset { get; } = NewPreset("Dleks", 3);

    public ModdedNumberOption AirshipChance { get; } = NewChance("Airship");

    public BorPresetOption AirshipPreset { get; } = NewPreset("Airship", 4);

    public ModdedNumberOption FungleChance { get; } = NewChance("Fungle");

    public BorPresetOption FunglePreset { get; } = NewPreset("Fungle", 5);

    /// <summary>
    ///     Gets each map (in the order of the game: <see cref="MapNames"/>) with its weight and its preset.
    /// </summary>
    public IEnumerable<(MapNames Map, ModdedNumberOption Chance, BorPresetOption Preset)> Maps =>
    [
        (MapNames.Skeld, SkeldChance, SkeldPreset),
        (MapNames.MiraHQ, MiraChance, MiraPreset),
        (MapNames.Polus, PolusChance, PolusPreset),
        (MapNames.Dleks, DleksChance, DleksPreset),
        (MapNames.Airship, AirshipChance, AirshipPreset),
        (MapNames.Fungle, FungleChance, FunglePreset),
    ];

    /// <summary>
    ///     Gets whether the random map is on.
    /// </summary>
    /// <returns>Whether the main toggle is on.</returns>
    public static bool IsOn() => OptionGroupSingleton<RandomMapOptions>.Instance.Enabled.Value;

    private static ModdedNumberOption NewChance(string map) =>
        new($"BetterOtherRoles.Option.RandomMap.{map}Chance", 0f, 0f, 100f, 10f, MiraNumberSuffixes.Percent,
            formatString: "0", includeInPreset: false)
        {
            Visible = IsOn,
        };

    // Under the weight of its map, when presets per map are on and the map can be drawn.
    private static BorPresetOption NewPreset(string map, int index) =>
        new($"BetterOtherRoles.Option.RandomMap.{map}Preset")
        {
            Visible = () =>
            {
                var options = OptionGroupSingleton<RandomMapOptions>.Instance;
                return options.Enabled.Value && options.PresetPerMap.Value && options.Maps.ElementAt(index).Chance.Value > 0;
            },
        };
}
