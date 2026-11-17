using BetterOtherRoles.Options;
using MiraAPI.Events;
using MiraAPI.GameOptions;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Utilities;
using MiraAPI.Voting;

namespace BetterOtherRoles.Modules.Meeting;

/// <summary>
///     A player killed during a meeting (TOU-Mira 1.7.9 HandleMeetingMurder; MiraAPI only handles a player who leaves):
///     their line shows them dead and their own vote goes away (the votes of the dead are not counted), the votes cast
///     against them are given back to their voters, as MiraAPI does when a player leaves. The victim closes their
///     minigame and can no longer vote (the game shows the meeting of the dead). Time is added to the meeting
///     (<see cref="GameplayOptions.AddedMeetingDeathTime"/>).
/// </summary>
public static class BorMeetingDeaths
{
    [RegisterEvent]
    public static void AfterMurderHandler(AfterMurderEvent @event)
    {
        var meeting = MeetingHud.Instance;
        var target = @event.Target;
        if (!meeting || !target || meeting.playerStates == null) return;

        var area = meeting.playerStates.FirstOrDefault(x => x && (byte)x.PlayerId == target.PlayerId);
        if (!area) return;

        if (area!.DidVote)
            area.UnsetVote();
        area.SetDead(true);

        // Time added on every client (each one counts the time of the meeting): the meeting timer goes back.
        var added = OptionGroupSingleton<GameplayOptions>.Instance.AddedMeetingDeathTime.Value;
        if (added > 0f && meeting.state is > MeetingHud.MeetingStates.Animating and < MeetingHud.MeetingStates.Results)
            meeting.discussionTimer -= added;

        if (target.AmOwner)
        {
            foreach (var state in meeting.playerStates)
            {
                if (state)
                    state.ClearButtons();
            }

            if (Minigame.Instance)
                Minigame.Instance.Close();
        }

        if (!AmongUsClient.Instance.AmHost) return;

        // Votes given back as MiraAPI does for a player who leaves (MeetingHudPatches.HandleDisconnect).
        foreach (var voter in Helpers.GetAlivePlayers())
        {
            var data = voter.GetVoteData();
            if (!data.VotedFor(target.PlayerId)) continue;

            var count = data.Votes.RemoveAll(x => x.Suspect == target.PlayerId);
            data.IncreaseRemainingVotes(count);
            VotingUtils.RpcRemoveVote(PlayerControl.LocalPlayer, voter.PlayerId, target.PlayerId);
        }

        meeting.SetDirtyBit(1U);
        meeting.CheckForEndVoting();
    }
}
