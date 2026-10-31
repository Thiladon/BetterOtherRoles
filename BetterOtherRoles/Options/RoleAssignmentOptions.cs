using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Translation;
using MiraAPI.Utilities;

namespace BetterOtherRoles.Options;

/// <summary>
///     Role assignment options, based on TOU-Mira 1.7.9 (RoleOptions), with a min/max for each faction as in BetterOtherRoles 1.7.1,
///     or a role list of 15 slots, one category each (TOU-Mira).
/// </summary>
public sealed class RoleAssignmentOptions : AbstractOptionGroup
{
    public override string GroupName => MiraLocaleManager.Get("BetterOtherRoles.Options.Groups.RoleAssignment", "Role Assignment");

    public override uint GroupPriority => 2;

    /// <summary>
    ///     Gets whether only the vanilla roles are played (the mod roles are left out, the vanilla ones can be set).
    /// </summary>
    [MiraIgnore]
    public static bool IsVanillaRolesOnly => OptionGroupSingleton<RoleAssignmentOptions>.Instance.Mode.Value is (int)RoleAssignmentMode.VanillaRolesOnly;

    /// <summary>
    ///     Number of role list slots (TOU-Mira).
    /// </summary>
    public const int RoleListSlotCount = 15;

    /// <summary>
    ///     Gets whether the roles are given from the role list slots.
    /// </summary>
    [MiraIgnore]
    public static bool IsRoleList => OptionGroupSingleton<RoleAssignmentOptions>.Instance.Mode.Value is (int)RoleAssignmentMode.RoleList;

    private static bool IsMinMaxList => OptionGroupSingleton<RoleAssignmentOptions>.Instance.Mode.Value is (int)RoleAssignmentMode.MinMaxList;

    public ModdedEnumOption Mode { get; } =
        new("BetterOtherRoles.Option.RoleAssignment.Mode", (int)RoleAssignmentMode.MinMaxList, typeof(RoleAssignmentMode),
            [
                "BetterOtherRoles.Option.RoleAssignment.ModeVanillaRolesOnly",
                "BetterOtherRoles.Option.RoleAssignment.ModeMinMaxList",
                "BetterOtherRoles.Option.RoleAssignment.ModeRoleList",
            ]);

    /// <summary>
    ///     Gets whether the last game impostors are less likely to be impostor again (TOU-Mira).
    /// </summary>
    public ModdedToggleOption ReduceImpostorStreak { get; } =
        new("BetterOtherRoles.Option.RoleAssignment.ReduceImpostorStreak", true)
        {
            Visible = () => IsMinMaxList || IsRoleList,
        };

    public ModdedNumberOption ImpostorStreakChance { get; } =
        new("BetterOtherRoles.Option.RoleAssignment.ImpostorStreakChance", 15f, 0f, 100f, 5f, MiraNumberSuffixes.Percent)
        {
            Visible = () => (IsMinMaxList || IsRoleList) && OptionGroupSingleton<RoleAssignmentOptions>.Instance.ReduceImpostorStreak.Value,
        };

    public ModdedNumberOption MinCrewmateRoles { get; } =
        new("BetterOtherRoles.Option.RoleAssignment.MinCrewmateRoles", 0f, 0f, 15f, 1f, MiraNumberSuffixes.None, "0")
        {
            Visible = () => IsMinMaxList,
        };

    public ModdedNumberOption MaxCrewmateRoles { get; } =
        new("BetterOtherRoles.Option.RoleAssignment.MaxCrewmateRoles", 15f, 0f, 15f, 1f, MiraNumberSuffixes.None, "0")
        {
            Visible = () => IsMinMaxList,
        };

    public ModdedNumberOption MinNeutralRoles { get; } =
        new("BetterOtherRoles.Option.RoleAssignment.MinNeutralRoles", 0f, 0f, 15f, 1f, MiraNumberSuffixes.None, "0")
        {
            Visible = () => IsMinMaxList,
        };

    public ModdedNumberOption MaxNeutralRoles { get; } =
        new("BetterOtherRoles.Option.RoleAssignment.MaxNeutralRoles", 15f, 0f, 15f, 1f, MiraNumberSuffixes.None, "0")
        {
            Visible = () => IsMinMaxList,
        };

    public ModdedNumberOption MinImpostorRoles { get; } =
        new("BetterOtherRoles.Option.RoleAssignment.MinImpostorRoles", 0f, 0f, 3f, 1f, MiraNumberSuffixes.None, "0")
        {
            Visible = () => IsMinMaxList,
        };

    public ModdedNumberOption MaxImpostorRoles { get; } =
        new("BetterOtherRoles.Option.RoleAssignment.MaxImpostorRoles", 3f, 0f, 3f, 1f, MiraNumberSuffixes.None, "0")
        {
            Visible = () => IsMinMaxList,
        };

    /// <summary>
    ///     Gets the role list slots: only the first ones are used, one per player (the players beyond 15 get a random
    ///     crewmate or non-impostor role, TOU-Mira).
    /// </summary>
    public ModdedOptionList<ModdedEnumOption<RoleListSlot>> Slots { get; } =
        new(RoleListSlotCount, i => new ModdedEnumOption<RoleListSlot>(
            $"BetterOtherRoles.Option.RoleAssignment.Slot{i + 1}",
            GetDefaultSlot(i),
            Enum.GetNames<RoleListSlot>().Select(x => $"BetterOtherRoles.RoleListSlot.{x}").ToArray())
        {
            Visible = () => IsRoleList,
        });

    // One impostor every 7 players or so (slots 1, 8 and 13), a non-impostor role every 5 slots, the rest crewmates.
    private static RoleListSlot GetDefaultSlot(int index)
    {
        return index switch
        {
            0 or 7 or 12 => RoleListSlot.ImpostorRandom,
            4 or 9 or 14 => RoleListSlot.NonImpostor,
            _ => RoleListSlot.CrewRandom,
        };
    }
}

/// <summary>
///     How the roles are given at the game start. New modes (RoleList, Draft) go at the end: the value is saved as a number.
/// </summary>
public enum RoleAssignmentMode
{
    /// <summary>The vanilla roles only, given by MiraAPI (the mod roles are left out). Modifiers will stay available.</summary>
    VanillaRolesOnly,

    /// <summary>A min/max for each faction.</summary>
    MinMaxList,

    /// <summary>A role list: one category per slot, one slot per player (TOU-Mira).</summary>
    RoleList,
}
