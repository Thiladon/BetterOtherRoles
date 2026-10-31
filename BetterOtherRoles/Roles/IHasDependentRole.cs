namespace BetterOtherRoles.Roles;

/// <summary>
///     A role that brings another role with it when it is assigned (the Sheriff and its Deputy).
///     At 100 %, the dependent role is forced; below, it joins the tickets. It takes a role slot of the same faction.
/// </summary>
public interface IHasDependentRole
{
    /// <summary>
    ///     Gets the dependent role id, or <see langword="null"/> when it is disabled.
    /// </summary>
    ushort? DependentRoleId { get; }

    /// <summary>
    ///     Gets the chance of the dependent role, in percent.
    /// </summary>
    int DependentRoleChance { get; }
}
