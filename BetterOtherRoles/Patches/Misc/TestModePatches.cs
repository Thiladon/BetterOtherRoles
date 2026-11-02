using System.Globalization;
using System.Text;
using BetterOtherRoles.Assets;
using BetterOtherRoles.Modules.Admin;
using BetterOtherRoles.Modules.Debug;
using BetterOtherRoles.Options;
using HarmonyLib;
using MiraAPI.GameOptions;
using MiraAPI.Roles;
using MiraAPI.Translation;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Debug Mode (<see cref="BorDevOptions.TestMode"/>), for the host with the admin key:
///     - game rules: start with less than 4 players, and no game end;
///     - chat kept during the game, with the dev commands (/setrole [player] {role}, /resetcd [player], /revive [player],
///       /endgame [crewmate|impostor]), on the host by default when no player is given;
///     - Debug button under the role guide button, opening the Debug menu (<see cref="BorDebugMenu"/>).
/// </summary>
[HarmonyPatch]
public static class TestModePatches
{
    private const string SetRoleCommand = "/setrole";
    private const string EndGameCommand = "/endgame";
    private const string ResetCooldownCommand = "/resetcd";
    private const string ReviveCommand = "/revive";

    private static int _vanillaMinPlayers = -1;
    private static bool _forceEndGame;
    private static GameObject? _debugButton;

    // ---------------------------------------------------------------------------------------------------------------
    // Game rules
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>
    ///     Ends the game despite the test mode (/endGame command).
    /// </summary>
    public static void ForceEndGame(GameOverReason reason)
    {
        _forceEndGame = true;
        try
        {
            GameManager.Instance.RpcEndGame(reason, false);
        }
        finally
        {
            _forceEndGame = false;
        }
    }

    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Update))]
    [HarmonyPrefix]
    public static void GameStartManagerUpdatePrefix(GameStartManager __instance)
    {
        if (_vanillaMinPlayers < 0)
        {
            _vanillaMinPlayers = __instance.MinPlayers;
        }

        TurnOffWithoutAdminKey();

        var minPlayers = BorDevOptions.IsTestModeActive ? 1 : _vanillaMinPlayers;
        if (__instance.MinPlayers == minPlayers)
        {
            return;
        }

        // The start button is only refreshed when the player count changes: force it.
        __instance.MinPlayers = minPlayers;
        __instance.LastPlayerCount = -1;
    }

    // A host without the admin key cannot use the Debug Mode: the saved value goes back to false.
    // Once the local player exists: SetValue sends the new value through it.
    private static void TurnOffWithoutAdminKey()
    {
        var option = OptionGroupSingleton<BorDevOptions>.Instance.TestMode;
        if (AmongUsClient.Instance.AmHost && !BorAdmin.IsAdmin && option.Value && PlayerControl.LocalPlayer)
        {
            option.SetValue(false);
        }
    }

    /// <summary>
    ///     After an ejection, the game only gives the control back if the game is not lost by deaths (impostors as many
    ///     as the crewmates): it then waits for the game end, blocked here, and stays on a black screen.
    /// </summary>
    [HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.IsGameOverDueToDeath))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static bool IsGameOverDueToDeathPrefix(ref bool __result)
    {
        if (!BorDevOptions.IsTestModeActive)
        {
            return true;
        }

        __result = false;
        return false;
    }

    /// <summary>
    ///     Before opening a meeting, the host checks the task win: alone as an impostor, there is no crewmate task, so
    ///     the tasks count as done, the game end is blocked and the meeting never opens ("Waiting for host").
    /// </summary>
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.CheckTaskCompletion))]
    [HarmonyPrefix]
    public static bool CheckTaskCompletionPrefix(ref bool __result)
    {
        if (!BorDevOptions.IsTestModeActive)
        {
            return true;
        }

        __result = false;
        return false;
    }

    /// <summary>
    ///     Every game end (votes, kills, tasks, sabotages, MiraAPI game overs) goes through RpcEndGame.
    /// </summary>
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.RpcEndGame))]
    [HarmonyPrefix]
    public static bool RpcEndGamePrefix()
    {
        return _forceEndGame || !BorDevOptions.IsTestModeActive;
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Chat commands
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>
    ///     Shows the chat to the host once the intro is over.
    /// </summary>
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    [HarmonyPostfix]
    public static void HudManagerUpdatePostfix(HudManager __instance)
    {
        if (IntroStatePatches.IntroEnded && BorDebugActions.CanUse && !__instance.Chat.chatButton.gameObject.activeSelf)
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

        if (!BorDebugActions.CanUse)
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
    ///     English name of a role, whatever the game language: class name without "Role" for the modded roles
    ///     (SheriffRole: Sheriff), RoleTypes name for the vanilla ones (Crewmate, Impostor).
    /// </summary>
    public static string GetCommandName(RoleBehaviour role)
    {
        if (role is not ICustomRole)
        {
            return role.Role.ToString();
        }

        var typeName = role.GetIl2CppType().Name;
        return typeName.EndsWith("Role", StringComparison.Ordinal) ? typeName[..^4] : typeName;
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

        BorDebugActions.EndGame(reason);
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
        var roles = BorDebugActions.GetRoles();
        var role = roles.FirstOrDefault(x => Normalize(GetCommandName(x)) == roleName);

        if (role == null)
        {
            Reply(MiraLocaleManager.Get("BetterOtherRoles.Dev.Chat.RoleNotFound", "Role not found. Roles: {0}")
                .Replace("{0}", string.Join(", ", roles.Select(GetCommandName).Distinct().OrderBy(x => x))));
            return;
        }

        BorDebugActions.SetRole(player, role);
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

    // The player named in the arguments (exact name, any case), or the host without arguments.
    private static PlayerControl? FindPlayerOrSelf(string args)
    {
        return args.Length == 0
            ? PlayerControl.LocalPlayer
            : GetPlayers().FirstOrDefault(x => string.Equals(x.Data.PlayerName, args, StringComparison.OrdinalIgnoreCase));
    }

    // Players still in the game, the longest names first (a name may start with another one).
    private static IEnumerable<PlayerControl> GetPlayers()
    {
        return PlayerControl.AllPlayerControls.ToArray()
            .Where(x => x.Data != null && !x.Data.Disconnected)
            .OrderByDescending(x => x.Data.PlayerName.Length);
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

    // ---------------------------------------------------------------------------------------------------------------
    // Debug button
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>
    ///     Debug button under the role guide button (copy of it): opens the Debug menu. Created once per HUD; shown only
    ///     to the host with the admin key, in game after the intro, with the Debug Mode (<see cref="BorDebugActions.CanUse"/>).
    ///     Called by <see cref="VerticalButtonGroupPatches"/>, which places it.
    /// </summary>
    /// <param name="guide">The role guide button.</param>
    public static void RefreshDebugButton(Transform guide)
    {
        if (!_debugButton)
        {
            _debugButton = CreateDebugButton(guide);
        }

        var visible = BorDebugActions.CanUse && IntroStatePatches.IntroEnded;
        if (_debugButton!.activeSelf != visible)
        {
            _debugButton.SetActive(visible);
        }
    }

    private static GameObject CreateDebugButton(Transform guide)
    {
        // Copied before anything of ours is added to the guide button.
        var copy = UnityEngine.Object.Instantiate(guide.gameObject);
        copy.name = "BorDebugButton";
        for (var i = copy.transform.childCount - 1; i >= 0; i--)
        {
            var child = copy.transform.GetChild(i);
            if (child.name.StartsWith("Bor", StringComparison.Ordinal))
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        // Guide button scripts: AspectPosition would move the copy to the screen edge every frame (MiraAPI removes it
        // from the guide button, but later than the copy).
        UnityEngine.Object.Destroy(copy.GetComponent<AspectPosition>());
        UnityEngine.Object.Destroy(copy.GetComponent<MatchInfoHudButton>());

        SetWrenchIcon(copy.transform);

        var button = copy.GetComponent<PassiveButton>();
        if (button)
        {
            button.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            button.OnClick.AddListener((Action)BorDebugMenu.Open);
        }

        copy.SetActive(false);
        VerticalButtonGroupPatches.Add(copy);
        return copy;
    }

    // Replaces the guide sprites (Inactive and Active children, as on the vanilla top right buttons) with the wrench ones,
    // with the pivot and scale of the guide sprites to stay in place.
    private static void SetWrenchIcon(Transform button)
    {
        foreach (var (name, asset) in new[] { ("Inactive", BorAssets.DebugButton), ("Active", BorAssets.DebugButtonActive) })
        {
            var renderer = button.Find(name)?.GetComponent<SpriteRenderer>();
            if (renderer && renderer!.sprite)
            {
                var template = renderer.sprite;
                var texture = asset.LoadAsset().texture;
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), template.pivot / template.rect.size, template.pixelsPerUnit);
                sprite.name = texture.name;
                renderer.sprite = sprite;
            }
            else
            {
                var children = string.Join(", ", Enumerable.Range(0, button.childCount).Select(i => button.GetChild(i).name));
                Warning($"Debug button: no {name} icon among {children}.");
            }
        }
    }
}
