using BetterOtherRoles.Patches.Misc;
using BetterOtherRoles.Patches.Roles;
using HarmonyLib;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Events.Vanilla.Meeting;
using MiraAPI.Events.Vanilla.Player;
using MiraAPI.Modifiers;
using MiraAPI.Roles;
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

    /// <summary>
    ///     Gets the current round: 1 at the start of the game, one more at the start of each round after a meeting
    ///     (TOU-Mira 1.7.9 CurrentRound). An ejection belongs to the round of its meeting.
    /// </summary>
    public static int CurrentRound { get; private set; } = 1;

    /// <summary>
    ///     Gets the players of the last game, by player id.
    /// </summary>
    public static IReadOnlyCollection<PlayerRecord> Players => Records.Values;

    // Start of a game, on every client, before the roles are given.
    [HarmonyPatch(typeof(AmongUsClient), "CoStartGame")]
    [HarmonyPrefix]
    public static void CoStartGamePrefix()
    {
        Records.Clear();
        CurrentRound = 1;
    }

    // Tasks and modifiers before the game takes them back (shield of the first kill).
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
    [HarmonyPrefix]
    public static void OnGameEndPrefix()
    {
        foreach (var record in Records.Values)
        {
            var info = GameData.Instance ? GameData.Instance.GetPlayerById(record.PlayerId) : null;
            if (!info) continue;

            record.IsDead = info!.IsDead || info.Disconnected;
            if (info.Object && !info.Disconnected)
                record.Modifiers = [.. info.Object.GetModifiers<BaseModifier>().Select(x => x.ModifierName)];

            if (record.Team != RoleTeamTypes.Crewmate || info.Tasks == null || info.Tasks.Count == 0) continue;

            record.TasksTotal = info.Tasks.Count;
            record.TasksDone = 0;
            foreach (var task in info.Tasks)
            {
                if (task.Complete)
                    record.TasksDone++;
            }
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
        var name = role is ICustomRole custom ? custom.RoleName : role.NiceName;
        if (record.Roles.Count > 0 && record.Roles[^1].Type == role.Role) return;

        record.Roles.Add(new RoleRecord(role.Role, name, NameColorPatches.GetRoleColor(role)));        record.Team = role.TeamType;
        record.IsImpostor = role.IsImpostor;
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
        GetOrAdd(source).Victims.Add(new VictimRecord(target.Data.PlayerName, CurrentRound));    }

    [RegisterEvent]
    public static void EjectionHandler(EjectionEvent @event)
    {
        var exiled = @event.ExileController?.initData?.networkedPlayer?.Object;
        if (!exiled) return;

        var record = GetOrAdd(exiled!);
        record.Death = DeathCause.Ejected;
        record.DeathRound = CurrentRound;
    }

    [RegisterEvent]
    public static void PlayerLeaveHandler(PlayerLeaveEvent @event)
    {
        var player = @event.ClientData?.Character;
        if (!player || !Records.TryGetValue(player!.PlayerId, out var record)) return;

        record.Disconnected = true;
        if (record.Death != DeathCause.Alive) return;

        record.Death = DeathCause.Disconnected;
        record.DeathRound = CurrentRound;
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
/// <param name="Color">Its color.</param>
public sealed record RoleRecord(AmongUs.GameOptions.RoleTypes Type, string Name, Color Color);

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

    public int TasksDone { get; set; }

    public int TasksTotal { get; set; }

    public bool IsDead { get; set; }

    public bool Disconnected { get; set; }

    public DeathCause Death { get; set; } = DeathCause.Alive;

    public int DeathRound { get; set; }

    public string? KillerName { get; set; }

    public string? KillerRolePart { get; set; }

    public List<VictimRecord> Victims { get; } = [];
}
