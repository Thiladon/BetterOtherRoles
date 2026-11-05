using System.Collections;
using BetterOtherRoles.Modules.Admin;
using HarmonyLib;
using MiraAPI.Translation;
using BetterOtherRoles.Utilities;
using Reactor.Utilities;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Welcome message for a BetterOtherRoles admin joining a lobby, as a message of the mod (<see cref="BorChat"/>)
///     seen by the admin only, once per lobby: not again when coming back from a game.
/// </summary>
[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
public static class AdminWelcomePatches
{
    // Lobby (game id) where the message was shown.
    private static int _welcomedGameId;

    [HarmonyPostfix]
    public static void StartPostfix()
    {
        if (BorAdmin.IsAdmin)
            Coroutines.Start(CoWelcome());
    }

    // The local player, its name and the chat are not ready yet when the lobby starts.
    private static IEnumerator CoWelcome()
    {
        while (!PlayerControl.LocalPlayer || PlayerControl.LocalPlayer.Data == null || string.IsNullOrEmpty(PlayerControl.LocalPlayer.Data.PlayerName) ||
               !HudManager.InstanceExists || !HudManager.Instance.Chat)
        {
            if (!LobbyBehaviour.Instance) yield break;

            yield return null;
        }

        var gameId = AmongUsClient.Instance.GameId;
        if (_welcomedGameId == gameId) yield break;

        _welcomedGameId = gameId;
        ShowWelcome(PlayerControl.LocalPlayer);
    }

    private static void ShowWelcome(PlayerControl admin)
    {
        var name = admin.Data?.PlayerName ?? string.Empty;
        BorChat.AddSystemMessage(string.Format(BetterOtherRolesPlugin.Culture,
            MiraLocaleManager.Get("BetterOtherRoles.Admin.Chat.Welcome", "Admin ({0}) detected in the lobby, welcome {0}"), name));
    }
}
