using System.Collections;
using BetterOtherRoles.LocalSettings;
using BetterOtherRoles.Modules.Summary;
using BetterOtherRoles.Utilities;
using HarmonyLib;
using MiraAPI.LocalSettings;
using MiraAPI.Translation;
using Reactor.Utilities;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Messages of the mod in the lobby chat (TOU-Mira 1.7.9 PlayerJoinPatch), seen by the local player only:
///     the summary of the game when coming back from it (<see cref="BorSummaryText.GetChatSummary"/>, in the format of the
///     local settings), and the welcome message in the first lobby after launching the game. Each one can be turned off
///     in the local settings.
/// </summary>
[HarmonyPatch]
public static class LobbyChatPatches
{
    // Text under the title, black as the players' messages (light chat bubbles).
    private const string TextColor = "#000000";

    // The welcome message was shown, or its lobby passed with the message turned off: once per launch of the game.
    private static bool _welcomeDone;

    // Lobby (game id) of the game whose summary is still to be shown, or none.
    private static int? _summaryGameId;

    private static BorLocalSettingsTab Settings => LocalSettingsTabSingleton<BorLocalSettingsTab>.Instance;

    // End of a game: its summary shown when coming back to the same lobby (not in another one, nor after freeplay).
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
    [HarmonyPostfix]
    public static void OnGameEndPostfix()
    {
        if (TutorialManager.InstanceExists || BorGameHistory.Players.Count == 0) return;

        _summaryGameId = AmongUsClient.Instance.GameId;
    }

    [HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
    [HarmonyPostfix]
    public static void StartPostfix()
    {
        Coroutines.Start(CoShowMessages());
    }

    // The local player and the chat are not ready yet when the lobby starts.
    private static IEnumerator CoShowMessages()
    {
        while (!PlayerControl.LocalPlayer || PlayerControl.LocalPlayer.Data == null || !HudManager.InstanceExists || !HudManager.Instance.Chat)
        {
            if (!LobbyBehaviour.Instance) yield break;

            yield return null;
        }

        var gameId = AmongUsClient.Instance.GameId;
        if (_summaryGameId != null)
        {
            if (_summaryGameId == gameId && Settings.ShowLobbySummary.Value)
                ShowSummary();
            _summaryGameId = null;
        }

        if (_welcomeDone) yield break;

        _welcomeDone = true;
        if (Settings.ShowWelcomeMessage.Value)
            ShowWelcome();
    }

    private static void ShowSummary()
    {
        var summary = BorSummaryText.GetChatSummary(Settings.LobbySummaryFormat.Value);
        BorChat.AddSystemMessage($"{BorSummaryText.Get("ChatTitle", "Summary of the last game:")}\n<color={TextColor}><size=62%>{summary}</size></color>");
    }

    private static void ShowWelcome()
    {
        var title = string.Format(BetterOtherRolesPlugin.Culture, Get("WelcomeTitle", "Welcome to Better Other Roles v{0}!"), BetterOtherRolesPlugin.Version);
        var help = Get("WelcomeHelp", "Use the role guide to learn more about the roles and modifiers, and /help in the chat for the list of commands.");
        var turnOff = Get("WelcomeTurnOff", "You can turn this message off in your settings (Better Other Roles tab, Chat).");
        BorChat.AddSystemMessage($"{title}\n<color={TextColor}><size=75%>{help}\n{turnOff}</size></color>");
    }

    private static string Get(string key, string fallback) => MiraLocaleManager.Get("BetterOtherRoles.LobbyChat." + key, fallback);
}
