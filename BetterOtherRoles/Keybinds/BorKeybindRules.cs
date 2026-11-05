using BetterOtherRoles.Modules.Admin;
using BetterOtherRoles.Roles.Crewmate;
using MiraAPI.Keybinds;
using MiraAPI.Roles;
using MiraAPI.Translation;
using UnityEngine;

namespace BetterOtherRoles.Keybinds;

/// <summary>
///     Conflict rule between keybinds, and their names in Remap Controls.
///     Two actions on the same key conflict unless they can't be on screen together:
///     - role actions (<see cref="BorKeybind"/>) of different roles don't conflict;
///     - vanilla Kill and Sabotage only conflict with the actions of impostor team roles and of the roles using the
///       Kill key for their own killing button (Sheriff);
///     - vanilla Role Ability only conflicts with vanilla actions (our roles don't have this button);
///     - any other action (Use, Report, Vent, movement, other mods...) is universal: it conflicts with everything.
/// </summary>
public static class BorKeybindRules
{
    // Vanilla Rewired action ids.
    private const int SabotageAction = 4;
    private const int KillAction = 8;
    private const int RoleAbilityAction = 49;

    // Roles outside the impostor team whose killing button uses the vanilla Kill key.
    private static readonly HashSet<Type> KillKeyRoles = [typeof(SheriffRole)];

    private enum Scope
    {
        Universal,
        Roles,
        ImpostorRoles,
        VanillaRoles,
    }

    /// <summary>
    ///     Gets the BetterOtherRoles role keybinds.
    /// </summary>
    public static IEnumerable<BorKeybind> RoleKeybinds => KeybindManager.Keybinds.OfType<BorKeybind>();

    /// <summary>
    ///     Gets a value indicating whether two Rewired actions can't share the same key or button.
    /// </summary>
    public static bool Conflicts(int actionA, int actionB)
    {
        if (actionA == actionB) return false;

        var scopeA = GetScope(actionA, out var keybindA);
        var scopeB = GetScope(actionB, out var keybindB);
        if (scopeA == Scope.Universal || scopeB == Scope.Universal) return true;

        if (scopeA == Scope.Roles && scopeB == Scope.Roles) return keybindA!.Roles.Intersect(keybindB!.Roles).Any();

        if (scopeA == Scope.Roles) return scopeB == Scope.ImpostorRoles && keybindA!.Roles.Any(UsesKillKey);

        if (scopeB == Scope.Roles) return scopeA == Scope.ImpostorRoles && keybindB!.Roles.Any(UsesKillKey);

        // Two vanilla actions bound to roles: vanilla impostor roles have Kill, Sabotage and Role Ability.
        return true;
    }

    /// <summary>
    ///     Gets a value indicating whether two keybinds can't share the same key or button.
    /// </summary>
    public static bool Conflicts(MiraKeybind keybindA, MiraKeybind keybindB)
    {
        return keybindA.RewiredInputAction is { } actionA &&
               keybindB.RewiredInputAction is { } actionB &&
               Conflicts(actionA.id, actionB.id);
    }

    /// <summary>
    ///     Writes the role keybind names shown in Remap Controls: the role name (in its color) above the action name,
    ///     in the mod language.
    ///     Does nothing before the RoleManager exists: reading a role name would make DestroyableSingleton register
    ///     the RoleManager before its Awake, which then skips DontDestroyOnLoad (no RoleManager in game, black screen).
    /// </summary>
    public static void RefreshNames()
    {
        if (!DestroyableSingleton<RoleManager>.InstanceExists) return;

        foreach (var keybind in RoleKeybinds)
        {
            if (keybind.RewiredInputAction == null) continue;

            var role = keybind.Roles.Count > 0 ? FindRole(keybind.Roles[0]) : null;
            var header = role != null
                ? $"<color=#{ColorUtility.ToHtmlStringRGB(role.RoleColor)}>{role.RoleName}</color>"
                : MiraLocaleManager.Get("BetterOtherRoles.LocalSettings.Tab", "Better Other Roles");
            keybind.RewiredInputAction.descriptiveName = $"<b><size=70%>{header}</size></b>\n{keybind.NameKey.Translate()}";
        }
    }

    /// <summary>
    ///     Shows the admin keybinds (Debug menu) in Remap Controls to the admins only; they still work for nobody else.
    /// </summary>
    public static void RefreshAdminKeybinds()
    {
        if (BorKeybinds.DebugMenu.RewiredInputAction is { } action)
            action.userAssignable = BorAdmin.IsAdmin;
    }

    private static Scope GetScope(int actionId, out BorKeybind? keybind)
    {
        keybind = RoleKeybinds.FirstOrDefault(x => x.RewiredInputAction?.id == actionId);
        if (keybind != null) return keybind.Roles.Count > 0 ? Scope.Roles : Scope.Universal;

        return actionId switch
        {
            KillAction or SabotageAction => Scope.ImpostorRoles,
            RoleAbilityAction => Scope.VanillaRoles,
            _ => Scope.Universal,
        };
    }

    private static ICustomRole? FindRole(Type roleType)
    {
        return CustomRoleManager.CustomMiraRoles.FirstOrDefault(role => role.GetType() == roleType);
    }

    private static bool UsesKillKey(Type roleType)
    {
        return KillKeyRoles.Contains(roleType) || FindRole(roleType)?.Team == ModdedRoleTeams.Impostor;
    }
}
