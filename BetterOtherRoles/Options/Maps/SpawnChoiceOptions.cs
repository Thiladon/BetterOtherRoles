using BetterOtherRoles.Modules.Spawn;
using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Translation;

namespace BetterOtherRoles.Options.Maps;

/// <summary>
///     Spawn room chosen in a window, as on the Airship, on the other maps (<see cref="BorSpawnChoice"/>; TOU-Mira 1.7.9
///     BetterAirshipOptions.SpawnMode for the rooms offered): at the start of the game only, or after each meeting too.
///     The Airship always has its window; with "start of the game", its choice after a meeting is left out.
/// </summary>
public sealed class SpawnChoiceOptions : AbstractOptionGroup
{
    public override string GroupName => MiraLocaleManager.Get("BetterOtherRoles.Options.Groups.SpawnChoice", "Spawn Room");

    public override uint GroupPriority => 14;

    /// <summary>
    ///     Gets when the spawn room is chosen.
    /// </summary>
    public ModdedEnumOption Mode { get; } = new("BetterOtherRoles.Option.SpawnChoice.Mode", (int)BorSpawnChoiceMode.Off,
        typeof(BorSpawnChoiceMode),
        ["BetterOtherRoles.Option.SpawnChoice.ModeOff", "BetterOtherRoles.Option.SpawnChoice.ModeStart",
         "BetterOtherRoles.Option.SpawnChoice.ModeStartAndMeetings"]);

    public ModdedToggleOption Skeld { get; } = NewMap("Skeld");

    public ModdedToggleOption Mira { get; } = NewMap("Mira");

    public ModdedToggleOption Polus { get; } = NewMap("Polus");

    public ModdedToggleOption Dleks { get; } = NewMap("Dleks");

    public ModdedToggleOption Fungle { get; } = NewMap("Fungle");

    /// <summary>
    ///     Gets the rooms offered: drawn for each player, the same ones for everyone, or the one set by the host.
    /// </summary>
    public ModdedEnumOption Locations { get; } = new("BetterOtherRoles.Option.SpawnChoice.Locations",
        (int)BorSpawnLocations.Normal, typeof(BorSpawnLocations),
        ["BetterOtherRoles.Option.SpawnChoice.LocationsNormal", "BetterOtherRoles.Option.SpawnChoice.LocationsSame",
         "BetterOtherRoles.Option.SpawnChoice.LocationsHost"])
    {
        Visible = IsOn,
    };

    public ModdedEnumOption SkeldRoom { get; } = NewRoom(MapNames.Skeld, "Skeld");

    public ModdedEnumOption MiraRoom { get; } = NewRoom(MapNames.MiraHQ, "Mira");

    public ModdedEnumOption PolusRoom { get; } = NewRoom(MapNames.Polus, "Polus");

    public ModdedEnumOption DleksRoom { get; } = NewRoom(MapNames.Dleks, "Dleks");

    public ModdedEnumOption AirshipRoom { get; } = NewRoom(MapNames.Airship, "Airship");

    public ModdedEnumOption FungleRoom { get; } = NewRoom(MapNames.Fungle, "Fungle");

    /// <summary>
    ///     Gets when the spawn room is chosen.
    /// </summary>
    public BorSpawnChoiceMode ChoiceMode => (BorSpawnChoiceMode)Mode.Value;

    /// <summary>
    ///     Gets the rooms offered.
    /// </summary>
    public BorSpawnLocations OfferedRooms => (BorSpawnLocations)Locations.Value;

    /// <summary>
    ///     Gets whether the spawn room is chosen on a map (always on the Airship), when the choice is on.
    /// </summary>
    /// <param name="map">The map.</param>
    /// <returns>Whether its box is ticked.</returns>
    public bool AppliesTo(MapNames map) => map switch
    {
        MapNames.Skeld => Skeld.Value,
        MapNames.MiraHQ => Mira.Value,
        MapNames.Polus => Polus.Value,
        MapNames.Dleks => Dleks.Value,
        MapNames.Airship => true,
        MapNames.Fungle => Fungle.Value,
        _ => false,
    };

    /// <summary>
    ///     Gets the room set by the host on a map (index in <see cref="BorSpawnRooms.Of"/>).
    /// </summary>
    /// <param name="map">The map.</param>
    /// <returns>The index of the room.</returns>
    public int HostRoom(MapNames map) => map switch
    {
        MapNames.Skeld => SkeldRoom.Value,
        MapNames.MiraHQ => MiraRoom.Value,
        MapNames.Polus => PolusRoom.Value,
        MapNames.Dleks => DleksRoom.Value,
        MapNames.Airship => AirshipRoom.Value,
        MapNames.Fungle => FungleRoom.Value,
        _ => 0,
    };

    /// <summary>
    ///     Gets whether the spawn room is chosen.
    /// </summary>
    /// <returns>Whether the choice is on.</returns>
    public static bool IsOn() =>
        OptionGroupSingleton<SpawnChoiceOptions>.Instance.ChoiceMode != BorSpawnChoiceMode.Off;

    private static ModdedToggleOption NewMap(string map) =>
        new($"BetterOtherRoles.Option.SpawnChoice.{map}", true) { Visible = IsOn };

    // Shown with "set by the host", for the Airship and the maps whose box is ticked.
    private static ModdedEnumOption NewRoom(MapNames map, string name) =>
        new($"BetterOtherRoles.Option.SpawnChoice.{name}Room", 0, typeof(BorSpawnLocations),
            BorSpawnRooms.Of(map).Select(x => x.NameKey).ToArray())
        {
            Visible = () =>
            {
                var options = OptionGroupSingleton<SpawnChoiceOptions>.Instance;
                return IsOn() && options.OfferedRooms == BorSpawnLocations.Host && options.AppliesTo(map);
            },
        };
}

/// <summary>
///     When the spawn room is chosen.
/// </summary>
public enum BorSpawnChoiceMode
{
    /// <summary>As in the game (the Airship only, at the start and after each meeting).</summary>
    Off,

    /// <summary>At the start of the game only.</summary>
    Start,

    /// <summary>At the start of the game and after each meeting.</summary>
    StartAndMeetings,
}

/// <summary>
///     Rooms offered in the window.
/// </summary>
public enum BorSpawnLocations
{
    /// <summary>Drawn for each player.</summary>
    Normal,

    /// <summary>Drawn by the host, the same ones for everyone.</summary>
    Same,

    /// <summary>No window: everyone is put in the room set by the host.</summary>
    Host,
}
