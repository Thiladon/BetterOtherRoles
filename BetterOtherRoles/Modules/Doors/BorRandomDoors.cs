using BetterOtherRoles.Options.Maps;
using MiraAPI.GameOptions;
using Reactor.Networking.Attributes;

namespace BetterOtherRoles.Modules.Doors;

/// <summary>
///     Doors drawn for a map set to random doors (<see cref="RandomDoorOptions"/>): by the host when the start
///     countdown ends, after the random map (<see cref="Presets.BorRandomMap"/>), then sent to everyone before the map is
///     made. A type at 100 % is forced (with the other ones at 100 %), otherwise each type is drawn by its weight; no
///     weight: the doors of the map. In free play, drawn when the map is made.
/// </summary>
public static class BorRandomDoors
{
    /// <summary>
    ///     Gets the doors drawn for the game, or null when none were drawn.
    /// </summary>
    public static BorDoorType? Drawn { get; private set; }

    /// <summary>
    ///     Draws the doors when the map of the game is set to random doors (host, end of the start countdown).
    /// </summary>
    public static void Draw()
    {
        var map = (MapNames)GameOptionsManager.Instance.CurrentGameOptions.MapId;
        if (OptionGroupSingleton<DoorOptions>.Instance.Get(map) != BorDoorType.Random) return;

        var type = Roll(BorDoors.NativeType(map) ?? BorDoorType.None);
        RpcSetDoors(PlayerControl.LocalPlayer, (int)type);
    }

    /// <summary>
    ///     Draws a type of doors by the weights.
    /// </summary>
    /// <param name="fallback">Doors when no type has a weight.</param>
    /// <returns>The doors drawn.</returns>
    public static BorDoorType Roll(BorDoorType fallback)
    {
        var types = OptionGroupSingleton<RandomDoorOptions>.Instance.Types.Where(x => x.Chance.Value > 0).ToList();
        if (types.Count == 0) return fallback;

        if (types.Exists(x => x.Chance.Value >= 100))
            types = types.Where(x => x.Chance.Value >= 100).ToList();

        var roll = UnityEngine.Random.Range(0f, types.Sum(x => x.Chance.Value));
        foreach (var (type, chance) in types)
        {
            if (roll < chance.Value) return type;

            roll -= chance.Value;
        }

        return types[^1].Type;
    }

    /// <summary>
    ///     Forgets the doors drawn (end of the game).
    /// </summary>
    public static void Clear() => Drawn = null;

    // "None" sent as the other types (TOU-Mira 1.7.9 left it out: the doors of the previous draw were kept).
    [MethodRpc((uint)BorRpc.RandomDoors)]
    private static void RpcSetDoors(PlayerControl host, int type)
    {
        if (!host || host.OwnerId != AmongUsClient.Instance.HostId || !Enum.IsDefined(typeof(BorDoorType), type)) return;

        Drawn = (BorDoorType)type;
    }
}
