using AmongUs.GameOptions;
using BetterOtherRoles.Modules.Admin;
using BetterOtherRoles.Options;
using BetterOtherRoles.Roles;
using MiraAPI.GameOptions;
using MiraAPI.Roles;

namespace BetterOtherRoles.Modules.Roles;

/// <summary>
///     Admin logs of the role assignment (host only, <see cref="BorAdmin"/>): the settings and chances before the
///     assignment, then "Role assignment: {name} -> {faction},{role},{modifier}" for each player.
///     The roles given with RpcSetRole are recorded, whatever gives them (our selector or MiraAPI). Role names are in English.
/// </summary>
public static class RoleAssignmentLog
{
    private const string NoModifier = "None";

    // Players and roles in the order they were given, null when not logging.
    private static List<(PlayerControl Player, RoleTypes Role)>? _assigned;

    /// <summary>
    ///     Logs the settings and chances, then starts recording the roles given.
    /// </summary>
    public static void Begin()
    {
        _assigned = null;
        if (!BorAdmin.IsAdmin || !AmongUsClient.Instance.AmHost) return;

        _assigned = [];
        var gameOptions = GameOptionsManager.Instance.CurrentGameOptions;
        var options = OptionGroupSingleton<RoleAssignmentOptions>.Instance;
        var players = GameData.Instance.AllPlayers.ToArray().Count(x => x != null && !x.Disconnected && x.Object != null);

        Info($"Role settings: {players} players, mode {(RoleAssignmentMode)options.Mode.Value}, " +
             $"impostors {gameOptions.GetAdjustedNumImpostors(players)} (setting {gameOptions.NumImpostors})");
        if (options.Mode.Value is (int)RoleAssignmentMode.MinMaxList)
        {
            Info($"Role settings: crewmate roles {options.MinCrewmateRoles.Value}-{options.MaxCrewmateRoles.Value}, " +
                 $"neutral roles {options.MinNeutralRoles.Value}-{options.MaxNeutralRoles.Value}, " +
                 $"impostor roles {options.MinImpostorRoles.Value}-{options.MaxImpostorRoles.Value}, " +
                 $"impostor streak reduction {(options.ReduceImpostorStreak.Value ? $"{options.ImpostorStreakChance.Value}%" : "off")}");
        }
        else if (options.Mode.Value is (int)RoleAssignmentMode.RoleList)
        {
            var slots = options.Slots.Options.Take(Math.Min(players, RoleAssignmentOptions.RoleListSlotCount)).Select(x => x.Value);
            Info($"Role settings: slots {string.Join(", ", slots)}, " +
                 $"impostor streak reduction {(options.ReduceImpostorStreak.Value ? $"{options.ImpostorStreakChance.Value}%" : "off")}");
        }

        var roleOptions = gameOptions.RoleOptions;
        foreach (var role in RoleManager.Instance.AllRoles.ToArray())
        {
            var managed = GetManagedRole(role.Role);
            if (managed.IsDead || role.Role is RoleTypes.Crewmate or RoleTypes.Impostor) continue;

            var chance = roleOptions.GetChancePerGame(role.Role);
            var count = roleOptions.GetNumPerGame(role.Role);
            if (chance > 0 && count > 0)
                Info($"Role chance: {BorRoleSelector.GetFaction(managed)},{GetRoleName(managed)},{chance}%,x{count}");

            if (managed is IHasDependentRole { DependentRoleId: { } dependentId, DependentRoleChance: > 0 } mainRole && chance > 0 && count > 0)
            {
                var dependent = GetManagedRole((RoleTypes)dependentId);
                Info($"Role chance: {BorRoleSelector.GetFaction(dependent)},{GetRoleName(dependent)},{mainRole.DependentRoleChance}% " +
                     $"(with {GetRoleName(managed)})");
            }
        }
    }

    /// <summary>
    ///     Records a role given during the assignment.
    /// </summary>
    public static void Record(PlayerControl player, RoleTypes role)
    {
        _assigned?.Add((player, role));
    }

    /// <summary>
    ///     Logs the role of each player and stops recording.
    /// </summary>
    public static void End()
    {
        if (_assigned == null) return;

        foreach (var (player, roleType) in _assigned)
        {
            var role = GetManagedRole(roleType);
            var name = player && player.Data != null ? player.Data.PlayerName : "?";
            Info($"Role assignment: {name} -> {BorRoleSelector.GetFaction(role)},{GetRoleName(role)},{NoModifier}");
        }

        _assigned = null;
    }

    // The managed object of a mod role (the RoleManager entries are plain RoleBehaviour wrappers, never ICustomRole).
    private static RoleBehaviour GetManagedRole(RoleTypes roleType)
    {
        return CustomRoleManager.CustomRoleBehaviours.FirstOrDefault(x => x.Role == roleType) ?? RoleManager.Instance.GetRole(roleType);
    }

    // English names, the same for every admin: the class name for the mod roles (SheriffRole -> Sheriff).
    private static string GetRoleName(RoleBehaviour role)
    {
        if (role is not ICustomRole) return role.Role.ToString();

        var name = role.GetType().Name;
        return name.EndsWith("Role", StringComparison.Ordinal) ? name[..^"Role".Length] : name;
    }
}
