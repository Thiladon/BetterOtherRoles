using System.Collections;
using BetterOtherRoles.Modules.Admin;
using HarmonyLib;
using MiraAPI.Translation;
using Reactor.Utilities;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Welcome message for a BetterOtherRoles admin joining a lobby, in its own chat only (AddChat is local),
///     once per lobby: not again when coming back from a game.
/// </summary>
[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
public static class AdminWelcomePatches
{
    private const string WelcomeColor = "#3C9BFF";

    // Lobby (game id) where the message was shown.
    private static int _welcomedGameId;

    [HarmonyPostfix]
    public static void StartPostfix()
    {
        if (BorAdmin.IsAdmin)
        {
            Coroutines.Start(CoWelcome());
        }
    }

    // The local player, its name and the chat are not ready yet when the lobby starts.
    private static IEnumerator CoWelcome()
    {
        while (!PlayerControl.LocalPlayer || PlayerControl.LocalPlayer.Data == null || string.IsNullOrEmpty(PlayerControl.LocalPlayer.Data.PlayerName) ||
               !HudManager.InstanceExists || !HudManager.Instance.Chat)
        {
            if (!LobbyBehaviour.Instance)
            {
                yield break;
            }

            yield return null;
        }

        var gameId = AmongUsClient.Instance.GameId;
        if (_welcomedGameId == gameId)
        {
            yield break;
        }

        _welcomedGameId = gameId;
        var name = PlayerControl.LocalPlayer.Data.PlayerName;
        var message = string.Format(BetterOtherRolesPlugin.Culture,
            MiraLocaleManager.Get("BetterOtherRoles.Admin.Chat.Welcome", "Admin ({0}) detected in the lobby, welcome {0}"), name);
        HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, $"<color={WelcomeColor}>{message}</color>");
    }
}
