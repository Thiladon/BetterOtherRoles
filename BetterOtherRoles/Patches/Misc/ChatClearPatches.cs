using HarmonyLib;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Clears the chat when a game starts: the lobby messages (admin welcome included) are not kept in the game.
/// </summary>
[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin))]
public static class ChatClearPatches
{
    [HarmonyPrefix]
    public static void CoBeginPrefix()
    {
        if (!HudManager.InstanceExists || !HudManager.Instance.Chat) return;

        var chat = HudManager.Instance.Chat;
        chat.chatBubblePool.ReclaimAll();
        chat.AlignAllBubbles();

        // The unread dot on the chat button, and the popup of the last message.
        chat.chatNotifyDot.enabled = false;
        if (chat.chatNotification.gameObject.activeSelf)
            chat.chatNotification.Close();
    }
}
