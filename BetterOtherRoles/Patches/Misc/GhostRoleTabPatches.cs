using BetterOtherRoles.LocalSettings;
using BetterOtherRoles.Patches.Roles;
using HarmonyLib;
using MiraAPI.LocalSettings;
using MiraAPI.Roles;
using UnityEngine;
using MiraHudPatches = MiraAPI.Patches.Roles.HudManagerPatches;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Tab of the role (MiraAPI <see cref="MiraHudPatches.RoleTab"/>, under the task list): MiraAPI removes it at death,
///     the ghost role having none. With <see cref="BorLocalSettingsTab.GhostsKeepRoleTab"/>, a dead player keeps the
///     tab of his role while alive. The closed tab keeps the gap of the open one with the task list.
/// </summary>
[HarmonyPatch]
public static class GhostRoleTabPatches
{
    private static readonly Func<ICustomRole, TaskPanelBehaviour> CreateRoleTab =
        AccessTools.MethodDelegate<Func<ICustomRole, TaskPanelBehaviour>>(AccessTools.Method(typeof(CustomRoleManager), "CreateRoleTab"));

    private static readonly Action<TaskPanelBehaviour, ICustomRole> UpdateRoleTab =
        AccessTools.MethodDelegate<Action<TaskPanelBehaviour, ICustomRole>>(AccessTools.Method(typeof(CustomRoleManager), "UpdateRoleTab"));

    [HarmonyPatch(typeof(MiraHudPatches), nameof(MiraHudPatches.UpdatePostfix))]
    [HarmonyPrefix]
    public static bool UpdatePostfixPrefix()
    {
        var role = GetAliveRole();
        if (role == null) return true;

        if (!MiraHudPatches.RoleTab)
            MiraHudPatches.RoleTab = CreateRoleTab(role);

        UpdateRoleTab(MiraHudPatches.RoleTab!, role);
        return false;
    }

    // Gap between the task list and the tab of the role, measured with the task list open (MiraAPI place).
    private static float _gap = 0.2f;

    /// <summary>
    ///     Same gap between the task list and the tab of the role, open or closed:
    ///     <list type="bullet">
    ///         <item>with the task list open, MiraAPI places the closed tab of the role 0.2 lower than the open one;</item>
    ///         <item>with the task list closed, at a fixed place (2), whatever the length of the tab of the tasks:
    ///         placed below it, with the gap measured with the task list open.</item>
    ///     </list>
    ///     When the task list opens or closes, the tab of the role moves with it.
    ///     Done after the update of MiraAPI: a patch on CustomRoleManager itself starts it too early, when the mod
    ///     loads, and the mod fails to load.
    /// </summary>
    [HarmonyPatch(typeof(MiraHudPatches), nameof(MiraHudPatches.UpdatePostfix))]
    [HarmonyPostfix]
    public static void UpdatePostfixPostfix()
    {
        var taskPanel = HudManager.InstanceExists ? HudManager.Instance.TaskPanel : null;
        if (!MiraHudPatches.RoleTab || !taskPanel) return;

        var panel = MiraHudPatches.RoleTab!;

        // Depths of the task list from its top, in world units: the tab only when closed (the list is out of the
        // screen), the list and its tab when open.
        var top = taskPanel!.transform.position.y;
        var tabDepth = top - taskPanel.tab.bounds.min.y;
        var openDepth = Mathf.Max(tabDepth, top - taskPanel.background.bounds.min.y);
        if (taskPanel.open && taskPanel.timer >= 1f)
            _gap = panel.openPosition.y - taskPanel.openPosition.y - openDepth;

        // Moved with the task list: same timer and same curve as its slide.
        var closedY = taskPanel.openPosition.y + tabDepth + _gap;
        var openY = taskPanel.openPosition.y + openDepth + _gap;
        var y = Mathf.SmoothStep(closedY, openY, taskPanel.timer);

        if (!Mathf.Approximately(panel.openPosition.y, y))
            panel.openPosition = new Vector3(panel.openPosition.x, y, panel.openPosition.z);
        if (!Mathf.Approximately(panel.closedPosition.y, y))
            panel.closedPosition = new Vector3(panel.closedPosition.x, y, panel.closedPosition.z);
    }

    // The role while alive of the dead local player, when it has a tab and the setting keeps it.
    private static ICustomRole? GetAliveRole()
    {
        var player = PlayerControl.LocalPlayer;
        if (!player || !player.Data || !player.Data.IsDead || !ShipStatus.Instance) return null;

        var settings = LocalSettingsTabSingleton<BorLocalSettingsTab>.Instance;
        if (settings == null || !settings.GhostsKeepRoleTab.Value) return null;

        var aliveRole = AliveRolePatches.Get(player);
        if (aliveRole == null || !CustomRoleManager.GetCustomRoleBehaviour(aliveRole.Value, out var role)) return null;

        return role is { Configuration.RoleHintType: RoleHintType.RoleTab } ? role : null;
    }
}
