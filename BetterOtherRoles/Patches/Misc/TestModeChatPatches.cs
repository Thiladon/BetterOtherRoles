using System.Globalization;
using AmongUs.GameOptions;
using System.Text;
using BetterOtherRoles.Options;
using BetterOtherRoles.Patches.Roles;
using BetterOtherRoles.Utilities;
using HarmonyLib;
using MiraAPI.Hud;
using MiraAPI.Roles;
using MiraAPI.Translation;
using Reactor.Networking.Attributes;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Test mode (<see cref="BorDevOptions.TestMode"/>): the host keeps the chat during the game,
///     and can use the dev commands (/setrole [player] {role}, /resetcd [player], /revive [player],
///     /endgame [crewmate|impostor]),
///     on the host by default when no player is given.
/// </summary>
[HarmonyPatch]
public static class TestModeChatPatches
{
    private const string SetRoleCommand = "/setrole";
    private const string EndGameCommand = "/endgame";
    private const string ResetCooldownCommand = "/resetcd";
    private const string ReviveCommand = "/revive";

    private static bool CanUseDevCommands => BorDevOptions.IsTestModeActive &&
                                            AmongUsClient.Instance.AmHost &&
                                            AmongUsClient.Instance.IsGameStarted;

    /// <summary>
    ///     Shows the chat to the host once the intro is over.
    /// </summary>
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    [HarmonyPostfix]
    public static void HudManagerUpdatePostfix(HudManager __instance)
    {
        if (IntroStatePatches.IntroEnded && CanUseDevCommands && !__instance.Chat.chatButton.gameObject.activeSelf)
        {
            __instance.Chat.SetVisible(true);
        }
    }

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.SendChat))]
    [HarmonyPrefix]
    public static bool SendChatPrefix(ChatController __instance)
    {
        var text = __instance.freeChatField.Text.Trim();
        var isSetRole = text.StartsWith(SetRoleCommand, StringComparison.OrdinalIgnoreCase);
        var isEndGame = text.StartsWith(EndGameCommand, StringComparison.OrdinalIgnoreCase);
        var isResetCooldown = text.StartsWith(ResetCooldownCommand, StringComparison.OrdinalIgnoreCase);
        var isRevive = text.StartsWith(ReviveCommand, StringComparison.OrdinalIgnoreCase);
        if (!isSetRole && !isEndGame && !isResetCooldown && !isRevive)
        {
            return true;
        }

        if (!CanUseDevCommands)
        {
            Reply(MiraLocaleManager.Get("BetterOtherRoles.Dev.Chat.NotAvailable", "Command only available to the host, in game, with the test mode."));
        }
        else if (isSetRole)
        {
            SetRole(text[SetRoleCommand.Length..].Trim());
        }
        else if (isResetCooldown)
        {
            ResetCooldown(text[ResetCooldownCommand.Length..].Trim());
        }
        else if (isRevive)
        {
            Revive(text[ReviveCommand.Length..].Trim());
        }
        else
        {
            EndGame(text[EndGameCommand.Length..].Trim());
        }

        __instance.freeChatField.Clear();
        __instance.quickChatMenu.Clear();
        __instance.quickChatField.Clear();
        __instance.UpdateChatMode();
        return false;
    }

    /// <summary>
    ///     /endgame [crewmate|impostor]: ends the game despite the test mode, won by the crewmates by default.
    /// </summary>
    private static void EndGame(string args)
    {
        var winner = Normalize(args);
        GameOverReason reason;
        if (winner is "" or "crewmate" or "crewmates" or "crew")
        {
            reason = GameOverReason.CrewmatesByVote;
        }
        else if (winner is "impostor" or "impostors" or "imp")
        {
            reason = GameOverReason.ImpostorsByKill;
        }
        else
        {
            Reply(MiraLocaleManager.Get("BetterOtherRoles.Dev.Chat.EndGameUsage", "Usage: /endgame [crewmate|impostor]."));
            return;
        }

        TestModePatches.ForceEndGame(reason);
    }

    /// <summary>
    ///     Parses "[Player] {Role}". Both names may contain spaces: the longest player name matching the start wins;
    ///     without a player name, the host (who typed the command) gets the role.
    /// </summary>
    private static void SetRole(string args)
    {
        var player = GetPlayers()
            .FirstOrDefault(x => args.StartsWith(x.Data.PlayerName + " ", StringComparison.OrdinalIgnoreCase));
        var roleArgs = player ? args[(player!.Data.PlayerName.Length + 1)..] : args;
        player ??= PlayerControl.LocalPlayer;

        if (Normalize(roleArgs).Length == 0)
        {
            Reply(MiraLocaleManager.Get("BetterOtherRoles.Dev.Chat.SetRoleUsage", "Usage: /setrole [player] {role}."));
            return;
        }

        var roleName = Normalize(roleArgs);
        // Modded roles, plus the vanilla Crewmate and Impostor: the other vanilla roles are left out,
        // as some modded roles will have the same name (Engineer...).
        // RoleManager.AllRoles gives plain RoleBehaviours (never ICustomRole): take the modded ones from MiraAPI.
        var roles = CustomRoleManager.CustomRoleBehaviours
            .Where(x => !x.IsDead)
            .Append(RoleManager.Instance.GetRole(RoleTypes.Crewmate))
            .Append(RoleManager.Instance.GetRole(RoleTypes.Impostor))
            .ToArray();
        var role = roles.FirstOrDefault(x => Normalize(GetCommandName(x)) == roleName);

        if (role == null)
        {
            Reply(MiraLocaleManager.Get("BetterOtherRoles.Dev.Chat.RoleNotFound", "Role not found. Roles: {0}")
                .Replace("{0}", string.Join(", ", roles.Select(GetCommandName).Distinct().OrderBy(x => x))));
            return;
        }

        player.RpcChangeRole((ushort)role.Role, readyButtons: true);
        Reply(MiraLocaleManager.Get("BetterOtherRoles.Dev.Chat.RoleSet", "{0} is now {1}.")
            .Replace("{0}", player.Data.PlayerName)
            .Replace("{1}", GetCommandName(role)));
    }

    /// <summary>
    ///     /resetcd [Player]: makes the buttons of the player (the host by default) ready again,
    ///     their role buttons and the kill button, and for an impostor the sabotages and doors.
    /// </summary>
    private static void ResetCooldown(string args)
    {
        var player = FindPlayerOrSelf(args);
        if (!player)
        {
            Reply(MiraLocaleManager.Get("BetterOtherRoles.Dev.Chat.ResetCooldownUsage", "Usage: /resetcd [player] (player not found)."));
            return;
        }

        player!.RpcResetCooldowns();
        Reply(MiraLocaleManager.Get("BetterOtherRoles.Dev.Chat.CooldownReset", "Cooldowns of {0} reset.")
            .Replace("{0}", player.Data.PlayerName));
    }

    /// <summary>
    ///     Resets the cooldowns of a player, sent by the host to everyone: only this player resets their buttons,
    ///     everyone resets the sabotages and doors of an impostor (the host keeps their timers, the others check them).
    /// </summary>
    [MethodRpc((uint)BorRpc.ResetCooldowns)]
    public static void RpcResetCooldowns(this PlayerControl player)
    {
        if (!player || player.Data?.Role == null)
        {
            return;
        }

        if (player.Data.Role.IsImpostor)
        {
            ResetSabotages();
        }

        if (!player.AmOwner)
        {
            return;
        }

        foreach (var button in CustomButtonManager.Buttons.Where(x => x.Enabled(player.Data.Role)))
        {
            button.SetTimer(0f);
        }

        if (player.Data.Role.CanUseKillButton)
        {
            player.SetKillTimer(0f);
        }
    }

    /// <summary>
    ///     /revive [Player]: brings the player (the host by default) back to life where their body lies,
    ///     with the role they had alive.
    /// </summary>
    private static void Revive(string args)
    {
        var player = FindPlayerOrSelf(args);
        if (!player)
        {
            Reply(MiraLocaleManager.Get("BetterOtherRoles.Dev.Chat.ReviveUsage", "Usage: /revive [player] (player not found)."));
            return;
        }

        if (!player!.Data.IsDead)
        {
            Reply(MiraLocaleManager.Get("BetterOtherRoles.Dev.Chat.NotDead", "{0} is not dead.")
                .Replace("{0}", player.Data.PlayerName));
            return;
        }

        player.RpcRevive();
        Reply(MiraLocaleManager.Get("BetterOtherRoles.Dev.Chat.Revived", "{0} is alive again.")
            .Replace("{0}", player.Data.PlayerName));
    }

    /// <summary>
    ///     Revives a player, sent by the host to everyone: the body is removed and the player stands in its place
    ///     (where they are when there is no body, ejected for instance), with their role when alive.
    /// </summary>
    [MethodRpc((uint)BorRpc.Revive)]
    public static void RpcRevive(this PlayerControl player)
    {
        if (!player || player.Data == null || !player.Data.IsDead)
        {
            return;
        }

        var body = UnityEngine.Object.FindObjectsOfType<DeadBody>().FirstOrDefault(x => x.ParentId == player.PlayerId);
        Vector2? position = body ? body!.TruePosition : null;
        if (body)
        {
            UnityEngine.Object.Destroy(body!.gameObject);
        }

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
            {
                player.NetTransform.SnapTo(place);
            }

            Physics2D.SyncTransforms();
        }

        // Back alive: no chat in game (the host in test mode gets it back).
        if (player.AmOwner && HudManager.Instance && !MeetingHud.Instance)
        {
            HudManager.Instance.Chat.SetVisible(false);
        }
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

    // The player named in the arguments (exact name, any case), or the host without arguments.
    private static PlayerControl? FindPlayerOrSelf(string args)
    {
        return args.Length == 0
            ? PlayerControl.LocalPlayer
            : GetPlayers().FirstOrDefault(x => string.Equals(x.Data.PlayerName, args, StringComparison.OrdinalIgnoreCase));
    }

    // Sabotage cooldown, and door cooldowns: by door (The Skeld, dlekS) or by room (other maps).
    private static void ResetSabotages()
    {
        if (!ShipStatus.Instance)
        {
            return;
        }

        if (ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Sabotage, out var sabotage))
        {
            sabotage.Cast<SabotageSystemType>().Timer = 0f;
        }

        if (!ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Doors, out var doors))
        {
            return;
        }

        var autoDoors = doors.TryCast<AutoDoorsSystemType>();
        if (autoDoors != null)
        {
            autoDoors.initialCooldown = 0f;
            foreach (var door in ShipStatus.Instance.AllDoors)
            {
                var autoDoor = door ? door.TryCast<AutoOpenDoor>() : null;
                if (autoDoor)
                {
                    autoDoor!.CooldownTimer = 0f;
                }
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

    // Players still in the game, the longest names first (a name may start with another one).
    private static IEnumerable<PlayerControl> GetPlayers()
    {
        return PlayerControl.AllPlayerControls.ToArray()
            .Where(x => x.Data != null && !x.Data.Disconnected)
            .OrderByDescending(x => x.Data.PlayerName.Length);
    }

    /// <summary>
    ///     English name of a role, whatever the game language: class name without "Role" for the modded roles
    ///     (SheriffRole: Sheriff), RoleTypes name for the vanilla ones (Crewmate, Impostor).
    /// </summary>
    private static string GetCommandName(RoleBehaviour role)
    {
        if (role is not ICustomRole)
        {
            return role.Role.ToString();
        }

        var typeName = role.GetIl2CppType().Name;
        return typeName.EndsWith("Role", StringComparison.Ordinal) ? typeName[..^4] : typeName;
    }

    /// <summary>
    ///     Lower case, without spaces nor accents ("Shérif" == "sherif").
    /// </summary>
    private static string Normalize(string text)
    {
        var builder = new StringBuilder();
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (!char.IsWhiteSpace(c) && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }

    private static void Reply(string message)
    {
        HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, message);
    }
}
