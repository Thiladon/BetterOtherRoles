using AmongUs.Data;
using HarmonyLib;
using InnerNet;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Free chat forced (BetterOtherRoles 1.7.1): the game switches a player to quick chat only when joining a quick chat
///     lobby by code (and keeps it in the settings), so it is put back to free or quick chat when the chat is created,
///     when a game is hosted and when a game is joined. Never for an account where free chat is not allowed (parental
///     control: EOSManager.IsFreechatAllowed). Only the mod settings are changed (settings.bor.amogus, SaveFilePatches).
/// </summary>
[HarmonyPatch]
public static class FreeChatPatches
{
    [HarmonyPatch(typeof(ChatController), nameof(ChatController.Awake))]
    [HarmonyPrefix]
    public static void ChatAwakePrefix() => ForceFreeChat();

    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.HostGame))]
    [HarmonyPrefix]
    public static void HostGamePrefix() => ForceFreeChat();

    // After the game set the lobby's chat mode (CoFindGameInfoFromCodeAndJoin), before joining.
    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.JoinGame))]
    [HarmonyPrefix]
    public static void JoinGamePrefix() => ForceFreeChat();

    private static void ForceFreeChat()
    {
        if (!EOSManager.InstanceExists || !EOSManager.Instance.IsFreechatAllowed()) return;

        var multiplayer = DataManager.Settings.Multiplayer;
        if (multiplayer.ChatMode != QuickChatModes.FreeChatOrQuickChat)
            multiplayer.ChatMode = QuickChatModes.FreeChatOrQuickChat;
    }
}
