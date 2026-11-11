using BetterOtherRoles.Modules.Chat;
using HarmonyLib;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Clickable terms (<see cref="BorChatLinks"/>) in the chat bubbles and the new message notifications
///     (TOU-Mira 1.7.9 WikiHyperLinkPatches).
/// </summary>
[HarmonyPatch]
public static class ChatLinkPatches
{
    [HarmonyPatch(typeof(ChatBubble), nameof(ChatBubble.SetText))]
    [HarmonyPrefix]
    public static void BubbleSetTextPrefix(ChatBubble __instance, ref string chatText)
    {
        chatText = BorChatLinks.Apply(chatText);
        BorChatLinkBehaviour.AddTo(__instance.TextArea);
    }

    // Before the in-game notification (ChatNotificationPatches), which writes the text itself.
    [HarmonyPatch(typeof(ChatNotification), nameof(ChatNotification.SetUp))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.High)]
    public static void NotificationSetUpPrefix(ChatNotification __instance, ref string text)
    {
        text = BorChatLinks.Apply(text);
        BorChatLinkBehaviour.AddTo(__instance.chatText);
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
    [HarmonyPostfix]
    public static void OnGameJoinedPostfix() => BorChatLinks.Clear();
}
