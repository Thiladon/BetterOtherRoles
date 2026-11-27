using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using AmongUs.Data;
using AmongUs.GameOptions;
using BepInEx;
using BetterOtherRoles.Options.Maps;
using HarmonyLib;

namespace BetterOtherRoles.Modules.Summary;

/// <summary>
///     Statistics of each game written by the host in the AmongStat format (FormatVersion 2), from
///     <see cref="BorGameHistory"/>: one file per lobby, AmongStat/AmongStat_{lobby}_{date}.json in the game folder, each
///     game added to its Games at the end of the game. The file follows the lobby (same lobby code, even after
///     midnight); a new lobby makes a new file. Its name and date are set when it is made; LobbyPlayerNames is made
///     again with each game. Read once at the end of the evening; also sent to the AmongStat server with
///     <see cref="BorAmongStatUpload"/>.
/// </summary>
[HarmonyPatch]
public static class BorAmongStatExport
{
    private const string FormatVersion = "2";
    private const string ModName = "BetterOtherRoles";
    private const string Folder = "AmongStat";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    // Lobby of the current file: the file goes on while the lobby code stays the same.
    private static int? _lobbyCode;
    private static string? _filePath;

    // Read before the end of the game, while the map is there.
    private static string _mapName = string.Empty;
    private static int _totalTasks;

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
    [HarmonyPrefix]
    public static void OnGameEndPrefix()
    {
        var options = GameOptionsManager.Instance?.CurrentGameOptions;
        if (options == null) return;

        _mapName = MapName((MapNames)options.MapId);
        _totalTasks = options.GetInt(Int32OptionNames.NumCommonTasks) + options.GetInt(Int32OptionNames.NumLongTasks) +
                      options.GetInt(Int32OptionNames.NumShortTasks);
    }

    // The winners are known on the end game screen.
    [HarmonyPatch(typeof(EndGameManager), "SetEverythingUp")]
    [HarmonyPostfix]
    public static void SetEverythingUpPostfix()
    {
        if (!AmongUsClient.Instance || !AmongUsClient.Instance.AmHost || TutorialManager.InstanceExists ||
            BorGameHistory.Players.Count == 0)
            return;

        try
        {
            Write(MakeGame());
        }
        catch (Exception e)
        {
            Error($"[AmongStat] the game could not be written: {e}");
        }
    }

    private static void Write(AmongStatGame game)
    {
        var path = GetFilePath();
        var file = File.Exists(path) ? JsonSerializer.Deserialize<AmongStatFile>(File.ReadAllText(path), JsonOptions) : null;
        file ??= new AmongStatFile
        {
            FormatVersion = FormatVersion,
            LobbyName = LobbyName,
            Date = DateTime.Today.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
        };

        game.GameNumber = file.Games.Count + 1;
        file.Games.Add(game);

        // Every player who took part in the lobby: the players of the games of the file, sorted.
        file.LobbyPlayerNames = [.. file.Games.SelectMany(x => x.Entries).Select(x => x.PlayerName).Distinct().OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(file, JsonOptions), new UTF8Encoding(false));
        Info($"[AmongStat] game {game.GameNumber} written to {path}");

        BorAmongStatUpload.Send(Path.GetFileName(path), file, JsonOptions);
    }

    // Same lobby code: same file. Another lobby: a new file, with a number when the name of the day is taken.
    private static string GetFilePath()
    {
        var code = AmongUsClient.Instance.GameId;
        if (_lobbyCode == code && _filePath != null) return _filePath;

        var folder = Path.Combine(Paths.GameRootPath, Folder);
        var name = $"AmongStat_{FileName(LobbyName)}_{DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        var path = Path.Combine(folder, name + ".json");
        var number = 2;
        while (File.Exists(path))
        {
            path = Path.Combine(folder, $"{name}_{number}.json");
            number++;
        }

        _lobbyCode = code;
        _filePath = path;
        return path;
    }

    // "lobby " and the host's name (the host writes the file).
    private static string LobbyName => "lobby " + DataManager.Player.Customization.Name;

    // As in the example of the format: "Brybry / Codaiche" gives "Brybry___Codaiche".
    private static string FileName(string text) =>
        new([.. text.Select(c => char.IsLetterOrDigit(c) || c is '-' or '.' ? c : '_')]);

    private static AmongStatGame MakeGame()
    {
        var reason = EndGameResult.CachedGameOverReason;
        var winners = EndGameResult.CachedWinners.ToArray().Select(x => x.PlayerName).ToHashSet();

        return new AmongStatGame
        {
            ModName = ModName,
            MapName = _mapName,
            WinningAlignment = WinningAlignment(reason),
            VictoryCondition = VictoryCondition(reason),
            TotalTasks = _totalTasks,
            Entries = [.. BorGameHistory.Players.OrderBy(x => x.PlayerId).Select(x => MakeEntry(x, winners.Contains(x.PlayerName)))],
            Meetings = [.. BorGameHistory.Meetings.Select(MakeMeeting)],
        };
    }

    private static AmongStatEntry MakeEntry(PlayerRecord record, bool winner)
    {
        var first = record.Roles.FirstOrDefault()?.EnglishName ?? string.Empty;
        var last = record.Roles.LastOrDefault()?.EnglishName;
        var dead = record.Death is not (DeathCause.Alive or DeathCause.Disconnected);

        return new AmongStatEntry
        {
            PlayerName = record.PlayerName,
            RoleName = first,
            FinalRoleName = last != null && last != first ? last : null,
            ModifierNames = record.EnglishModifiers,

            // A player who killed himself (Sheriff misfire) is his own killer, as in the example of the format.
            KilledByPlayerName = record.Death == DeathCause.Suicide ? record.PlayerName : record.KillerName,
            EjectedInMeetingNumber = record.EjectedInMeeting,
            DeathRound = dead ? record.DeathRound : null,
            KillType = KillType(record, dead),
            TasksCompleted = record.TasksTotal > 0 ? record.TasksDone : null,
            ExtraTasksCount = null,
            IsWinner = winner,
            HasCrashed = record.Disconnected,
            HasOwnVictoryCondition = false,
        };
    }

    // None for a player alive at the end (or who left alive); Power for the Sheriff's misfire; Classic for a kill (the
    // Sheriff's too) and for an ejection, as in the example of the format.
    private static string? KillType(PlayerRecord record, bool dead)
    {
        if (!dead) return null;
        return record.Death == DeathCause.Suicide ? "Power" : "Classic";
    }

    // BodyReport and EmergencyButton need the name of the player (else the import fails with "unknown player"); with
    // no name, Unknown and no player.
    private static AmongStatMeeting MakeMeeting(MeetingRecord meeting) => new()
    {
        MeetingNumber = meeting.Number,
        TriggeredByPlayerName = string.IsNullOrEmpty(meeting.TriggeredBy) ? null : meeting.TriggeredBy,
        TriggerType = TriggerType(meeting),
        EjectedPlayerName = meeting.EjectedName,
        SkipCount = meeting.SkipCount,
        WasTiebreaker = false,
        PowerUsed = false,
        Votes = [.. meeting.Votes.Select(x => new AmongStatVote { PlayerName = x.Key, VoteCount = x.Value })],
    };

    private static string TriggerType(MeetingRecord meeting)
    {
        if (string.IsNullOrEmpty(meeting.TriggeredBy)) return "Unknown";
        return meeting.BodyReport ? "BodyReport" : "EmergencyButton";
    }

    // The name of the map, with " (better)" for Better Polus and Better Skeld.
    private static string MapName(MapNames map)
    {
        var name = map switch
        {
            MapNames.Skeld => "The Skeld",
            MapNames.MiraHQ => "MIRA HQ",
            MapNames.Polus => "Polus",
            MapNames.Dleks => "dlekS",
            MapNames.Airship => "Airship",
            MapNames.Fungle => "The Fungle",
            _ => map.ToString(),
        };
        var better = (map == MapNames.Polus && BetterPolusOptions.IsOn()) || (map == MapNames.Skeld && BetterSkeldOptions.IsOn());
        return better ? $"{name} (better)" : name;
    }

    private static string WinningAlignment(GameOverReason reason) => reason switch
    {
        GameOverReason.CrewmatesByVote or GameOverReason.CrewmatesByTask or GameOverReason.ImpostorDisconnect => "Crewmate",
        _ => "Impostor",
    };

    // Tasks, NumericalSuperiority, RoleConditions (a win of its own to a role) or Sabotage (Impostor only). No value
    // for a disconnection: NumericalSuperiority (all the impostors are gone, or not enough crewmates), the player who
    // left has HasCrashed.
    private static string VictoryCondition(GameOverReason reason) => reason switch
    {
        GameOverReason.CrewmatesByTask => "Tasks",
        GameOverReason.ImpostorsBySabotage => "Sabotage",
        _ => "NumericalSuperiority",
    };
}

/// <summary>
///     A file of the AmongStat format: the games of a lobby.
/// </summary>
public sealed class AmongStatFile
{
    public string FormatVersion { get; set; } = string.Empty;

    public string LobbyName { get; set; } = string.Empty;

    public List<string> LobbyPlayerNames { get; set; } = [];

    public string Date { get; set; } = string.Empty;

    public List<AmongStatGame> Games { get; set; } = [];
}

/// <summary>
///     A game of the AmongStat format.
/// </summary>
public sealed class AmongStatGame
{
    public int GameNumber { get; set; }

    public string ModName { get; set; } = string.Empty;

    public string MapName { get; set; } = string.Empty;

    public string WinningAlignment { get; set; } = string.Empty;

    public string VictoryCondition { get; set; } = string.Empty;

    public int TotalTasks { get; set; }

    public List<AmongStatEntry> Entries { get; set; } = [];

    public List<AmongStatMeeting> Meetings { get; set; } = [];
}

/// <summary>
///     A player in a game of the AmongStat format.
/// </summary>
public sealed class AmongStatEntry
{
    public string PlayerName { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public string? FinalRoleName { get; set; }

    public List<string> ModifierNames { get; set; } = [];

    public string? KilledByPlayerName { get; set; }

    public int? EjectedInMeetingNumber { get; set; }

    public int? DeathRound { get; set; }

    public string? KillType { get; set; }

    public int? TasksCompleted { get; set; }

    public int? ExtraTasksCount { get; set; }

    public bool IsWinner { get; set; }

    public bool HasCrashed { get; set; }

    public bool HasOwnVictoryCondition { get; set; }
}

/// <summary>
///     A meeting in a game of the AmongStat format.
/// </summary>
public sealed class AmongStatMeeting
{
    public int MeetingNumber { get; set; }

    public string? TriggeredByPlayerName { get; set; }

    public string TriggerType { get; set; } = string.Empty;

    public string? EjectedPlayerName { get; set; }

    public int SkipCount { get; set; }

    public bool WasTiebreaker { get; set; }

    public bool PowerUsed { get; set; }

    public List<AmongStatVote> Votes { get; set; } = [];
}

/// <summary>
///     The votes against a player in a meeting of the AmongStat format.
/// </summary>
public sealed class AmongStatVote
{
    public string PlayerName { get; set; } = string.Empty;

    public int VoteCount { get; set; }
}
