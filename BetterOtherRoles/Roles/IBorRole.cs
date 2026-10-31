using MiraAPI.Roles;

namespace BetterOtherRoles.Roles;

/// <summary>
///     Base interface of every BetterOtherRoles role.
/// </summary>
public interface IBorRole : ICustomRole
{
    /// <summary>
    ///     Gets the prefix of the role translation keys: BetterOtherRoles.Role.{IdPart}[.Suffix].
    /// </summary>
    string ICustomRole.IdPrefix => "BetterOtherRoles.Role";

    /// <summary>
    ///     Gets the alignment of the role inside its faction, for the role list slots.
    /// </summary>
    BorRoleAlignment Alignment { get; }
}
