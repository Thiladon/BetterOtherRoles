using AmongUs.GameOptions;
using BetterOtherRoles.Patches.Misc;
using BetterOtherRoles.Patches.Roles;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Events.Vanilla.Meeting;
using MiraAPI.Events.Vanilla.Player;
using MiraAPI.Modifiers;
using MiraAPI.Roles;
using MiraAPI.Translation;
using UnityEngine;

namespace BetterOtherRoles.Modules.Summary;

/// <summary>
///     History of the game, kept on every client for the end game summary (TOU-Mira 1.7.9 GameHistory): roles held by
///     each player, the cause, the killer and the round of a death, the victims of each killer with their round.
///     Names, roles and colors are kept as text: the players and the role manager are gone on the end game screen.
///     Cleared when a game starts; kept until the next one (summary in the lobby chat, /summary).
/// </summary>
[HarmonyPatch]
public static class BorGameHistory
{
    private static readonly Dictionary<byte, PlayerRecord> Records = [];

    private static readonly List<MeetingRecord> MeetingRecords = [];

    // PlayerVoteArea.SkippedVote and MissedVote (not constants in the interop assembly): a skip, or no vote at the end
    // of the time, both counted as skips.
    private const byte SkippedVote = 253;
    private const byte MissedVote = 254;

    /// <summary>
    ///     Gets the current round: 1 at the start of the game, one more at the start of each round after a meeting
    ///     (TOU-Mira 1.7.9 CurrentRound). An ejection belongs to the round of its meeting.
    /// </summary>
    public static int CurrentRound { get; private set; } = 1;

    /// <summary>
    ///     Gets the players of the last game, by player id.
    /// </summary>
    public static IReadOnlyCollection<PlayerRecord> Players => Records.Values;

    /// <summary>
    ///     Gets the meetings of the last game, in their order.
    /// </summary>
    public static IReadOnlyList<MeetingRecord> Meetings => MeetingRecords;

    // Start of a game, on every client, before the roles are given.
    [HarmonyPatch(typeof(AmongUsClient), "CoStartGame")]
    [HarmonyPrefix]
    public static void CoStartGamePrefix()
    {
        Records.Clear();
        MeetingRecords.Clear();
        CurrentRound = 1;
    }

    // Tasks and modifiers before the game takes them back (shield of the first kill).
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
    [HarmonyPrefix]
    public static void OnGameEndPrefix()
    {
        foreach (var record in Records.Values)
        {
            // A player who left was kept when leaving (HandleDisconnectPrefix).
            if (record.Disconnected) continue;

            var info = GameData.Instance ? GameData.Instance.GetPlayerById(record.PlayerId) : null;
            if (info)
                Keep(record, info!);
        }
    }

    // A player who leaves during the game (TOU-Mira 1.7.9 SummaryDisconnectFixPatch), before the game drops it: its
    // modifiers and tasks kept now, "Disconnected" as cause when alive. Not in the lobby nor on the end game screen,
    // which would change the last game.
    [HarmonyPatch(typeof(GameData), nameof(GameData.HandleDisconnect), typeof(PlayerControl), typeof(DisconnectReasons))]
    [HarmonyPrefix]
    public static void HandleDisconnectPrefix(PlayerControl player)
    {
        if (!AmongUsClient.Instance || AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started) return;
        if (!player || !Records.TryGetValue(player.PlayerId, out var record) || record.Disconnected) return;

        if (player.Data)
            Keep(record, player.Data);
        record.Disconnected = true;
        record.IsDead = true;
        if (record.Death != DeathCause.Alive) return;

        record.Death = DeathCause.Disconnected;
        record.DeathRound = CurrentRound;
    }

    // State, modifiers and tasks of a player, before the game takes them back (end of the game, or the player leaves).
    private static void Keep(PlayerRecord record, NetworkedPlayerInfo info)
    {
        record.IsDead = info.IsDead || info.Disconnected;
        if (info.Object)
        {
            var modifiers = info.Object.GetModifiers<BaseModifier>().ToList();
            record.Modifiers = [.. modifiers.Select(x => x.ModifierName)];
            record.EnglishModifiers = [.. modifiers.Select(EnglishModifierName)];
        }

        if (record.Team != RoleTeamTypes.Crewmate || info.Tasks == null || info.Tasks.Count == 0) return;

        record.TasksTotal = info.Tasks.Count;
        record.TasksDone = 0;
        foreach (var task in info.Tasks)
        {
            if (task.Complete)
                record.TasksDone++;
        }
    }

    // Roles given by MiraAPI (start of the game); a change during the game goes through RecordRole.
    [RegisterEvent]
    public static void SetRoleHandler(SetRoleEvent @event)
    {
        if (!@event.Player || !RoleManager.InstanceExists) return;

        RecordRole(@event.Player, RoleManager.Instance.GetRole(@event.Role));
    }

    /// <summary>
    ///     Adds a role to the roles held by a player (<see cref="Utilities.BorPlayerExtensions.ChangeRole"/>, which does
    ///     not go through the SetRoleEvent of MiraAPI). Ghost roles and the same role again are not added.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="role">The new role.</param>
    public static void RecordRole(PlayerControl player, RoleBehaviour? role)
    {
        if (!player || !player.Data || !role || role!.IsDead) return;

        var record = GetOrAdd(player);
        if (record.Roles.Count > 0 && record.Roles[^1].Type == role.Role) return;

        var custom = role as ICustomRole;
        var name = custom != null ? custom.RoleName : role.NiceName;
        var english = custom != null ? MiraLocaleManager.Get(MiraLanguage.English, custom.RoleNameLocale, custom.RoleName) : VanillaEnglishName(role.Role);
        record.Roles.Add(new RoleRecord(role.Role, name, english, NameColorPatches.GetRoleColor(role)));
        record.Team = role.TeamType;
        record.IsImpostor = role.IsImpostor;
    }

    // Roles of the game in English, whatever the language (export of the game, BorAmongStatExport).
    private static string VanillaEnglishName(RoleTypes role) => role switch
    {
        RoleTypes.GuardianAngel => "Guardian Angel",
        _ => role.ToString(),
    };

    // A meeting starts, on every client: the reporter, and the body (none with the emergency button).
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.StartMeeting))]
    [HarmonyPrefix]
    public static void StartMeetingPrefix(PlayerControl __instance, NetworkedPlayerInfo? target)
    {
        if (!__instance || !__instance.Data) return;

        MeetingRecords.Add(new MeetingRecord(MeetingRecords.Count + 1, __instance.Data.PlayerName, target != null));
    }

    // End of the vote, on every client, with every vote even when they are anonymous.
    [HarmonyPatch(typeof(MeetingHud), "VotingComplete")]
    [HarmonyPrefix]
    public static void VotingCompletePrefix(Il2CppStructArray<MeetingHud.VoterState> states, NetworkedPlayerInfo? exiled, bool tie)
    {
        if (MeetingRecords.Count == 0) return;

        var meeting = MeetingRecords[^1];
        meeting.Tie = tie;
        meeting.EjectedName = exiled ? exiled!.PlayerName : null;
        foreach (var state in states)
        {
            if (state.VotedForId is SkippedVote or MissedVote)
            {
                meeting.SkipCount++;
                continue;
            }

            var suspect = GameData.Instance ? GameData.Instance.GetPlayerById(state.VotedForId) : null;
            if (!suspect) continue;

            var name = suspect!.PlayerName;
            meeting.Votes[name] = meeting.Votes.GetValueOrDefault(name) + 1;
        }
    }

    [RegisterEvent]
    public static void RoundStartHandler(RoundStartEvent @event)
    {
        if (!@event.TriggeredByIntro)
            CurrentRound++;
    }

    // Before the murder event: a death by the game (other reasons) still gets a cause.
    [RegisterEvent]
    public static void PlayerDeathHandler(PlayerDeathEvent @event)
    {
        if (!@event.Player) return;

        var record = GetOrAdd(@event.Player);
        if (record.Death != DeathCause.Alive) return;

        record.Death = @event.DeathReason == DeathReason.Exile ? DeathCause.Ejected : DeathCause.Killed;
        record.DeathRound = CurrentRound;
    }

    [RegisterEvent]
    public static void AfterMurderHandler(AfterMurderEvent @event)
    {
        var source = @event.Source;
        var target = @event.Target;
        if (!source || !target) return;

        var record = GetOrAdd(target);
        record.DeathRound = CurrentRound;
        record.KillerRolePart = GetIdPart(source);

        if (source == target)
        {
            record.Death = DeathCause.Suicide;
            return;
        }

        record.Death = DeathCause.Killed;
        record.KillerName = source.Data.PlayerName;
        GetOrAdd(source).Victims.Add(new VictimRecord(target.Data.PlayerName, CurrentRound));
    }

    [RegisterEvent]
    public static void EjectionHandler(EjectionEvent @event)
    {
        var exiled = @event.ExileController?.initData?.networkedPlayer?.Object;
        if (!exiled) return;

        var record = GetOrAdd(exiled!);
        record.Death = DeathCause.Ejected;
        record.DeathRound = CurrentRound;
        record.EjectedInMeeting = MeetingRecords.Count;
    }

    // A modifier in English: key BetterOtherRoles.Modifier.{class name without "Modifier"} (FirstKillShieldModifier:
    // BetterOtherRoles.Modifier.FirstKillShield), or its name when there is none.
    private static string EnglishModifierName(BaseModifier modifier)
    {
        var key = "BetterOtherRoles.Modifier." + modifier.GetType().Name.Replace("Modifier", string.Empty);
        return MiraLocaleManager.Get(MiraLanguage.English, key, modifier.ModifierName);
    }

    private static PlayerRecord GetOrAdd(PlayerControl player)
    {
        if (Records.TryGetValue(player.PlayerId, out var record)) return record;

        record = new PlayerRecord(player.PlayerId, player.Data.PlayerName);
        Records[player.PlayerId] = record;
        return record;
    }

    // The role of the killer while alive, for a cause of death named after it (Sheriff: "Justice", "Misfire").
    private static string? GetIdPart(PlayerControl player)
    {
        var role = AliveRolePatches.GetRole(player);
        return role is ICustomRole custom ? custom.IdPart : null;
    }
}

/// <summary>
///     How a player died.
/// </summary>
public enum DeathCause
{
    Alive,
    Killed,
    Suicide,
    Ejected,
    Disconnected,
}

/// <summary>
///     A role held by a player, as shown at that time.
/// </summary>
/// <param name="Type">The role.</param>
/// <param name="Name">Its translated name.</param>
/// <param name="EnglishName">Its name in English (export of the game).</param>
/// <param name="Color">Its color.</param>
public sealed record RoleRecord(RoleTypes Type, string Name, string EnglishName, Color Color);

/// <summary>
///     A meeting: who called it, the votes against each player, the skips and the ejected player.
/// </summary>
/// <param name="number">Its number in the game, from 1.</param>
/// <param name="triggeredBy">The name of the player who called it.</param>
/// <param name="bodyReport">Called by reporting a body (else the emergency button).</param>
public sealed class MeetingRecord(int number, string triggeredBy, bool bodyReport)
{
    public int Number { get; } = number;

    public string TriggeredBy { get; } = triggeredBy;

    public bool BodyReport { get; } = bodyReport;

    public string? EjectedName { get; set; }

    public int SkipCount { get; set; }

    public bool Tie { get; set; }

    /// <summary>
    ///     Gets the votes against each player, by name, in the order of the first vote.
    /// </summary>
    public Dictionary<string, int> Votes { get; } = [];
}

/// <summary>
///     A player killed by another one.
/// </summary>
/// <param name="Name">The victim's name.</param>
/// <param name="Round">The round of the kill.</param>
public sealed record VictimRecord(string Name, int Round);

/// <summary>
///     What the summary knows about a player.
/// </summary>
/// <param name="playerId">The player id.</param>
/// <param name="playerName">The name at the start of the game.</param>
public sealed class PlayerRecord(byte playerId, string playerName)
{
    public byte PlayerId { get; } = playerId;

    public string PlayerName { get; } = playerName;

    public List<RoleRecord> Roles { get; } = [];

    public RoleTeamTypes Team { get; set; } = RoleTeamTypes.Crewmate;

    public bool IsImpostor { get; set; }

    public List<string> Modifiers { get; set; } = [];

    public List<string> EnglishModifiers { get; set; } = [];

    public int TasksDone { get; set; }

    public int TasksTotal { get; set; }

    public bool IsDead { get; set; }

    public bool Disconnected { get; set; }

    public DeathCause Death { get; set; } = DeathCause.Alive;

    public int DeathRound { get; set; }

    /// <summary>
    ///     Gets or sets the number of the meeting where the player was ejected, or null.
    /// </summary>
    public int? EjectedInMeeting { get; set; }

    public string? KillerName { get; set; }

    public string? KillerRolePart { get; set; }

    public List<VictimRecord> Victims { get; } = [];
}
