using BetterOtherRoles.Utilities;
using HarmonyLib;

namespace BetterOtherRoles.Modules.Chat;

/// <summary>
///     Role information messages (results of an investigation...) received by the local player, kept round by round to
///     be replayed with /info (TOU-Mira 1.7.9 FakeChatHistory). A round ends with the vote of a meeting; everything is
///     cleared when a game starts (<see cref="Patches.Misc.ChatClearPatches"/>).
///     A role sends its information with <see cref="Send"/>, never with <see cref="BorChat"/> directly.
/// </summary>
[HarmonyPatch]
public static class BorInfoHistory
{
    private static readonly Dictionary<int, List<string>> Rounds = [];

    /// <summary>
    ///     Gets the current round: 1 at the start of the game, one more after each vote.
    /// </summary>
    public static int CurrentRound { get; private set; } = 1;

    /// <summary>
    ///     Gets the last round with information, or null.
    /// </summary>
    public static int? LastRound => Rounds.Count == 0 ? null : Rounds.Keys.Max();

    /// <summary>
    ///     Shows a role information message to the local player and keeps it for /info.
    /// </summary>
    /// <param name="message">The message, already translated.</param>
    /// <param name="bubblePlayer">Player shown in the chat bubble when the chat is closed, or none.</param>
    public static void Send(string message, PlayerControl? bubblePlayer = null)
    {
        if (!Rounds.TryGetValue(CurrentRound, out var messages))
        {
            messages = [];
            Rounds[CurrentRound] = messages;
        }

        messages.Add(message);
        BorChat.AddSystemMessage(message, bubblePlayer);
    }

    /// <summary>
    ///     Gets the messages of a round (empty if none).
    /// </summary>
    /// <param name="round">The round.</param>
    /// <returns>The messages, in the order received.</returns>
    public static IReadOnlyList<string> Get(int round)
    {
        return Rounds.TryGetValue(round, out var messages) ? messages : [];
    }

    /// <summary>
    ///     Forgets everything (start of a game).
    /// </summary>
    public static void Clear()
    {
        Rounds.Clear();
        CurrentRound = 1;
    }

    // At the vote and not at the start of the meeting: a role may send its information when the meeting starts.
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.VotingComplete))]
    [HarmonyPrefix]
    public static void VotingCompletePrefix() => CurrentRound++;
}
