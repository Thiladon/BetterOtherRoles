using System.Reflection;
using AmongUs.GameOptions;
using BetterOtherRoles.Modules.Roles;
using BetterOtherRoles.Options;
using HarmonyLib;
using MiraAPI.GameModes;
using MiraAPI.GameOptions;
using MiraAPI.Roles;
using MiraSelectRolesPatch = MiraAPI.Patches.Roles.SelectRolesPatch;

namespace BetterOtherRoles.Patches.Roles;

/// <summary>
///     Role assignment at the game start, based on TOU-Mira 1.7.9 (RoleManagerPatches):
///     impostor choice, then a min/max for each faction (<see cref="RoleAssignmentOptions"/>) or the role list slots
///     (<see cref="BorRoleListSelector"/>).
///     The "vanilla roles only" mode leaves the assignment to MiraAPI, without the mod roles.
/// </summary>
[HarmonyPatch]
public static class RoleAssignmentPatches
{
    /// <summary>
    ///     Impostors of the last game (client ids), for <see cref="RoleAssignmentOptions.ReduceImpostorStreak"/>.
    /// </summary>
    private static List<int> _lastImpostors = [];

    /// <summary>
    ///     Gets whether MiraAPI is giving the roles in the "vanilla roles only" mode: the mod roles count as 0 meanwhile
    ///     (only meanwhile, MiraAPI's role menu reads the same values).
    /// </summary>
    public static bool IsSelectingVanillaRolesOnly { get; private set; }

    [HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static bool SelectRolesPrefix()
    {
        var options = OptionGroupSingleton<RoleAssignmentOptions>.Instance;
        if (!AmongUsClient.Instance.AmHost || TutorialManager.InstanceExists || GameManager.Instance.IsHideAndSeek() ||
            !CustomGameModeManager.IsClassic() || RoleAssignmentOptions.IsVanillaRolesOnly)
        {
            IsSelectingVanillaRolesOnly = AmongUsClient.Instance.AmHost && RoleAssignmentOptions.IsVanillaRolesOnly;
            MiraSelectRolesPatch.ApiHandlesRoleSelect = true;
            RoleAssignmentLog.Begin();
            return true;
        }

        MiraSelectRolesPatch.ApiHandlesRoleSelect = false;
        RoleAssignmentLog.Begin();

        var players = GameData.Instance.AllPlayers.ToArray()
            .Where(x => x != null && !x.Disconnected && x.Object != null)
            .ToList();
        BorRoleSelector.Shuffle(players);

        var impostorCount = GameOptionsManager.Instance.CurrentGameOptions.GetAdjustedNumImpostors(players.Count);
        var infected = ChooseImpostors(players, impostorCount, options);

        var impostors = infected.Select(x => x.Object).ToList();
        var crewmates = players.Where(x => !infected.Contains(x)).Select(x => x.Object).ToList();

        if (RoleAssignmentOptions.IsRoleList)
        {
            var (listImpostorRoles, listCrewmateRoles) = BorRoleListSelector.SelectRoles(impostors.Count, crewmates.Count);
            AssignRoles(impostors, listImpostorRoles, RoleTypes.Impostor);
            AssignRoles(crewmates, listCrewmateRoles, RoleTypes.Crewmate);
            return false;
        }

        // Crewmates must always outnumber the neutrals (TOU-Mira), without going below the min.
        var neutralCount = BorRoleSelector.RollCount(options.MinNeutralRoles.Value, options.MaxNeutralRoles.Value, crewmates.Count);
        var minNeutrals = Math.Min((int)options.MinNeutralRoles.Value, (int)options.MaxNeutralRoles.Value);
        while (neutralCount > minNeutrals && neutralCount >= Math.Ceiling(crewmates.Count / 2d))
        {
            neutralCount--;
        }

        // The three factions drawn together, a random one at each step (BetterOtherRoles 1.7.1).
        var roles = BorRoleSelector.SelectRoles(
            new Dictionary<BorFaction, int>
            {
                [BorFaction.Impostor] = BorRoleSelector.RollCount(options.MinImpostorRoles.Value, options.MaxImpostorRoles.Value, impostors.Count),
                [BorFaction.Neutral] = neutralCount,
                [BorFaction.Crewmate] = BorRoleSelector.RollCount(options.MinCrewmateRoles.Value, options.MaxCrewmateRoles.Value, crewmates.Count),
            },
            impostors.Count,
            crewmates.Count);
        var impostorRoles = roles[BorFaction.Impostor];
        var neutralRoles = roles[BorFaction.Neutral];
        var crewmateRoles = roles[BorFaction.Crewmate];

        AssignRoles(impostors, impostorRoles, RoleTypes.Impostor);
        AssignRoles(crewmates, [.. neutralRoles, .. crewmateRoles], RoleTypes.Crewmate);

        return false;
    }

    [HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
    [HarmonyPostfix]
    public static void SelectRolesPostfix()
    {
        IsSelectingVanillaRolesOnly = false;
        RoleAssignmentLog.End();
    }

    /// <summary>
    ///     Records the roles given during the assignment, for the admin logs (<see cref="RoleAssignmentLog"/>).
    /// </summary>
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSetRole))]
    [HarmonyPostfix]
    public static void RpcSetRolePostfix(PlayerControl __instance, RoleTypes __0)
    {
        RoleAssignmentLog.Record(__instance, __0);
    }

    /// <summary>
    ///     Impostor choice of TOU-Mira: shuffled players, the last game impostors being skipped with the streak chance.
    /// </summary>
    private static List<NetworkedPlayerInfo> ChooseImpostors(List<NetworkedPlayerInfo> players, int impostorCount, RoleAssignmentOptions options)
    {
        var infected = new List<NetworkedPlayerInfo>();

        if (options.ReduceImpostorStreak.Value && _lastImpostors.Count > 0)
        {
            var skipChance = options.ImpostorStreakChance.Value / 100d;
            while (infected.Count < impostorCount)
            {
                var candidates = players.Where(x => !infected.Contains(x)).ToList();
                if (candidates.Count == 0)
                {
                    break;
                }

                // Only last game impostors left: no more skipping.
                if (candidates.All(x => _lastImpostors.Contains(x.ClientId)))
                {
                    infected.AddRange(candidates.Take(impostorCount - infected.Count));
                    break;
                }

                var player = candidates[BorRoleSelector.NextInt(candidates.Count)];
                if (_lastImpostors.Contains(player.ClientId) && BorRoleSelector.NextDouble() < skipChance)
                {
                    continue;
                }

                infected.Add(player);
            }
        }
        else
        {
            infected.AddRange(players.Take(impostorCount));
        }

        _lastImpostors = [.. infected.Select(x => x.ClientId)];
        return infected;
    }

    /// <summary>
    ///     Gives each role to a random player, then the default role to the players left.
    /// </summary>
    private static void AssignRoles(List<PlayerControl> players, List<ushort> roles, RoleTypes defaultRole)
    {
        BorRoleSelector.Shuffle(roles);

        foreach (var roleId in roles)
        {
            if (players.Count == 0)
            {
                break;
            }

            var index = BorRoleSelector.NextInt(players.Count);
            var player = players[index];
            players.RemoveAt(index);

            player.RpcSetRole((RoleTypes)roleId);
        }

        foreach (var player in players)
        {
            player.RpcSetRole(defaultRole);
        }
    }

    /// <summary>
    ///     Number of impostors as BetterOtherRoles 1.7.1: the game setting between 1 and 3, without the vanilla limits.
    ///     Kept below the crewmates count, or the game would end at once (always 1 with less than 5 players).
    ///     In the role list mode, the count comes from the slots.
    /// </summary>
    [HarmonyPatch(typeof(IGameOptionsExtensions), nameof(IGameOptionsExtensions.GetAdjustedNumImpostors))]
    [HarmonyPostfix]
    public static void GetAdjustedNumImpostorsPostfix(IGameOptions __0, int __1, ref int __result)
    {
        if (GameManager.Instance && GameManager.Instance.IsHideAndSeek())
        {
            return;
        }

        if (RoleAssignmentOptions.IsRoleList)
        {
            __result = BorRoleListSelector.GetImpostorCount(__1);
            return;
        }

        var max = Math.Max(1, Math.Min(3, (__1 - 1) / 2));
        __result = Math.Clamp(__0.NumImpostors, 1, max);
    }
}

/// <summary>
///     Vanilla roles (Scientist, Engineer, Guardian Angel...) are disabled, as in BetterOtherRoles 1.7.1:
///     their count and chance are 0 in classic games, except in the "vanilla roles only" mode.
///     In that mode, the mod roles are 0 while MiraAPI gives the roles.
/// </summary>
[HarmonyPatch]
public static class VanillaRolesPatch
{
    private const string CollectionPrefix = "RoleOptionsCollectionV";

    /// <summary>
    ///     Gets whether the vanilla roles are disabled (classic games outside the "vanilla roles only" mode).
    /// </summary>
    public static bool AreVanillaRolesDisabled =>
        !(GameManager.Instance && GameManager.Instance.IsHideAndSeek()) && !RoleAssignmentOptions.IsVanillaRolesOnly;

    /// <summary>
    ///     GetNumPerGame and GetChancePerGame of the latest RoleOptionsCollectionV* class, as MiraAPI does (RoleOptionsCollectionPatch).
    /// </summary>
    public static IEnumerable<MethodBase> TargetMethods()
    {
        var collectionType = AccessTools.GetTypesFromAssembly(typeof(RoleManager).Assembly)
            .Where(x => x.Name.StartsWith(CollectionPrefix, StringComparison.Ordinal))
            .OrderByDescending(x => int.TryParse(x.Name[CollectionPrefix.Length..], out var version) ? version : -1)
            .First();

        yield return AccessTools.Method(collectionType, "GetNumPerGame");
        yield return AccessTools.Method(collectionType, "GetChancePerGame");
    }

    public static void Postfix(RoleTypes role, ref int __result)
    {
        var isModRole = CustomRoleManager.GetCustomRoleBehaviour(role, out _);
        if (isModRole ? RoleAssignmentPatches.IsSelectingVanillaRolesOnly : AreVanillaRolesDisabled)
        {
            __result = 0;
        }
    }
}
