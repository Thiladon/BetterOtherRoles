using BetterOtherRoles.Options;
using BetterOtherRoles.Roles;
using MiraAPI.GameOptions;
using MiraAPI.Roles;

namespace BetterOtherRoles.Modules.Roles;

/// <summary>
///     Role list assignment, based on TOU-Mira 1.7.9 (AssignRolesFromRoleList): one slot per player, each slot gives a
///     role of its category.
///     - The impostor count comes from the slots (<see cref="GetImpostorCount"/>), between 1 and 3.
///     - The slots are filled from the narrowest category to the broadest; a slot whose category has no role left falls
///       back to the broader one (Crew Killing, then Crew Special, Crew Random, Non-Impostor), and gives a plain
///       crewmate or impostor at the end.
///     - In a slot, a 100 % role first, otherwise a draw weighted by the chances (TOU-Mira SelectRole): the slot is always
///       filled when a role of its category is left.
///     - Exclusive roles (<see cref="IExclusiveRole"/>): a chosen role blocks the roles it cannot be with.
///     - Dependent roles (<see cref="IHasDependentRole"/>, the Deputy): when the main role comes out, the dependent role
///       (with its chance) takes a crewmate slot not filled yet, the broadest one.
/// </summary>
public static class BorRoleListSelector
{
    // Slots filled in this order, with the category used when no role is left (TOU-Mira order).
    private static readonly (RoleListSlot Slot, RoleListSlot? Fallback)[] FillOrder =
    [
        (RoleListSlot.ImpostorConcealing, RoleListSlot.ImpostorCommon),
        (RoleListSlot.ImpostorSupport, RoleListSlot.ImpostorCommon),
        (RoleListSlot.ImpostorKilling, RoleListSlot.ImpostorSpecial),
        (RoleListSlot.ImpostorPower, RoleListSlot.ImpostorSpecial),
        (RoleListSlot.ImpostorCommon, RoleListSlot.ImpostorRandom),
        (RoleListSlot.ImpostorSpecial, RoleListSlot.ImpostorRandom),
        (RoleListSlot.ImpostorRandom, null),
        (RoleListSlot.CrewInvestigative, RoleListSlot.CrewCommon),
        (RoleListSlot.CrewProtective, RoleListSlot.CrewCommon),
        (RoleListSlot.CrewSupport, RoleListSlot.CrewCommon),
        (RoleListSlot.CrewKilling, RoleListSlot.CrewSpecial),
        (RoleListSlot.CrewPower, RoleListSlot.CrewSpecial),
        (RoleListSlot.CrewCommon, RoleListSlot.CrewRandom),
        (RoleListSlot.CrewSpecial, RoleListSlot.CrewRandom),
        (RoleListSlot.CrewRandom, RoleListSlot.NonImpostor),
        (RoleListSlot.NeutralBenign, RoleListSlot.NeutralCommon),
        (RoleListSlot.NeutralEvil, RoleListSlot.NeutralCommon),
        (RoleListSlot.NeutralOutlier, RoleListSlot.NeutralSpecial),
        (RoleListSlot.NeutralKilling, RoleListSlot.NeutralSpecial),
        (RoleListSlot.NeutralCommon, RoleListSlot.NeutralRandom),
        (RoleListSlot.NeutralSpecial, RoleListSlot.NeutralRandom),
        (RoleListSlot.NeutralWildcard, RoleListSlot.NeutralRandom),
        (RoleListSlot.NeutralRandom, RoleListSlot.NonImpostor),
        (RoleListSlot.NonImpostor, null),
    ];

    // Crewmate slots a dependent role can take, the broadest first.
    private static readonly RoleListSlot[] DependentSlots =
    [
        RoleListSlot.NonImpostor, RoleListSlot.CrewRandom, RoleListSlot.CrewSpecial, RoleListSlot.CrewCommon,
        RoleListSlot.CrewKilling, RoleListSlot.CrewPower, RoleListSlot.CrewInvestigative, RoleListSlot.CrewProtective,
        RoleListSlot.CrewSupport,
    ];

    private static readonly RoleListSlot[] ImpostorSlots =
    [
        RoleListSlot.ImpostorConcealing, RoleListSlot.ImpostorKilling, RoleListSlot.ImpostorPower,
        RoleListSlot.ImpostorSupport, RoleListSlot.ImpostorCommon, RoleListSlot.ImpostorSpecial,
        RoleListSlot.ImpostorRandom,
    ];

    private static int _impostorCountFrame = -1;
    private static int _impostorCountPlayers;
    private static int _impostorCount;

    /// <summary>
    ///     Checks whether a slot gives an impostor role.
    /// </summary>
    public static bool IsImpostorSlot(RoleListSlot slot) => ImpostorSlots.Contains(slot);

    /// <summary>
    ///     Impostor count of the role list (TOU-Mira GetAdjustedNumImpostors): the impostor slots, plus a chance for each
    ///     "Any" slot, between 1 and 3 and below the crewmates count. The "Any" draws are kept for the whole frame, so the
    ///     admin logs and the assignment read the same count.
    /// </summary>
    /// <param name="players">The player count.</param>
    /// <returns>The impostor count.</returns>
    public static int GetImpostorCount(int players)
    {
        if (_impostorCountFrame == UnityEngine.Time.frameCount && _impostorCountPlayers == players)
            return _impostorCount;

        var slots = GetSlots(players);
        var impostors = slots.Count(IsImpostorSlot);
        var anySlots = slots.Count(x => x == RoleListSlot.Any);
        if (anySlots > 0)
        {
            var chance = (int)Math.Floor(players / (double)anySlots * 5 / 3);
            for (var i = 0; i < anySlots; i++)
            {
                if (BorRoleSelector.NextInt(100) < chance)
                    impostors++;

                chance += 3;
            }
        }

        var max = Math.Max(1, Math.Min(3, (players - 1) / 2));
        _impostorCount = Math.Clamp(impostors, 1, max);
        _impostorCountPlayers = players;
        _impostorCountFrame = UnityEngine.Time.frameCount;
        return _impostorCount;
    }

    /// <summary>
    ///     Selects the roles of the impostors and of the other players.
    /// </summary>
    /// <param name="impostorCount">The impostor players.</param>
    /// <param name="crewmateCount">The non-impostor players.</param>
    /// <returns>The impostor roles (one per impostor slot) and the crewmate and neutral roles.</returns>
    public static (List<ushort> Impostors, List<ushort> Crewmates) SelectRoles(int impostorCount, int crewmateCount)
    {
        var slots = GetSlots(impostorCount + crewmateCount);
        AdjustImpostorSlots(slots, impostorCount);
        EnsureCrewmateOrKiller(slots);

        var available = GetAvailableRoles();
        var impostorRoles = new List<ushort>();
        var crewmateRoles = new List<ushort>();

        foreach (var (slot, fallback) in FillOrder)
        {
            while (slots.Remove(slot))
            {
                var candidates = available.Where(x => Matches(slot, x.Alignment)).ToList();
                if (candidates.Count == 0)
                {
                    // Nothing left in this category: the broader one, or the plain role.
                    if (fallback is { } broader)
                        slots.Add(broader);

                    continue;
                }

                var chosen = Draw(candidates);
                available.Remove(chosen);
                (IsImpostorSlot(slot) ? impostorRoles : crewmateRoles).Add(chosen.RoleId);
                Block(available, chosen.RoleId);
                AddDependentRole(available, slots, crewmateRoles, chosen.RoleId);
            }
        }

        return (impostorRoles, crewmateRoles);
    }

    private static List<RoleListSlot> GetSlots(int players)
    {
        var options = OptionGroupSingleton<RoleAssignmentOptions>.Instance;
        var slots = options.Slots.Options.Take(Math.Min(players, RoleAssignmentOptions.RoleListSlotCount)).Select(x => x.Value).ToList();

        // Players beyond the slots: a random crewmate role (one in four) or a non-impostor role.
        while (slots.Count < players)
        {
            slots.Add(BorRoleSelector.NextInt(4) == 0 ? RoleListSlot.CrewRandom : RoleListSlot.NonImpostor);
        }

        return slots;
    }

    /// <summary>
    ///     As many impostor slots as impostors (TOU-Mira): the extra ones become non-impostor slots, the missing ones
    ///     replace random slots, then the "Any" slots become impostor slots while some are missing, non-impostor after.
    /// </summary>
    private static void AdjustImpostorSlots(List<RoleListSlot> slots, int impostorCount)
    {
        var impostors = slots.Count(IsImpostorSlot);
        while (impostors > impostorCount)
        {
            var index = RandomIndex(slots, IsImpostorSlot);
            slots[index] = RoleListSlot.NonImpostor;
            impostors--;
        }

        while (impostors + slots.Count(x => x == RoleListSlot.Any) < impostorCount && slots.Count > 0)
        {
            var index = RandomIndex(slots, x => !IsImpostorSlot(x) && x != RoleListSlot.Any);
            if (index < 0) break;

            slots[index] = RoleListSlot.ImpostorRandom;
            impostors++;
        }

        int any;
        while ((any = RandomIndex(slots, x => x == RoleListSlot.Any)) >= 0)
        {
            slots[any] = impostors < impostorCount ? RoleListSlot.ImpostorRandom : RoleListSlot.NonImpostor;
            if (slots[any] == RoleListSlot.ImpostorRandom)
                impostors++;
        }
    }

    /// <summary>
    ///     At least one crewmate or neutral killing slot (TOU-Mira EnsureCrewNeutralRoles), so that the non-impostors are
    ///     not only benign or evil neutrals.
    /// </summary>
    private static void EnsureCrewmateOrKiller(List<RoleListSlot> slots)
    {
        if (slots.Any(x => x is >= RoleListSlot.CrewInvestigative and <= RoleListSlot.CrewRandom or RoleListSlot.NeutralKilling))
            return;

        var index = slots.IndexOf(RoleListSlot.NeutralRandom);
        if (index >= 0)
        {
            slots[index] = RoleListSlot.NeutralKilling;
            return;
        }

        index = slots.IndexOf(RoleListSlot.NonImpostor);
        if (index < 0)
            index = RandomIndex(slots, x => !IsImpostorSlot(x));

        if (index >= 0)
            slots[index] = BorRoleSelector.NextInt(2) == 0 ? RoleListSlot.CrewRandom : RoleListSlot.NeutralKilling;
    }

    // One entry per role count: the mod roles that can spawn, with a chance above 0.
    private static List<(ushort RoleId, int Chance, BorRoleAlignment Alignment)> GetAvailableRoles()
    {
        var roleOptions = GameOptionsManager.Instance.CurrentGameOptions.RoleOptions;
        var roles = new List<(ushort RoleId, int Chance, BorRoleAlignment Alignment)>();
        foreach (var role in CustomRoleManager.CustomRoleBehaviours)
        {
            if (role is not IBorRole borRole || role.IsDead || !CustomRoleUtils.CanSpawnOnCurrentMode(role)) continue;

            var chance = roleOptions.GetChancePerGame(role.Role);
            if (chance <= 0) continue;

            for (var i = 0; i < roleOptions.GetNumPerGame(role.Role); i++)
            {
                roles.Add(((ushort)role.Role, chance, borRole.Alignment));
            }
        }

        return roles;
    }

    private static bool Matches(RoleListSlot slot, BorRoleAlignment alignment)
    {
        return slot switch
        {
            RoleListSlot.CrewInvestigative => alignment is BorRoleAlignment.CrewmateInvestigative,
            RoleListSlot.CrewKilling => alignment is BorRoleAlignment.CrewmateKilling,
            RoleListSlot.CrewProtective => alignment is BorRoleAlignment.CrewmateProtective,
            RoleListSlot.CrewPower => alignment is BorRoleAlignment.CrewmatePower,
            RoleListSlot.CrewSupport => alignment is BorRoleAlignment.CrewmateSupport,
            RoleListSlot.CrewCommon => alignment is BorRoleAlignment.CrewmateInvestigative or BorRoleAlignment.CrewmateProtective or BorRoleAlignment.CrewmateSupport,
            RoleListSlot.CrewSpecial => alignment is BorRoleAlignment.CrewmateKilling or BorRoleAlignment.CrewmatePower,
            RoleListSlot.CrewRandom => IsCrewmate(alignment),
            RoleListSlot.NeutralBenign => alignment is BorRoleAlignment.NeutralBenign,
            RoleListSlot.NeutralEvil => alignment is BorRoleAlignment.NeutralEvil,
            RoleListSlot.NeutralKilling => alignment is BorRoleAlignment.NeutralKilling,
            RoleListSlot.NeutralOutlier => alignment is BorRoleAlignment.NeutralOutlier,
            RoleListSlot.NeutralCommon => alignment is BorRoleAlignment.NeutralBenign or BorRoleAlignment.NeutralEvil,
            RoleListSlot.NeutralSpecial => alignment is BorRoleAlignment.NeutralOutlier or BorRoleAlignment.NeutralKilling,
            RoleListSlot.NeutralWildcard => alignment is BorRoleAlignment.NeutralBenign or BorRoleAlignment.NeutralEvil or BorRoleAlignment.NeutralOutlier,
            RoleListSlot.NeutralRandom => IsNeutral(alignment),
            RoleListSlot.ImpostorConcealing => alignment is BorRoleAlignment.ImpostorConcealing,
            RoleListSlot.ImpostorKilling => alignment is BorRoleAlignment.ImpostorKilling,
            RoleListSlot.ImpostorPower => alignment is BorRoleAlignment.ImpostorPower,
            RoleListSlot.ImpostorSupport => alignment is BorRoleAlignment.ImpostorSupport,
            RoleListSlot.ImpostorCommon => alignment is BorRoleAlignment.ImpostorConcealing or BorRoleAlignment.ImpostorSupport,
            RoleListSlot.ImpostorSpecial => alignment is BorRoleAlignment.ImpostorKilling or BorRoleAlignment.ImpostorPower,
            RoleListSlot.ImpostorRandom => alignment >= BorRoleAlignment.ImpostorConcealing,
            RoleListSlot.NonImpostor => IsCrewmate(alignment) || IsNeutral(alignment),
            _ => false,
        };

        static bool IsCrewmate(BorRoleAlignment x) => x <= BorRoleAlignment.CrewmateSupport;

        static bool IsNeutral(BorRoleAlignment x) => x is >= BorRoleAlignment.NeutralBenign and <= BorRoleAlignment.NeutralOutlier;
    }

    // A 100 % role first, otherwise a draw weighted by the chances (TOU-Mira SelectRole).
    private static (ushort RoleId, int Chance, BorRoleAlignment Alignment) Draw(List<(ushort RoleId, int Chance, BorRoleAlignment Alignment)> candidates)
    {
        var ensured = candidates.Where(x => x.Chance >= 100).ToList();
        if (ensured.Count > 0) return ensured[BorRoleSelector.NextInt(ensured.Count)];

        var draw = BorRoleSelector.NextInt(candidates.Sum(x => x.Chance));
        foreach (var candidate in candidates)
        {
            draw -= candidate.Chance;
            if (draw < 0) return candidate;
        }

        return candidates[^1];
    }

    // The roles that cannot be with the chosen one are never drawn afterwards.
    private static void Block(List<(ushort RoleId, int Chance, BorRoleAlignment Alignment)> available, ushort roleId)
    {
        if (BorRoleSelector.GetModRole(roleId) is not { } role) return;

        available.RemoveAll(x => x.RoleId != roleId && BorRoleSelector.GetModRole(x.RoleId) is { } other && BorRoleSelector.AreExclusive(role, other));
    }

    private static void AddDependentRole(
        List<(ushort RoleId, int Chance, BorRoleAlignment Alignment)> available,
        List<RoleListSlot> slots,
        List<ushort> crewmateRoles,
        ushort roleId)
    {
        if (BorRoleSelector.GetModRole(roleId) is not IHasDependentRole { DependentRoleId: { } dependentId } mainRole ||
            mainRole.DependentRoleChance <= 0 || BorRoleSelector.GetModRole(dependentId) is not { } dependent ||
            dependent.IsImpostor || BorRoleSelector.NextInt(100) >= mainRole.DependentRoleChance)
            return;

        // A role exclusive with the dependent one is already in the game.
        var chosen = crewmateRoles.Select(BorRoleSelector.GetModRole).Where(x => x != null);
        if (chosen.Any(x => x != null && BorRoleSelector.AreExclusive(dependent, x))) return;

        foreach (var slot in DependentSlots)
        {
            if (slots.Remove(slot))
            {
                crewmateRoles.Add(dependentId);
                Block(available, dependentId);
                return;
            }
        }
    }

    private static int RandomIndex(List<RoleListSlot> slots, Func<RoleListSlot, bool> predicate)
    {
        var indexes = Enumerable.Range(0, slots.Count).Where(i => predicate(slots[i])).ToList();
        return indexes.Count > 0 ? indexes[BorRoleSelector.NextInt(indexes.Count)] : -1;
    }
}
