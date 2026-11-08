using BetterOtherRoles.Modules.Roles;
using HarmonyLib;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     With anonymous votes, a dead player who sees the votes (<see cref="BorGhostInfo.SeesVotes"/>) gets the vote icons
///     in the color of the voters, as BetterOtherRoles 1.7.1 (MeetingHudBloopAVoteIconPatch).
/// </summary>
[HarmonyPatch]
public static class GhostVotePatches
{
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.BloopAVoteIcon))]
    [HarmonyPostfix]
    public static void BloopAVoteIconPostfix(NetworkedPlayerInfo voterPlayer, Transform parent)
    {
        if (!GameManager.Instance.LogicOptions.GetAnonymousVotes() || !BorGhostInfo.SeesVotes || !voterPlayer || parent.childCount == 0) return;

        // The icon the game just made, last child of the area.
        var icon = parent.GetChild(parent.childCount - 1).GetComponent<SpriteRenderer>();
        if (icon)
            PlayerMaterial.SetColors(voterPlayer.DefaultOutfit.ColorId, icon);
    }
}
