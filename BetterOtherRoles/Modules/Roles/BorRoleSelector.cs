using BetterOtherRoles.Roles;
using MiraAPI.Roles;

namespace BetterOtherRoles.Modules.Roles;

/// <summary>
///     The factions of the min/max assignment.
/// </summary>
public enum BorFaction
{
    Crewmate,
    Neutral,
    Impostor,
}

/// <summary>
///     Picks the roles of the three factions together, as BetterOtherRoles 1.7.1:
///     the 100 % roles first, then the other roles: each one first rolls its chance (50 % = one game in two), and the
///     ones that pass share the slots left by a ticket draw weighted by the chance. At each step, the faction is drawn
///     at random among the ones with a free slot and a role left. Neutrals and crewmates share the non-impostor players.
///     Exclusive roles (<see cref="IExclusiveRole"/>): each assigned role blocks the roles it cannot be with,
///     even at 100 % (blocked role pairings of BetterOtherRoles 1.7.1).
/// </summary>
public static class BorRoleSelector
{
    private static readonly Random Rng = new();

    public static BorFaction GetFaction(RoleBehaviour role)
    {
        if (role.IsImpostor) return BorFaction.Impostor;

        return role is ICustomRole { Team: ModdedRoleTeams.Custom } ? BorFaction.Neutral : BorFaction.Crewmate;
    }

    /// <summary>
    ///     Draws a count between min and max (min lowered to max if needed), limited to the available players.
    /// </summary>
    public static int RollCount(float min, float max, int players)
    {
        var maxCount = (int)max;
        var minCount = Math.Min((int)min, maxCount);
        return Math.Min(Rng.Next(minCount, maxCount + 1), players);
    }

    /// <summary>
    ///     Gets the managed mod role object: CustomRoleManager.GetCustomRoleBehaviour reads plain RoleBehaviour
    ///     wrappers, never ICustomRole.
    /// </summary>
    public static RoleBehaviour? GetModRole(ushort roleId)
    {
        return CustomRoleManager.CustomRoleBehaviours.FirstOrDefault(x => (ushort)x.Role == roleId && x is ICustomRole);
    }

    /// <summary>
    ///     Checks whether two roles cannot be assigned in the same game, declared by either of them.
    /// </summary>
    public static bool AreExclusive(RoleBehaviour a, RoleBehaviour b)
    {
        return (a is IExclusiveRole exclusiveA && exclusiveA.ExclusiveWith.Any(t => t.IsInstanceOfType(b)))
               || (b is IExclusiveRole exclusiveB && exclusiveB.ExclusiveWith.Any(t => t.IsInstanceOfType(a)));
    }

    /// <summary>
    ///     Selects the roles of the three factions. A role with a count of N gives N entries.
    /// </summary>
    /// <param name="maxRoles">The number of roles to draw in each faction.</param>
    /// <param name="impostorPlayers">The impostor players.</param>
    /// <param name="crewmatePlayers">The non-impostor players, shared by the neutral and crewmate roles.</param>
    /// <returns>The selected roles of each faction.</returns>
    public static Dictionary<BorFaction, List<ushort>> SelectRoles(
        IReadOnlyDictionary<BorFaction, int> maxRoles,
        int impostorPlayers,
        int crewmatePlayers)
    {
        var factions = Enum.GetValues<BorFaction>();
        var selected = factions.ToDictionary(x => x, _ => new List<ushort>());
        var slots = factions.ToDictionary(x => x, x => maxRoles.TryGetValue(x, out var max) ? max : 0);
        var ensured = factions.ToDictionary(x => x, _ => new List<ushort>());
        var tickets = factions.ToDictionary(x => x, _ => new List<(ushort RoleId, int Chance)>());
        var blocked = new HashSet<ushort>();
        var playersLeft = new Dictionary<bool, int> { [true] = impostorPlayers, [false] = crewmatePlayers };

        var roleOptions = GameOptionsManager.Instance.CurrentGameOptions.RoleOptions;

        // The managed role objects: the AllRoles entries are plain RoleBehaviour wrappers, never ICustomRole.
        foreach (var role in CustomRoleManager.CustomRoleBehaviours)
        {
            if (role is not ICustomRole || role.IsDead || !CustomRoleUtils.CanSpawnOnCurrentMode(role)) continue;

            var chance = roleOptions.GetChancePerGame(role.Role);
            if (chance <= 0) continue;

            var faction = GetFaction(role);
            for (var i = 0; i < roleOptions.GetNumPerGame(role.Role); i++)
            {
                if (chance >= 100)
                {
                    ensured[faction].Add((ushort)role.Role);
                }
                else
                {
                    AddTicket(faction, (ushort)role.Role, chance);
                }
            }
        }

        // 100 % roles: a random faction, then a random role of it.
        while (TryDrawFaction(x => ensured[x].Count > 0, out var faction))
        {
            var list = ensured[faction];
            var index = Rng.Next(list.Count);
            var roleId = list[index];
            list.RemoveAt(index);
            Select(faction, roleId);
        }

        // Other roles: a random faction, then a ticket of it weighted by the chance.
        while (TryDrawFaction(x => tickets[x].Count > 0, out var faction))
        {
            var list = tickets[faction];
            var index = DrawTicket(list);
            var roleId = list[index].RoleId;
            list.RemoveAt(index);
            Select(faction, roleId);
        }

        return selected;

        bool HasSlot(BorFaction faction) => slots[faction] > 0 && playersLeft[faction == BorFaction.Impostor] > 0;

        bool TryDrawFaction(Func<BorFaction, bool> hasRoles, out BorFaction faction)
        {
            var available = factions.Where(x => HasSlot(x) && hasRoles(x)).ToList();
            faction = available.Count > 0 ? available[Rng.Next(available.Count)] : default;
            return available.Count > 0;
        }

        void Select(BorFaction faction, ushort roleId)
        {
            selected[faction].Add(roleId);
            slots[faction]--;
            playersLeft[faction == BorFaction.Impostor]--;
            Block(roleId);

            if (GetModRole(roleId) is not IHasDependentRole { DependentRoleId: { } dependentId } mainRole ||
                mainRole.DependentRoleChance <= 0 || blocked.Contains(dependentId) ||
                GetModRole(dependentId) is not { } dependent)
                return;

            var dependentFaction = GetFaction(dependent);
            if (mainRole.DependentRoleChance < 100)
            {
                AddTicket(dependentFaction, dependentId, mainRole.DependentRoleChance);
            }
            else if (HasSlot(dependentFaction))
            {
                Select(dependentFaction, dependentId);
            }
        }

        // The roles that cannot be with this one are never drawn afterwards, whatever their faction and chance.
        void Block(ushort roleId)
        {
            if (GetModRole(roleId) is not { } role) return;

            foreach (var other in CustomRoleManager.CustomRoleBehaviours)
            {
                var otherId = (ushort)other.Role;
                if (otherId == roleId || other is not ICustomRole || !AreExclusive(role, other) || !blocked.Add(otherId))
                    continue;

                foreach (var faction in factions)
                {
                    ensured[faction].RemoveAll(x => x == otherId);
                    tickets[faction].RemoveAll(x => x.RoleId == otherId);
                }
            }
        }

        // A ticket only for a role that passes its chance roll.
        void AddTicket(BorFaction faction, ushort roleId, int chance)
        {
            if (!blocked.Contains(roleId) && Rng.Next(100) < chance)
                tickets[faction].Add((roleId, chance));
        }
    }

    public static void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public static int NextInt(int maxExclusive) => Rng.Next(maxExclusive);

    public static double NextDouble() => Rng.NextDouble();

    /// <summary>
    ///     Draws a ticket index: each entry weighs its chance.
    /// </summary>
    private static int DrawTicket(List<(ushort RoleId, int Chance)> tickets)
    {
        var draw = Rng.Next(tickets.Sum(x => x.Chance));
        for (var i = 0; i < tickets.Count; i++)
        {
            draw -= tickets[i].Chance;
            if (draw < 0) return i;
        }

        return tickets.Count - 1;
    }
}
