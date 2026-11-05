using AmongUs.GameOptions;
using BetterOtherRoles.Patches.Misc;
using MiraAPI.Hud;
using Reactor.Networking.Attributes;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Utilities;

public static class BorPlayerExtensions
{
    /// <summary>
    ///     Changes the role of a player during the game, for everyone.
    ///     Taken from TOU-Mira 1.7.9 (Extensions.RpcChangeRole), without their role history and event.
    /// </summary>
    /// <param name="player">The player whose role changes.</param>
    /// <param name="newRoleType">The new role.</param>
    /// <param name="readyButtons">
    ///     <see langword="true"/> to make the buttons of the new role usable right away (test command),
    ///     instead of starting from their initial cooldown.
    /// </param>
    [MethodRpc((uint)BorRpc.ChangeRole)]
    public static void RpcChangeRole(this PlayerControl player, ushort newRoleType, bool readyButtons)
    {
        ChangeRole(player, newRoleType, readyButtons);
    }

    public static void ChangeRole(this PlayerControl player, ushort newRoleType, bool readyButtons = false)
    {
        player.roleAssigned = false;

        var data = player.Data;
        if (data.Role)
            data.Role.Deinitialize(player);

        var oldRole = data.Role;
        var newRole = RoleManager.Instance.GetRole((RoleTypes)newRoleType);
        var roleBehaviour = Object.Instantiate(newRole, data.gameObject.transform);
        roleBehaviour.Initialize(player);

        if (player.AmOwner && HudManager.Instance)
            HudManager.Instance.SetHudActive(player, roleBehaviour, !MeetingHud.Instance && !ExileController.Instance);

        data.Role = roleBehaviour;
        data.RoleType = roleBehaviour.Role;

        if (!roleBehaviour.IsDead)
            data.RoleWhenAlive = new Il2CppSystem.Nullable<RoleTypes>(roleBehaviour.Role);

        roleBehaviour.AdjustTasks(player);
        roleBehaviour.SpawnTaskHeader(player);

        if (player.AmOwner)
            ResetButtons(oldRole, roleBehaviour, readyButtons);

        NameColorPatches.Refresh();
    }

    /// <summary>
    ///     MiraAPI buttons keep their state (uses, cooldown) when their role is lost: start the new role from scratch.
    ///     A button already there with the old role keeps its state (handcuffs of the promoted Deputy),
    ///     except with the test command, which always resets.
    /// </summary>
    private static void ResetButtons(RoleBehaviour? oldRole, RoleBehaviour role, bool readyButtons)
    {
        foreach (var button in CustomButtonManager.Buttons.Where(x => x.Enabled(role)))
        {
            if (!readyButtons && oldRole && button.Enabled(oldRole)) continue;

            if (button.LimitedUses)
                button.SetUses(button.MaxUses);

            button.EffectActive = false;
            button.SetTimer(readyButtons ? 0f : button.InitialCooldown);
        }
    }
}
