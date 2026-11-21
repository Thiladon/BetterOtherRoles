using BetterOtherRoles.Modules.Chat;
using BetterOtherRoles.Options;
using HarmonyLib;
using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Roles;
using MiraAPI.Translation;
using MiraAPI.Utilities;
using UnityEngine;

namespace BetterOtherRoles.Patches.GameTweaks;

/// <summary>
///     Devices locked for the crewmates (TOU-Mira 1.7.9 Advanced Utility Options, UseButtonPatch), with
///     one lock per device (<see cref="DeviceLockOptions.LockAdmin"/>...): the admin, cams, door log and vitals open
///     once the player has done enough tasks and once enough rounds have passed (0: no such condition). A click on a locked device shows what is missing instead
///     of opening it. Impostors, neutrals and the dead use the devices freely.
///     A player with fewer tasks than asked unlocks the device with all of them done.
/// </summary>
[HarmonyPatch]
public static class DeviceLockPatches
{
    private enum Device
    {
        None,
        Admin,
        Cams,
        DoorLog,
        Vitals,
    }

    private static DeviceLockOptions Options => OptionGroupSingleton<DeviceLockOptions>.Instance;

    // Cams (with the binoculars of The Fungle), door log and vitals.
    [HarmonyPatch(typeof(SystemConsole), nameof(SystemConsole.Use))]
    [HarmonyPrefix]
    public static bool SystemConsoleUsePrefix(SystemConsole __instance) => TryUse(DeviceOf(__instance.UseIcon));

    // Admin table.
    [HarmonyPatch(typeof(MapConsole), nameof(MapConsole.Use))]
    [HarmonyPrefix]
    public static bool MapConsoleUsePrefix(MapConsole __instance) => TryUse(DeviceOf(__instance.UseIcon));

    private static Device DeviceOf(ImageNames icon) => icon switch
    {
        ImageNames.AdminMapButton or ImageNames.MIRAAdminButton or ImageNames.PolusAdminButton or ImageNames.AirshipAdminButton => Device.Admin,
        ImageNames.CamsButton => Device.Cams,
        ImageNames.DoorLogsButton => Device.DoorLog,
        ImageNames.VitalsButton => Device.Vitals,
        _ => Device.None,
    };

    // The device opens when unlocked; otherwise a notification tells what is missing.
    private static bool TryUse(Device device)
    {
        var player = PlayerControl.LocalPlayer;
        if (device == Device.None || !player || !player.Data || player.Data.IsDead || !IsCrewmate(player)) return true;

        var (locked, tasks, rounds) = Settings(device);
        if (!locked.Value) return true;

        var tasksLeft = TasksLeft(player, (int)tasks.Value);
        var roundsLocked = BorInfoHistory.CurrentRound <= (int)rounds.Value;
        if (tasksLeft <= 0 && !roundsLocked) return true;

        var lines = new List<string>();
        if (roundsLocked)
        {
            lines.Add(string.Format(BetterOtherRolesPlugin.Culture, MiraLocaleManager.Get("BetterOtherRoles.DeviceLock.Rounds", "Available from round {0}."),
                (int)rounds.Value + 1));
        }

        if (tasksLeft > 0)
        {
            var key = tasksLeft > 1 ? "BetterOtherRoles.DeviceLock.Tasks" : "BetterOtherRoles.DeviceLock.TaskSingle";
            lines.Add(string.Format(BetterOtherRolesPlugin.Culture, MiraLocaleManager.Get(key, "{0} more tasks to use {1}."), tasksLeft, Name(device)));
        }

        Helpers.CreateAndShowNotification($"<b>{string.Join("\n", lines)}</b>", Color.white, new Vector3(0f, 1f, -20f));
        return false;
    }

    // Lock, tasks to do and rounds locked of the device.
    private static (ModdedToggleOption Locked, ModdedNumberOption Tasks, ModdedNumberOption Rounds) Settings(Device device) => device switch
    {
        Device.Admin => (Options.LockAdmin, Options.TasksForAdmin, Options.RoundsForAdmin),
        Device.Cams => (Options.LockCams, Options.TasksForCams, Options.RoundsForCams),
        Device.DoorLog => (Options.LockDoorLog, Options.TasksForDoorLog, Options.RoundsForDoorLog),
        _ => (Options.LockVitals, Options.TasksForVitals, Options.RoundsForVitals),
    };

    // Tasks still to do (no more than the tasks of the player).
    private static int TasksLeft(PlayerControl player, int needed)
    {
        var tasks = player.Data.Tasks;
        var done = 0;
        foreach (var task in tasks)
        {
            if (task.Complete)
                done++;
        }

        return Math.Min(needed, tasks.Count) - done;
    }

    // Team of the role (the roles of a mod tell their team, the roles of the game their team type).
    private static bool IsCrewmate(PlayerControl player)
    {
        var role = player.Data.Role;
        if (!role) return false;

        return role is ICustomRole custom ? custom.Team == ModdedRoleTeams.Crewmate : role.TeamType == RoleTeamTypes.Crewmate;
    }

    private static string Name(Device device) => device switch
    {
        Device.Admin => MiraLocaleManager.Get("BetterOtherRoles.DeviceLock.Admin", "the admin table"),
        Device.Cams => MiraLocaleManager.Get("BetterOtherRoles.DeviceLock.Cams", "the cams"),
        Device.DoorLog => MiraLocaleManager.Get("BetterOtherRoles.DeviceLock.DoorLog", "the door log"),
        _ => MiraLocaleManager.Get("BetterOtherRoles.DeviceLock.Vitals", "the vitals"),
    };
}
