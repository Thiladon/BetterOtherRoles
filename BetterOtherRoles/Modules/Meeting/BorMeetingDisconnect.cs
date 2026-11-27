using HarmonyLib;
using MiraAPI.Utilities;
using MiraAPI.Voting;

namespace BetterOtherRoles.Modules.Meeting;

/// <summary>
///     A player who leaves during a meeting, around two faults of MiraAPI 0.6.0 (MeetingHudPatches.HandleDisconnect and
///     VotingUtils.RpcRemoveVote):
///     - its line shows it dead on every client, as the game does (HandleDisconnectedPlayerVoteArea): MiraAPI replaces
///       MeetingHud.HandleDisconnect and does nothing on the clients, so they kept it alive;
///     - a client whose vote against it is given back can vote again: MiraAPI takes one remaining vote away from it
///       instead of giving one back (it stays at 0, so no vote). Also for a player killed during a meeting
///       (<see cref="BorMeetingDeaths"/>), which gives the votes back the same way.
/// </summary>
[HarmonyPatch]
public static class BorMeetingDisconnect
{
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.HandleDisconnect), typeof(PlayerControl), typeof(DisconnectReasons))]
    [HarmonyPostfix]
    public static void HandleDisconnectPostfix(MeetingHud __instance, PlayerControl pc)
    {
        if (!pc || __instance.playerStates == null) return;

        var area = __instance.playerStates.FirstOrDefault(x => x && (byte)x.PlayerId == pc.PlayerId);
        if (!area) return;

        area!.ClearButtons();
        area.SetDead(true);
    }

    // The remaining votes of the voter before MiraAPI changes them, on the client whose vote is given back.
    [HarmonyPatch(typeof(VotingUtils), nameof(VotingUtils.RpcRemoveVote))]
    [HarmonyPrefix]
    public static void RemoveVotePrefix(byte voterId, out int __state)
    {
        __state = -1;
        if (AmongUsClient.Instance.AmHost || !PlayerControl.LocalPlayer || PlayerControl.LocalPlayer.PlayerId != voterId) return;

        __state = PlayerControl.LocalPlayer.GetVoteData().VotesRemaining;
    }

    // One vote more than before, as on the host.
    [HarmonyPatch(typeof(VotingUtils), nameof(VotingUtils.RpcRemoveVote))]
    [HarmonyPostfix]
    public static void RemoveVotePostfix(int __state)
    {
        if (__state < 0) return;

        PlayerControl.LocalPlayer.GetVoteData().SetRemainingVotes(__state + 1);
    }
}
