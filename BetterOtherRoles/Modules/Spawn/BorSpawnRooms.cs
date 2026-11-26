using MiraAPI.Translation;
using UnityEngine;

namespace BetterOtherRoles.Modules.Spawn;

/// <summary>
///     A room offered in the spawn window: everyone who picks it arrives on its point, as on the Airship.
/// </summary>
/// <param name="Key">Name of the room in the locale files (BetterOtherRoles.SpawnChoice.Room.Key).</param>
/// <param name="Position">Point where the players arrive.</param>
public sealed record BorSpawnRoom(string Key, Vector2 Position)
{
    /// <summary>
    ///     Gets the locale key of its name.
    /// </summary>
    public string NameKey => $"BetterOtherRoles.SpawnChoice.Room.{Key}";

    /// <summary>
    ///     Gets its name, as given to the window.
    /// </summary>
    public StringNames Name => MiraLocaleManager.GetOrCreateLocaleString(NameKey);
}

/// <summary>
///     Spawn rooms of each map, points noted in the game by the user (Debug menu, BorDebugPosition) on 2026-11-24/25.
///     The first one is the room set by the host by default. dlekS: The Skeld turned over (x inverted). Airship: the
///     locations of its window (SpawnInMinigame), same keys as their StringNames.
/// </summary>
public static class BorSpawnRooms
{
    private static readonly BorSpawnRoom[] Skeld =
    [
        new("Cafeteria", new(-0.83f, 2.67f)),
        new("Reactor", new(-20.87f, -5.35f)),
        new("Electrical", new(-8.30f, -11.15f)),
        new("Navigation", new(16.67f, -4.63f)),
        new("MedBay", new(-9.10f, -2.98f)),
        new("Comms", new(3.87f, -15.70f)),
    ];

    private static readonly BorSpawnRoom[] Dleks =
        Skeld.Select(x => x with { Position = new Vector2(-x.Position.x, x.Position.y) }).ToArray();

    private static readonly BorSpawnRoom[] Mira =
    [
        new("Launchpad", new(-4.40f, 2.45f)),
        new("ReactorHallway", new(6.08f, 12.44f)),
        new("Greenhouse", new(17.87f, 23.42f)),
        new("Cafeteria", new(25.38f, 2.59f)),
        new("MedBay", new(15.26f, 0.25f)),
    ];

    private static readonly BorSpawnRoom[] Polus =
    [
        new("Dropship", new(16.69f, -2.71f)),
        new("Electrical", new(5.42f, -9.97f)),
        new("O2", new(3.16f, -20.04f)),
        new("Weapons", new(12.29f, -23.19f)),
        new("Office", new(22.24f, -19.11f)),
        new("Specimens", new(36.47f, -19.81f)),
        new("Laboratory", new(33.23f, -7.09f)),
    ];

    private static readonly BorSpawnRoom[] Airship =
    [
        new("MainHall", new(15.50f, 0.00f)),
        new("Kitchen", new(-7.00f, -11.50f)),
        new("CargoBay", new(33.50f, -1.50f)),
        new("Engine", new(-0.70f, -1.00f)),
        new("Brig", new(-0.70f, 8.50f)),
        new("Records", new(20.00f, 10.50f)),
    ];

    private static readonly BorSpawnRoom[] Fungle =
    [
        new("Campfire", new(-9.85f, 4.28f)),
        new("Beach", new(-22.77f, -0.47f)),
        new("Laboratory", new(-4.34f, -8.57f)),
        new("Greenhouse", new(9.33f, -10.02f)),
        new("MiningPit", new(12.63f, 9.78f)),
        new("Comms", new(21.65f, 13.55f)),
    ];

    /// <summary>
    ///     Gets the spawn rooms of a map (none for a map not handled).
    /// </summary>
    /// <param name="map">The map.</param>
    /// <returns>Its rooms.</returns>
    public static BorSpawnRoom[] Of(MapNames map) => map switch
    {
        MapNames.Skeld => Skeld,
        MapNames.MiraHQ => Mira,
        MapNames.Polus => Polus,
        MapNames.Dleks => Dleks,
        MapNames.Airship => Airship,
        MapNames.Fungle => Fungle,
        _ => [],
    };
}
