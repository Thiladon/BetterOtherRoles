using BetterOtherRoles.LocalSettings;
using HarmonyLib;
using MiraAPI.LocalSettings;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Notification of a new chat message in game (TOU-Mira 1.7.9 ShowChatNotifsInGame): the game shows it in the lobby
///     only. With <see cref="BorLocalSettingsTab.ChatNotificationsInGame"/>, it is shown in game too, the same way.
///     The game shows it only for a message received while the chat is closed.
/// </summary>
[HarmonyPatch]
public static class ChatNotificationPatches
{
    // Time on screen, as in the game.
    private const float TimeOnScreen = 5f;

    [HarmonyPatch(typeof(ChatNotification), nameof(ChatNotification.SetUp))]
    [HarmonyPrefix]
    public static bool SetUpPrefix(ChatNotification __instance, PlayerControl sender, string text)
    {
        // Lobby: the game shows it; the customization menu hides it.
        if (!ShipStatus.Instance || PlayerCustomizationMenu.Instance) return true;
        if (!LocalSettingsTabSingleton<BorLocalSettingsTab>.Instance.ChatNotificationsInGame.Value || !sender || !sender.Data) return true;

        __instance.timeOnScreen = TimeOnScreen;
        __instance.gameObject.SetActive(true);
        __instance.SetCosmetics(sender.Data);
        var colorId = __instance.player.ColorId;
        var textColor = ColorUtility.ToHtmlStringRGB(Palette.TextColors[colorId]);
        __instance.playerColorText.text = __instance.player.ColorBlindName;
        __instance.playerNameText.text = $"<color=#{textColor}>{(string.IsNullOrEmpty(sender.Data.PlayerName) ? "..." : sender.Data.PlayerName)}";
        __instance.playerNameText.outlineColor = Palette.TextOutlineColors[colorId];
        __instance.chatText.text = text;
        return false;
    }
}
