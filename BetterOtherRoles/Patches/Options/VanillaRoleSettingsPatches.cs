using BetterOtherRoles.Patches.Roles;
using HarmonyLib;
using MiraAPI.Roles;

namespace BetterOtherRoles.Patches.Options;

/// <summary>
///     Vanilla roles in the role settings: greyed out with 0 / 0 % while they are disabled (<see cref="VanillaRolesPatch"/>),
///     editable in the "vanilla roles only" mode. The values shown come from the patched GetNumPerGame / GetChancePerGame.
/// </summary>
[HarmonyPatch]
public static class VanillaRoleSettingsPatches
{
    // State of the rows shown, to refresh them when the assignment mode changes.
    private static bool? _shownDisabled;

    [HarmonyPatch(typeof(RoleOptionSetting), nameof(RoleOptionSetting.UpdateValuesAndText))]
    [HarmonyPostfix]
    public static void UpdateValuesAndTextPostfix(RoleOptionSetting __instance)
    {
        // Enabled: the game sets the buttons from the values (minus greyed at 0, plus at the max).
        if (!IsVanillaRow(__instance) || !VanillaRolesPatch.AreVanillaRolesDisabled) return;

        __instance.CountMinusBtn.SetInteractable(false);
        __instance.CountPlusBtn.SetInteractable(false);
        __instance.ChanceMinusBtn.SetInteractable(false);
        __instance.ChancePlusBtn.SetInteractable(false);
    }

    [HarmonyPatch(typeof(RolesSettingsMenu), nameof(RolesSettingsMenu.OnEnable))]
    [HarmonyPostfix]
    public static void OnEnablePostfix()
    {
        _shownDisabled = null;
    }

    [HarmonyPatch(typeof(RolesSettingsMenu), nameof(RolesSettingsMenu.Update))]
    [HarmonyPostfix]
    public static void UpdatePostfix(RolesSettingsMenu __instance)
    {
        var disabled = VanillaRolesPatch.AreVanillaRolesDisabled;
        if (_shownDisabled == disabled || __instance.roleChances == null) return;

        _shownDisabled = disabled;
        foreach (var row in __instance.roleChances)
        {
            if (IsVanillaRow(row))
                row.UpdateValuesAndText(GameOptionsManager.Instance.CurrentGameOptions.RoleOptions);
        }
    }

    private static bool IsVanillaRow(RoleOptionSetting row)
    {
        return row && row.Role && !row.Role.IsCustomRole();
    }
}
