using BetterOtherRoles.Patches.Options;
using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Translation;

namespace BetterOtherRoles.Options.Maps;

/// <summary>
///     Doors of each map (TOU-Mira 1.7.9 Better Maps door type): the ones of another map, none, or drawn at the start
///     of the game (<see cref="RandomDoorOptions"/>). Shown in the vanilla game settings tab, only for the maps that can
///     be played (<see cref="MapSettingsPatches"/>), not in the tab of BetterOtherRoles. MIRA HQ has no doors.
/// </summary>
public sealed class DoorOptions : AbstractOptionGroup
{
    public override string GroupName => MiraLocaleManager.Get("BetterOtherRoles.Setting.Doors", "Doors");

    public override uint GroupPriority => 101;

    public override Func<bool> GroupVisible => () => false;

    public override bool ShowInMatchInfoGuide => false;

    public ModdedEnumOption SkeldDoors { get; } = New("Skeld", BorDoorType.Skeld);

    public ModdedEnumOption PolusDoors { get; } = New("Polus", BorDoorType.Polus);

    public ModdedEnumOption AirshipDoors { get; } = New("Airship", BorDoorType.Airship);

    public ModdedEnumOption FungleDoors { get; } = New("Fungle", BorDoorType.Fungle);

    /// <summary>
    ///     Gets each option with the maps it is used on (<see cref="MapNames"/>), in the order shown. The Skeld and dlekS
    ///     share their option.
    /// </summary>
    public IEnumerable<(MapNames[] Maps, IModdedOption Option)> All =>
    [
        ([MapNames.Skeld, MapNames.Dleks], SkeldDoors),
        ([MapNames.Polus], PolusDoors),
        ([MapNames.Airship], AirshipDoors),
        ([MapNames.Fungle], FungleDoors),
    ];

    /// <summary>
    ///     Gets whether a map is set to random doors (the weights are then shown).
    /// </summary>
    public bool AnyRandom => All.Any(x => ((ModdedEnumOption)x.Option).Value == (int)BorDoorType.Random);

    /// <summary>
    ///     Gets the doors set for a map (random not drawn yet), or null for a map without doors.
    /// </summary>
    public BorDoorType? Get(MapNames map) => map switch
    {
        MapNames.Skeld or MapNames.Dleks => (BorDoorType)SkeldDoors.Value,
        MapNames.Polus => (BorDoorType)PolusDoors.Value,
        MapNames.Airship => (BorDoorType)AirshipDoors.Value,
        MapNames.Fungle => (BorDoorType)FungleDoors.Value,
        _ => null,
    };

    private static ModdedEnumOption New(string map, BorDoorType defaultValue) =>
        new($"BetterOtherRoles.Setting.Doors.{map}", (int)defaultValue, typeof(BorDoorType),
            ["BetterOtherRoles.Setting.Doors.TypeSkeld", "BetterOtherRoles.Setting.Doors.TypePolus", "BetterOtherRoles.Setting.Doors.TypeAirship",
             "BetterOtherRoles.Setting.Doors.TypeFungle", "BetterOtherRoles.Setting.Doors.TypeNone", "BetterOtherRoles.Setting.Doors.TypeRandom"])
        {
            ChangedEvent = _ => MapSettingsPatches.RefreshValues(),
        };
}

/// <summary>
///     Doors of a map: the ones of a map of the game, none, or drawn at the start of the game.
/// </summary>
public enum BorDoorType
{
    /// <summary>Close for a while, then open by themselves.</summary>
    Skeld,

    /// <summary>Opened with the minigame of Polus (manual doors, as below).</summary>
    Polus,

    /// <summary>Opened with the minigame of the Airship.</summary>
    Airship,

    /// <summary>Opened with the minigame of The Fungle.</summary>
    Fungle,

    /// <summary>No doors (as MIRA HQ).</summary>
    None,

    /// <summary>Drawn by the host at the start of the game (<see cref="RandomDoorOptions"/>).</summary>
    Random,
}
