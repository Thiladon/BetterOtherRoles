using AmongUs.GameOptions;
using BetterOtherRoles.Options;
using BetterOtherRoles.Patches.Misc;
using BetterOtherRoles.Patches.Roles;
using BetterOtherRoles.Utilities;
using MiraAPI.Hud;
using MiraAPI.Networking;
using MiraAPI.Roles;
using Reactor.Networking.Attributes;
using UnityEngine;

namespace BetterOtherRoles.Modules.Debug;

/// <summary>
///     Debug Mode actions of the host (<see cref="BorDevOptions.TestMode"/>), used by the chat commands
///     (Modules/Commands/Debug) and the Debug menu (<see cref="BorDebugMenu"/>).
/// </summary>
public static class BorDebugActions
{
    /// <summary>
    ///     Gets a value indicating whether the debug actions are available: host with the admin key, in game or in
    ///     Freeplay (not "started" for the game), with the Debug Mode.
    /// </summary>
    public static bool CanUse => BorDevOptions.IsTestModeActive &&
                                 AmongUsClient.Instance.AmHost &&
                                 (AmongUsClient.Instance.IsGameStarted || TutorialManager.InstanceExists);

    /// <summary>
    ///     Gets the roles a player can be given: the modded roles, plus the vanilla Crewmate and Impostor (the other
    ///     vanilla roles are left out, as some modded roles will have the same name: Engineer...).
    ///     RoleManager.AllRoles gives plain RoleBehaviours (never ICustomRole): the modded ones come from MiraAPI.
    /// </summary>
    /// <returns>The roles.</returns>
    public static RoleBehaviour[] GetRoles()
    {
        return CustomRoleManager.CustomRoleBehaviours
            .Where(x => !x.IsDead)
            .Append(RoleManager.Instance.GetRole(RoleTypes.Crewmate))
            .Append(RoleManager.Instance.GetRole(RoleTypes.Impostor))
            .ToArray();
    }

    /// <summary>
    ///     Gives a role to a player, buttons ready.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="role">The role.</param>
    public static void SetRole(PlayerControl player, RoleBehaviour role)
    {
        player.RpcChangeRole((ushort)role.Role, readyButtons: true);
    }

    /// <summary>
    ///     Ends the game despite the Debug Mode.
    /// </summary>
    /// <param name="reason">The reason, which gives the winners.</param>
    public static void EndGame(GameOverReason reason)
    {
        TestModePatches.ForceEndGame(reason);
    }

    /// <summary>
    ///     Kills a player where they stand, as if they killed themselves (nobody moves, no kill cooldown reset).
    /// </summary>
    /// <param name="player">The player.</param>
    public static void Kill(PlayerControl player)
    {
        player.RpcCustomMurder(player, MeetingCheck.OutsideMeeting, resetKillTimer: false, teleportMurderer: false);
    }

    /// <summary>
    ///     Calls an emergency meeting from the host, without going to the button. A dead host cannot call one in the
    ///     game: a living player calls it instead.
    /// </summary>
    public static void StartMeeting()
    {
        if (MeetingHud.Instance || ExileController.Instance) return;

        var caller = PlayerControl.LocalPlayer;
        if (caller.Data.IsDead)
            caller = PlayerControl.AllPlayerControls.ToArray().FirstOrDefault(x => x.Data != null && !x.Data.IsDead && !x.Data.Disconnected);
        if (!caller) return;

        MeetingRoomManager.Instance.AssignSelf(caller, null);
        HudManager.Instance.OpenMeetingRoom(caller);
        caller!.RpcStartMeeting(null);
    }

    /// <summary>
    ///     Finishes the tasks of a player, sent by the host to everyone: the player completes them themselves (their
    ///     tasks are theirs to send).
    /// </summary>
    /// <param name="player">The player.</param>
    [MethodRpc((uint)BorRpc.FinishTasks)]
    public static void RpcFinishTasks(this PlayerControl player)
    {
        if (!player || !player.AmOwner || player.Data?.Tasks == null) return;

        foreach (var task in player.Data.Tasks.ToArray())
        {
            if (!task.Complete)
                player.RpcCompleteTask(task.Id);
        }
    }

    /// <summary>
    ///     Gives a player its tasks back from the start, sent by the host to everyone: each client marks them not done
    ///     and makes new ones in their place, as the game does at the start (PlayerControl.CoSetTasks).
    ///     The type of a task is read on the task itself (Index): the task list of the player data loses it once synced
    ///     (NetworkedPlayerInfo.Deserialize only sends the id and whether it is done).
    /// </summary>
    /// <param name="player">The player.</param>
    [MethodRpc((uint)BorRpc.ResetTasks)]
    public static void RpcResetTasks(this PlayerControl player)
    {
        if (!player || player.Data?.Tasks == null || !ShipStatus.Instance) return;

        foreach (var info in player.Data.Tasks)
        {
            info.Complete = false;
        }

        for (var i = 0; i < player.myTasks.Count; i++)
        {
            var old = player.myTasks[i] ? player.myTasks[i].TryCast<NormalPlayerTask>() : null;
            if (!old || player.Data.FindTaskById(old!.Id) == null) continue;

            var task = CreateTask(player, (byte)old.Index, old.Id);
            if (!task) continue;

            CloseTaskMinigame(old);
            player.myTasks[i] = task;
            UnityEngine.Object.Destroy(old.gameObject);
        }

        GameData.Instance.RecomputeTaskCounts();
    }

    /// <summary>
    ///     Gets the tasks of the map a player does not have yet (type of task).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The tasks, models of the map.</returns>
    public static NormalPlayerTask[] GetMissingTasks(PlayerControl player)
    {
        if (!ShipStatus.Instance) return [];

        var owned = GetTasks(player).Select(x => x.Index).ToHashSet();
        return ShipStatus.Instance.CommonTasks
            .Concat(ShipStatus.Instance.LongTasks)
            .Concat(ShipStatus.Instance.ShortTasks)
            .Where(x => x && !owned.Contains(x.Index))
            .ToArray();
    }

    /// <summary>
    ///     Gets the tasks of a player (the normal ones, those of the task list).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The tasks.</returns>
    public static NormalPlayerTask[] GetTasks(PlayerControl player)
    {
        return player.myTasks.ToArray()
            .Select(x => x ? x.TryCast<NormalPlayerTask>() : null)
            .Where(x => x && player.Data.FindTaskById(x!.Id) != null)
            .Select(x => x!)
            .ToArray();
    }

    /// <summary>
    ///     Gives a player one more task, sent by the host to everyone: added to the task list of the player data, with
    ///     the next id, and made as the game does at the start (PlayerControl.CoSetTasks).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="typeId">The type of the task (NormalPlayerTask.Index on the map).</param>
    [MethodRpc((uint)BorRpc.AddTask)]
    public static void RpcAddTask(this PlayerControl player, byte typeId)
    {
        if (!player || player.Data?.Tasks == null || !ShipStatus.Instance) return;

        uint id = 0;
        foreach (var info in player.Data.Tasks)
        {
            id = Math.Max(id, info.Id + 1);
        }

        var task = CreateTask(player, typeId, id);
        if (!task) return;

        player.Data.Tasks.Add(new NetworkedPlayerInfo.TaskInfo(typeId, id));
        player.myTasks.Add(task);
        GameData.Instance.RecomputeTaskCounts();
    }

    /// <summary>
    ///     Takes a task away from a player, sent by the host to everyone: removed from the task list of the player data
    ///     and from its tasks (its window closed when open).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="taskId">The id of the task.</param>
    [MethodRpc((uint)BorRpc.RemoveTask)]
    public static void RpcRemoveTask(this PlayerControl player, uint taskId)
    {
        if (!player || player.Data?.Tasks == null) return;

        for (var i = player.Data.Tasks.Count - 1; i >= 0; i--)
        {
            if (player.Data.Tasks[i].Id == taskId)
                player.Data.Tasks.RemoveAt(i);
        }

        for (var i = player.myTasks.Count - 1; i >= 0; i--)
        {
            var task = player.myTasks[i] ? player.myTasks[i].TryCast<NormalPlayerTask>() : null;
            if (!task || task!.Id != taskId) continue;

            CloseTaskMinigame(task);
            player.myTasks.RemoveAt(i);
            UnityEngine.Object.Destroy(task.gameObject);
        }

        GameData.Instance.RecomputeTaskCounts();
    }

    // A task of the map made for a player, as in PlayerControl.CoSetTasks.
    private static NormalPlayerTask? CreateTask(PlayerControl player, byte typeId, uint id)
    {
        var prefab = ShipStatus.Instance.GetTaskById(typeId);
        if (!prefab) return null;

        var task = UnityEngine.Object.Instantiate(prefab, player.transform);
        task.Id = id;
        task.Index = prefab.Index;
        task.Owner = player;
        task.Initialize();
        return task;
    }

    // The window of a task being done is closed before the task goes away.
    private static void CloseTaskMinigame(PlayerTask task)
    {
        var minigame = Minigame.Instance;
        if (minigame && minigame.MyTask != null && minigame.MyTask.Pointer == task.Pointer)
            minigame.Close();
    }

    /// <summary>
    ///     Moves a player to another one.
    /// </summary>
    /// <param name="player">The player who moves.</param>
    /// <param name="target">The player they go to.</param>
    public static void Teleport(PlayerControl player, PlayerControl target)
    {
        player.RpcTeleport(target.GetTruePosition() - player.Collider.offset);
    }

    /// <summary>
    ///     Moves a player, sent by the host to everyone: the player moves themselves (their position is theirs to send).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="position">The new position of the transform.</param>
    [MethodRpc((uint)BorRpc.Teleport)]
    public static void RpcTeleport(this PlayerControl player, Vector2 position)
    {
        if (player && player.AmOwner && player.NetTransform)
            player.NetTransform.RpcSnapTo(position);
    }

    /// <summary>
    ///     Resets the cooldowns of a player, sent by the host to everyone: only this player resets their buttons,
    ///     everyone resets the sabotages and doors of an impostor (the host keeps their timers, the others check them).
    /// </summary>
    /// <param name="player">The player.</param>
    [MethodRpc((uint)BorRpc.ResetCooldowns)]
    public static void RpcResetCooldowns(this PlayerControl player)
    {
        if (!player || player.Data?.Role == null) return;

        if (player.Data.Role.IsImpostor)
            ResetSabotages();

        if (!player.AmOwner) return;

        foreach (var button in CustomButtonManager.Buttons.Where(x => x.Enabled(player.Data.Role)))
        {
            button.SetTimer(0f);
        }

        if (player.Data.Role.CanUseKillButton)
            player.SetKillTimer(0f);
    }

    /// <summary>
    ///     Revives a player, sent by the host to everyone: the body is removed and the player stands in its place
    ///     (where they are when there is no body, ejected for instance), with their role when alive.
    /// </summary>
    /// <param name="player">The player.</param>
    [MethodRpc((uint)BorRpc.Revive)]
    public static void RpcRevive(this PlayerControl player)
    {
        if (!player || player.Data == null || !player.Data.IsDead) return;

        var body = UnityEngine.Object.FindObjectsOfType<DeadBody>().FirstOrDefault(x => x.ParentId == player.PlayerId);
        Vector2? position = body ? body!.TruePosition : null;
        if (body)
            UnityEngine.Object.Destroy(body!.gameObject);

        // RoleWhenAlive of the game reads as Crewmate through Il2Cpp: our own record (impostor or crewmate by default).
        var role = AliveRolePatches.Get(player) ??
                   (player.Data.Role.TeamType == RoleTeamTypes.Impostor ? RoleTypes.Impostor : RoleTypes.Crewmate);

        player.Revive();
        RemoveGhostText(player);
        player.ChangeRole((ushort)role);

        if (position != null && !MeetingHud.Instance && !ExileController.Instance)
        {
            // TruePosition is the feet: the transform is a little higher (collider offset).
            var place = position.Value - player.Collider.offset;
            player.transform.position = new Vector3(place.x, place.y, player.transform.position.z);
            player.MyPhysics.body.position = place;
            if (!player.AmOwner && player.NetTransform)
                player.NetTransform.SnapTo(place);

            Physics2D.SyncTransforms();
        }

        // Back alive: no chat in game (the host in test mode gets it back).
        if (player.AmOwner && HudManager.Instance && !MeetingHud.Instance)
            HudManager.Instance.Chat.SetVisible(false);
    }

    // The text given at death ("You're dead, finish your tasks", "You're dead. You can still sabotage"...) stays after
    // a revive: removed.
    private static void RemoveGhostText(PlayerControl player)
    {
        var ghostTexts = new[] { StringNames.GhostDoTasks, StringNames.GhostIgnoreTasks, StringNames.GhostImpostor }
            .Select(x => TranslationController.Instance.GetString(x))
            .ToArray();
        for (var i = player.myTasks.Count - 1; i >= 0; i--)
        {
            var task = player.myTasks[i];
            var text = task ? task.TryCast<ImportantTextTask>() : null;
            if (text && ghostTexts.Contains(text!.Text))
            {
                player.myTasks.RemoveAt(i);
                UnityEngine.Object.Destroy(text.gameObject);
            }
        }
    }

    // Sabotage cooldown, and door cooldowns: by door (The Skeld, dlekS) or by room (other maps).
    private static void ResetSabotages()
    {
        if (!ShipStatus.Instance) return;

        if (ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Sabotage, out var sabotage))
            sabotage.Cast<SabotageSystemType>().Timer = 0f;

        if (!ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Doors, out var doors)) return;

        var autoDoors = doors.TryCast<AutoDoorsSystemType>();
        if (autoDoors != null)
        {
            autoDoors.initialCooldown = 0f;
            foreach (var door in ShipStatus.Instance.AllDoors)
            {
                var autoDoor = door ? door.TryCast<AutoOpenDoor>() : null;
                if (autoDoor)
                    autoDoor!.CooldownTimer = 0f;
            }
        }

        var manualDoors = doors.TryCast<DoorsSystemType>();
        if (manualDoors != null)
        {
            manualDoors.initialCooldown = 0f;
            var rooms = new List<SystemTypes>();
            foreach (var timer in manualDoors.timers)
            {
                rooms.Add(timer.Key);
            }

            foreach (var room in rooms)
            {
                manualDoors.timers[room] = 0f;
            }
        }
    }
}
