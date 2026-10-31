namespace BetterOtherRoles.Roles;

/// <summary>
///     A role that cannot be assigned in the same game as some other roles (blocked role pairings of BetterOtherRoles
///     1.7.1, declared as the IExclusiveRole of TOU-Mira). Symmetric: only one of the two roles needs to declare it.
///     Applied during the draw (<see cref="Modules.Roles.BorRoleSelector"/>): the first role actually assigned blocks
///     the other ones, even at 100 %.
/// </summary>
public interface IExclusiveRole
{
    /// <summary>
    ///     Gets the role types this role cannot be assigned with. Types that are not registered roles are ignored.
    /// </summary>
    IEnumerable<Type> ExclusiveWith { get; }
}
