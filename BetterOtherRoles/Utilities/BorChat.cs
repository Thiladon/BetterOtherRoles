namespace BetterOtherRoles.Utilities;

/// <summary>
///     Messages of the mod in the chat: game messages (no player as sender), "[BOR] : " first, in the BOR blue.
/// </summary>
public static class BorChat
{
    private const string Color = "#025AE8";

    /// <summary>
    ///     Adds a message of the mod to the local chat, with the chat bubble of a player when the chat is closed.
    /// </summary>
    /// <param name="message">Message, already translated.</param>
    /// <param name="bubblePlayer">Player shown in the chat bubble, or none.</param>
    public static void AddSystemMessage(string message, PlayerControl? bubblePlayer = null)
    {
        var chat = HudManager.InstanceExists ? HudManager.Instance.Chat : null;
        if (!chat) return;

        var text = $"<color={Color}>[BOR] : {message}</color>";
        chat!.AddChatWarning(text);
        if (bubblePlayer && !chat.IsOpenOrOpening && chat.chatNotification)
            chat.chatNotification.SetUp(bubblePlayer, text);
    }
}
