using BetterOtherRoles.Modules.Debug;
using MiraAPI.Roles;
using MiraAPI.Translation;

namespace BetterOtherRoles.Modules.Commands.Debug;

/// <summary>
///     /setrole [player] {role}: gives a role to the player (the host by default), buttons ready.
///     Same action as "Change role" in the Debug menu (<see cref="BorDebugActions.SetRole"/>).
/// </summary>
public sealed class SetRoleCommand : BorCommand
{
    public override string Name => "setrole";

    public override CommandAccess Access => CommandAccess.Debug;

    protected override string Key => "SetRole";

    /// <summary>
    ///     English name of a role, whatever the game language: class name without "Role" for the modded roles
    ///     (SheriffRole: Sheriff), RoleTypes name for the vanilla ones (Crewmate, Impostor).
    /// </summary>
    /// <param name="role">The role.</param>
    /// <returns>The name typed in the command.</returns>
    public static string GetCommandName(RoleBehaviour role)
    {
        if (role is not ICustomRole) return role.Role.ToString();

        var typeName = role.GetIl2CppType().Name;
        return typeName.EndsWith("Role", StringComparison.Ordinal) ? typeName[..^4] : typeName;
    }

    public override void Run(string args)
    {
        var player = BorCommandUtils.TakePlayerOrSelf(args, out var roleArgs);
        var roleName = BorCommandUtils.Normalize(roleArgs);
        if (roleName.Length == 0)
        {
            ReplyUsage();
            return;
        }

        var roles = BorDebugActions.GetRoles();
        var role = roles.FirstOrDefault(x => BorCommandUtils.Normalize(GetCommandName(x)) == roleName);
        if (role == null)
        {
            BorCommandUtils.Reply(MiraLocaleManager.Get("BetterOtherRoles.Command.SetRole.NotFound", "Role not found. Roles: {0}")
                .Replace("{0}", string.Join(", ", roles.Select(GetCommandName).Distinct().OrderBy(x => x))));
            return;
        }

        BorDebugActions.SetRole(player, role);
        BorCommandUtils.Reply(MiraLocaleManager.Get("BetterOtherRoles.Command.SetRole.Done", "{0} is now {1}.")
            .Replace("{0}", player.Data.PlayerName)
            .Replace("{1}", GetCommandName(role)));
    }
}
