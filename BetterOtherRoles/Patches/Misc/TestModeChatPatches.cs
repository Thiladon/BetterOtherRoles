using System.Globalization;
using AmongUs.GameOptions;
using System.Text;
using BetterOtherRoles.Options;
using BetterOtherRoles.Utilities;
using HarmonyLib;
using MiraAPI.Roles;
using MiraAPI.Translation;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Test mode (<see cref="BorDevOptions.TestMode"/>): the host keeps the chat during the game,
///     and can use the dev commands (/setRole {Player} {Role}, /endGame [Crewmate|Impostor]).
/// </summary>
[HarmonyPatch]
public static class TestModeChatPatches
{
    private const string SetRoleCommand = "/setrole";
    private const string EndGameCommand = "/endgame";

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
        if (!isSetRole && !isEndGame)
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
    ///     /endGame [Crewmate|Impostor]: ends the game despite the test mode, won by the crewmates by default.
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
            Reply(MiraLocaleManager.Get("BetterOtherRoles.Dev.Chat.EndGameUsage", "Usage: /endGame [Crewmate|Impostor]."));
            return;
        }

        TestModePatches.ForceEndGame(reason);
    }

    /// <summary>
    ///     Parses "{Player} {Role}". Both names may contain spaces: the longest player name matching the start wins.
    /// </summary>
    private static void SetRole(string args)
    {
        var player = PlayerControl.AllPlayerControls.ToArray()
            .Where(x => x.Data != null && !x.Data.Disconnected)
            .OrderByDescending(x => x.Data.PlayerName.Length)
            .FirstOrDefault(x => args.StartsWith(x.Data.PlayerName + " ", StringComparison.OrdinalIgnoreCase));

        if (player == null)
        {
            Reply(MiraLocaleManager.Get("BetterOtherRoles.Dev.Chat.SetRoleUsage", "Usage: /setRole {Player} {Role} (player not found)."));
            return;
        }

        var roleName = Normalize(args[(player.Data.PlayerName.Length + 1)..]);
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
