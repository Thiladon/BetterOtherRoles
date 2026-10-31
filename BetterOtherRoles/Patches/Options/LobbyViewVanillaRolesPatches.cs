using HarmonyLib;
using MiraAPI.Roles;

namespace BetterOtherRoles.Patches.Options;

/// <summary>
///     Fixes a MiraAPI 0.6.0 bug in the game settings view of the clients (LobbyViewSettingsPane, roles tab of
///     Among Us): MiraAPI skips the mod roles of RoleManager.AllRoles with "is ICustomRole", but that list only holds
///     plain RoleBehaviour wrappers, so the Sheriff was shown with the vanilla roles.
///     The list is filtered while the tab is drawn (the MiraAPI tab of our mod uses its own role list).
/// </summary>
[HarmonyPatch(typeof(LobbyViewSettingsPane), nameof(LobbyViewSettingsPane.DrawRolesTab))]
public static class LobbyViewVanillaRolesPatches
{
    private static Il2CppSystem.Collections.Generic.List<RoleBehaviour>? _allRoles;

    // Before the MiraAPI prefix, which returns false and skips the prefixes after it.
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First + 100)]
    public static void DrawRolesTabPrefix()
    {
        var roleManager = RoleManager.Instance;
        if (!roleManager || roleManager.AllRoles == null)
        {
            return;
        }

        _allRoles = roleManager.AllRoles;
        var vanillaRoles = new Il2CppSystem.Collections.Generic.List<RoleBehaviour>();
        foreach (var role in _allRoles)
        {
            if (!CustomRoleManager.GetCustomRoleBehaviour(role.Role, out _))
            {
                vanillaRoles.Add(role);
            }
        }

        roleManager.AllRoles = vanillaRoles;
    }

    [HarmonyFinalizer]
    public static void DrawRolesTabFinalizer()
    {
        if (_allRoles != null && RoleManager.Instance)
        {
            RoleManager.Instance.AllRoles = _allRoles;
        }

        _allRoles = null;
    }
}
