using System.Collections;
using BetterOtherRoles.LocalSettings;
using BetterOtherRoles.Utilities;
using HarmonyLib;
using InnerNet;
using MiraAPI.LocalSettings;
using MiraAPI.Translation;
using Reactor.Utilities;
using UnityEngine;

namespace BetterOtherRoles.Modules.Lobby;

/// <summary>
///     Lobby code copied to the clipboard by itself (BetterOtherRoles 1.7.1, GameStartManagerPatch), for the host of an
///     online game, once per lobby: not again when coming back from a game, so that something copied in between stays.
///     A message of the mod tells it was copied (the vanilla "code copied" text belongs to the lobby info pane, usually
///     closed). Local setting <see cref="BorLocalSettingsTab.CopyLobbyCode"/>, on by default.
///     The game also copies it with Ctrl+C in the lobby.
/// </summary>
[HarmonyPatch]
public static class BorLobbyCode
{
    // Lobby (game id) whose code was copied.
    private static int _copiedGameId;

    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Start))]
    [HarmonyPostfix]
    public static void StartPostfix()
    {
        var client = AmongUsClient.Instance;
        if (!client || !client.AmHost || client.NetworkMode != NetworkModes.OnlineGame || client.GameId == _copiedGameId ||
            !LocalSettingsTabSingleton<BorLocalSettingsTab>.Instance.CopyLobbyCode.Value)
            return;

        _copiedGameId = client.GameId;
        var code = GameCode.IntToGameName(client.GameId);
        GUIUtility.systemCopyBuffer = code;
        Coroutines.Start(CoTellCopied(code));
    }

    // The local player and the chat are not ready yet when the lobby starts.
    private static IEnumerator CoTellCopied(string code)
    {
        while (!PlayerControl.LocalPlayer || !HudManager.InstanceExists || !HudManager.Instance.Chat)
        {
            if (!LobbyBehaviour.Instance) yield break;

            yield return null;
        }

        BorChat.AddSystemMessage(MiraLocaleManager.Get("BetterOtherRoles.Lobby.CodeCopied", "Lobby code copied: {0}").Replace("{0}", code),
            PlayerControl.LocalPlayer);
    }
}
