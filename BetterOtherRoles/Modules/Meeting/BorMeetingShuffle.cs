using BetterOtherRoles.Options;
using HarmonyLib;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.GameOptions;
using Reactor.Networking.Attributes;

namespace BetterOtherRoles.Modules.Meeting;

/// <summary>
///     Players order shuffled in meetings (BetterOtherRoles 1.7.1 MeetingRandomizer,
///     <see cref="GameplayOptions.ShuffleMeetingOrder"/>): the living players swap their places at random, the dead stay
///     after them. The host draws a seed at the start of each round and sends it to everyone, so every player sees the
///     same order, a new one at each meeting.
/// </summary>
[HarmonyPatch(typeof(MeetingHud))]
public static class BorMeetingShuffle
{
    private static int _seed;

    /// <summary>
    ///     Start of the game and end of each meeting: the host draws the seed of the next meeting.
    /// </summary>
    [RegisterEvent]
    public static void RoundStartHandler(RoundStartEvent @event)
    {
        if (!AmongUsClient.Instance.AmHost || !PlayerControl.LocalPlayer) return;

        PlayerControl.LocalPlayer.RpcMeetingSeed(new Random().Next());
    }

    /// <summary>
    ///     Gives everyone the seed of the next meeting, sent by the host.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <param name="seed">The seed.</param>
    [MethodRpc((uint)BorRpc.MeetingSeed)]
    public static void RpcMeetingSeed(this PlayerControl host, int seed)
    {
        if (!host || host.OwnerId != AmongUsClient.Instance.HostId) return;

        _seed = seed;
    }

    /// <summary>
    ///     Once the game has placed the players (living first, by id), the living swap their places.
    /// </summary>
    /// <param name="__instance">The meeting.</param>
    [HarmonyPatch(nameof(MeetingHud.SortButtons))]
    [HarmonyPostfix]
    public static void SortButtonsPostfix(MeetingHud __instance)
    {
        if (!OptionGroupSingleton<GameplayOptions>.Instance.ShuffleMeetingOrder.Value) return;

        // Same list on every client before the draw: sorted by id, as the game places them.
        var alive = __instance.playerStates.Where(x => x && !x.AmDead).OrderBy(x => (byte)x.PlayerId).ToArray();
        var places = alive.Select(x => x.transform.localPosition).ToArray();
        var random = new Random(_seed);
        var shuffled = alive.OrderBy(_ => random.Next()).ToArray();
        for (var i = 0; i < shuffled.Length; i++)
            shuffled[i].transform.localPosition = places[i];
    }
}
