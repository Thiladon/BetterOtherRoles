using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Translation;
using MiraAPI.Utilities;

namespace BetterOtherRoles.Options.Maps;

/// <summary>
///     Options of the doors, for every map and type of doors (<see cref="DoorOptions"/>): durations, mushrooms of the
///     doors of The Fungle, and the rules between doors and sabotages (<see cref="Modules.Doors.BorDoorRules"/>).
/// </summary>
public sealed class DoorRulesOptions : AbstractOptionGroup
{
    public override string GroupName => MiraLocaleManager.Get("BetterOtherRoles.Options.Groups.Doors", "Doors");

    public override uint GroupPriority => 8;

    /// <summary>
    ///     Gets the time before the impostors can sabotage or close doors at the start of the game (10 s in the game).
    /// </summary>
    public ModdedNumberOption InitialCooldown { get; } =
        new("BetterOtherRoles.Option.Doors.InitialCooldown", 10f, 0f, 60f, 1f, MiraNumberSuffixes.Seconds);

    /// <summary>
    ///     Gets how long the doors that open by themselves (doors of The Skeld) stay closed (10 s in the game).
    /// </summary>
    public ModdedNumberOption ClosedDuration { get; } =
        new("BetterOtherRoles.Option.Doors.ClosedDuration", 10f, 5f, 30f, 1f, MiraNumberSuffixes.Seconds);

    /// <summary>
    ///     Gets the time before the doors of a room can be closed again, for every type of doors (30 s in the game).
    /// </summary>
    public ModdedNumberOption Cooldown { get; } =
        new("BetterOtherRoles.Option.Doors.Cooldown", 30f, 10f, 60f, 1f, MiraNumberSuffixes.Seconds);

    /// <summary>
    ///     Gets the mushrooms to hit to open a door of The Fungle (6 in the game).
    /// </summary>
    public ModdedNumberOption Mushrooms { get; } =
        new("BetterOtherRoles.Option.Doors.Mushrooms", 6f, 1f, 12f, 1f, MiraNumberSuffixes.None);

    /// <summary>
    ///     Gets whether the impostors can sabotage while doors are closed. In the game, only the doors of The Skeld stop
    ///     the sabotages.
    /// </summary>
    public ModdedEnumOption SabotageWithClosedDoors { get; } = NewRule("SabotageWithClosedDoors");

    /// <summary>
    ///     Gets whether the impostors can close doors during a sabotage. In the game, only on Polus.
    /// </summary>
    public ModdedEnumOption CloseDoorsDuringSabotage { get; } = NewRule("CloseDoorsDuringSabotage");

    private static ModdedEnumOption NewRule(string name) =>
        new($"BetterOtherRoles.Option.Doors.{name}", (int)DoorRule.ByDoorType, typeof(DoorRule),
            ["BetterOtherRoles.Option.Doors.RuleByDoorType", "BetterOtherRoles.Option.Doors.RuleYes", "BetterOtherRoles.Option.Doors.RuleNo"]);
}

/// <summary>
///     A rule between doors and sabotages.
/// </summary>
public enum DoorRule
{
    /// <summary>As the game does with the doors used (the doors of a map keep their rule on another map).</summary>
    ByDoorType,

    Yes,

    No,
}
