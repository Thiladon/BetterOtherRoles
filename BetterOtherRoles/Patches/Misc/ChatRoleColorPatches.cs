using BetterOtherRoles.LocalSettings;
using BetterOtherRoles.Patches.Roles;
using HarmonyLib;
using MiraAPI.LocalSettings;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Names of the chat bubbles in the color of the role when the local player knows it (TOU-Mira 1.7.9
///     ShowRolesOnChatBubbles, color only), the same as the names above the players (<see cref="NameColorPatches"/>):
///     its own role, the Sheriff and the Deputy who know each other, the other impostors for an impostor. A dead player
///     is seen with its role while alive. Local setting <see cref="BorLocalSettingsTab.ChatRoleColors"/>; in game only.
/// </summary>
[HarmonyPatch]
public static class ChatRoleColorPatches
{
    // Runs before the finalizer of the impostor chat: the bubble is still in the list it was added to.
    [HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
    [HarmonyPostfix]
    public static void AddChatPostfix(ChatController __instance, PlayerControl sourcePlayer)
    {
        if (!sourcePlayer || !PlayerControl.LocalPlayer || !AmongUsClient.Instance || !AmongUsClient.Instance.IsGameStarted) return;
        if (!LocalSettingsTabSingleton<BorLocalSettingsTab>.Instance.ChatRoleColors.Value) return;

        var bubbles = __instance.chatBubblePool.activeChildren;
        var bubble = bubbles.Count > 0 ? bubbles[bubbles.Count - 1].TryCast<ChatBubble>() : null;

        // The game adds no bubble for some messages (a dead player seen by a living one).
        if (!bubble || bubble!.playerInfo != sourcePlayer.Data) return;

        var localRole = AliveRolePatches.GetRole(PlayerControl.LocalPlayer);
        var role = AliveRolePatches.GetRole(sourcePlayer);
        if (!localRole || !role) return;

        var color = NameColorPatches.GetKnownColor(localRole!, sourcePlayer, role!);
        if (color != null)
            bubble.NameText.color = color.Value;
    }
}
