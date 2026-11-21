using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Translation;
using MiraAPI.Utilities;

namespace BetterOtherRoles.Options.Maps;

/// <summary>
///     Weights of the doors drawn for a map set to random doors (TOU-Mira 1.7.9 RandomDoorMapOptions, same weights by
///     default), shown when a map is set so (<see cref="DoorOptions"/>). A type at 100 % is forced, as for the random
///     map (<see cref="Modules.Doors.BorRandomDoors"/>).
/// </summary>
public sealed class RandomDoorOptions : AbstractOptionGroup
{
    public override string GroupName => MiraLocaleManager.Get("BetterOtherRoles.Options.Groups.RandomDoors", "Random Doors");

    public override uint GroupPriority => 9;

    public override Func<bool> GroupVisible => () => OptionGroupSingleton<DoorOptions>.Instance.AnyRandom;

    public ModdedNumberOption NoneChance { get; } = NewChance("None", 5f);

    public ModdedNumberOption SkeldChance { get; } = NewChance("Skeld", 20f);

    public ModdedNumberOption PolusChance { get; } = NewChance("Polus", 40f);

    public ModdedNumberOption AirshipChance { get; } = NewChance("Airship", 15f);

    public ModdedNumberOption FungleChance { get; } = NewChance("Fungle", 20f);

    /// <summary>
    ///     Gets each type of doors with its weight.
    /// </summary>
    public IEnumerable<(BorDoorType Type, ModdedNumberOption Chance)> Types =>
    [
        (BorDoorType.None, NoneChance),
        (BorDoorType.Skeld, SkeldChance),
        (BorDoorType.Polus, PolusChance),
        (BorDoorType.Airship, AirshipChance),
        (BorDoorType.Fungle, FungleChance),
    ];

    private static ModdedNumberOption NewChance(string type, float defaultValue) =>
        new($"BetterOtherRoles.Option.RandomDoors.{type}Chance", defaultValue, 0f, 100f, 5f, MiraNumberSuffixes.Percent, "0");
}
