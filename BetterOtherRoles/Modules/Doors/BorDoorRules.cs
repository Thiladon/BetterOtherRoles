using System.Diagnostics.CodeAnalysis;
using BetterOtherRoles.Options.Maps;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using MiraAPI.GameOptions;
using Reactor.Utilities.Attributes;

namespace BetterOtherRoles.Modules.Doors;

/// <summary>
///     Door options of BetterOtherRoles (<see cref="DoorRulesOptions"/>), for every map and type of doors:
///     - time the doors that open by themselves stay closed, time before the doors of a room can be closed again (the
///       game writes 10 s and 30 s in its code);
///     - mushrooms to hit on a door of The Fungle (6 in the game);
///     - sabotages while doors are closed, doors closed during a sabotage: as the doors used (the doors of The Skeld
///       stop the sabotages, the doors of Polus can be closed during a sabotage), or always, or never.
/// </summary>
[HarmonyPatch]
public static class BorDoorRules
{
    private static DoorRulesOptions Options => OptionGroupSingleton<DoorRulesOptions>.Instance;

    /// <summary>
    ///     Gets the time before the impostors can sabotage or close doors at the start of the game.
    /// </summary>
    public static float InitialCooldown => Options.InitialCooldown.Value;

    /// <summary>
    ///     Gets how long the doors that open by themselves stay closed.
    /// </summary>
    public static float ClosedDuration => Options.ClosedDuration.Value;

    /// <summary>
    ///     Gets the time before the doors of a room can be closed again.
    /// </summary>
    public static float Cooldown => Options.Cooldown.Value;

    /// <summary>
    ///     Gets whether closed doors stop the sabotages, with the doors used.
    /// </summary>
    public static bool DoorsStopSabotages => (DoorRule)Options.SabotageWithClosedDoors.Value switch
    {
        DoorRule.Yes => false,
        DoorRule.No => true,
        _ => BorDoors.CurrentType == BorDoorType.Skeld,
    };

    /// <summary>
    ///     Gets whether doors can be closed during a sabotage, with the doors used.
    /// </summary>
    public static bool DoorsDuringSabotage => (DoorRule)Options.CloseDoorsDuringSabotage.Value switch
    {
        DoorRule.Yes => true,
        DoorRule.No => false,
        _ => BorDoors.CurrentType == BorDoorType.Polus,
    };

    // ---------------------------------------------------------------------------------------------------------------
    // Cooldown at the start of the game (10 s written in the code of the game, set after the intro on every player)
    // ---------------------------------------------------------------------------------------------------------------

    [HarmonyPatch(typeof(SabotageSystemType), nameof(SabotageSystemType.SetInitialSabotageCooldown))]
    [HarmonyPostfix]
    public static void SabotageInitialCooldownPostfix(SabotageSystemType __instance)
    {
        __instance.Timer = InitialCooldown;

        // Left on, the flag would keep dividing the next cooldowns by the initial one (black icons).
        if (InitialCooldown <= 0f) __instance.initialCooldown = false;
    }

    [HarmonyPatch(typeof(SabotageSystemType), nameof(SabotageSystemType.PercentCool), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool SabotagePercentCoolPrefix(SabotageSystemType __instance, ref float __result)
    {
        if (!__instance.initialCooldown || InitialCooldown <= 0f) return true;

        __result = __instance.Timer / InitialCooldown;
        return false;
    }

    [HarmonyPatch(typeof(AutoDoorsSystemType), nameof(AutoDoorsSystemType.SetInitialSabotageCooldown))]
    [HarmonyPostfix]
    public static void AutoInitialCooldownPostfix(AutoDoorsSystemType __instance) => __instance.initialCooldown = InitialCooldown;

    [HarmonyPatch(typeof(DoorsSystemType), nameof(DoorsSystemType.SetInitialSabotageCooldown))]
    [HarmonyPostfix]
    public static void ManualInitialCooldownPostfix(DoorsSystemType __instance) => __instance.initialCooldown = InitialCooldown;

    // ---------------------------------------------------------------------------------------------------------------
    // Durations
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>
    ///     Sets the durations of a door that opens by itself, just closed (the game writes 10 s and 30 s in
    ///     AutoOpenDoor.SetDoorway). Not a patch of SetDoorway: a virtual method called by the game, whose patch made the
    ///     game crash when the door opened again.
    /// </summary>
    /// <param name="door">A door of the map.</param>
    public static void ApplyClosed(OpenableDoor door)
    {
        var autoDoor = door ? door.TryCast<AutoOpenDoor>() : null;
        if (!autoDoor || autoDoor!.IsOpen) return;

        autoDoor.ClosedTimer = ClosedDuration;
        autoDoor.CooldownTimer = Cooldown;
    }

    /// <summary>
    ///     Gets whether each door of the map is open, before the doors are read from the host.
    /// </summary>
    /// <returns>The states, in the order of AllDoors.</returns>
    public static bool[] OpenStates() => ShipStatus.Instance ? ShipStatus.Instance.AllDoors.Select(x => x && x.IsOpen).ToArray() : [];

    /// <summary>
    ///     Sets the durations of the doors closed by the host (read from it): each player makes the doors open again.
    /// </summary>
    /// <param name="before">The states before (<see cref="OpenStates"/>).</param>
    public static void ApplyNewlyClosed(bool[] before)
    {
        if (!ShipStatus.Instance) return;

        var doors = ShipStatus.Instance.AllDoors;
        for (var i = 0; i < doors.Length && i < before.Length; i++)
        {
            if (before[i]) ApplyClosed(doors[i]);
        }
    }

    /// <summary>
    ///     Doors of The Skeld closed by the host (system of the game).
    /// </summary>
    [HarmonyPatch(typeof(AutoDoorsSystemType), nameof(AutoDoorsSystemType.CloseDoorsOfType))]
    [HarmonyPostfix]
    public static void AutoCloseDoorsPostfix(SystemTypes room)
    {
        foreach (var door in ShipStatus.Instance.AllDoors)
        {
            if (door && door.Room == room) ApplyClosed(door);
        }
    }

    [HarmonyPatch(typeof(AutoDoorsSystemType), nameof(AutoDoorsSystemType.Deserialize))]
    [HarmonyPrefix]
    public static void AutoDeserializePrefix(out bool[] __state) => __state = OpenStates();

    [HarmonyPatch(typeof(AutoDoorsSystemType), nameof(AutoDoorsSystemType.Deserialize))]
    [HarmonyPostfix]
    public static void AutoDeserializePostfix(bool[] __state) => ApplyNewlyClosed(__state);

    [HarmonyPatch(typeof(AutoDoorsSystemType), nameof(AutoDoorsSystemType.GetTimer))]
    [HarmonyPrefix]
    public static bool AutoGetTimerPrefix(AutoDoorsSystemType __instance, SystemTypes room, ref float __result)
    {
        if (__instance.initialCooldown > 0f)
        {
            __result = __instance.initialCooldown / InitialCooldown;
            return false;
        }

        __result = 0f;
        foreach (var door in ShipStatus.Instance.AllDoors)
        {
            var autoDoor = door.Room == room ? door.TryCast<AutoOpenDoor>() : null;
            if (!autoDoor) continue;

            __result = autoDoor!.CooldownTimer / Cooldown;
            break;
        }

        return false;
    }

    /// <summary>
    ///     Doors opened with a minigame (Polus, Airship, The Fungle): cooldown by room.
    /// </summary>
    [HarmonyPatch(typeof(DoorsSystemType), nameof(DoorsSystemType.CloseDoorsOfType))]
    [HarmonyPostfix]
    public static void ManualCloseDoorsPostfix(DoorsSystemType __instance, SystemTypes room) => __instance.timers[room] = Cooldown;

    // The game keeps the timers between 0 and 30 s.
    [HarmonyPatch(typeof(DoorsSystemType), nameof(DoorsSystemType.Deteriorate))]
    [HarmonyPrefix]
    public static bool ManualDeterioratePrefix(DoorsSystemType __instance, float deltaTime)
    {
        var timers = __instance.timers;
        var rooms = new List<SystemTypes>();
        foreach (var pair in timers)
            rooms.Add(pair.Key);

        foreach (var room in rooms)
            timers[room] = Math.Clamp(timers[room] - deltaTime, 0f, Cooldown);

        if (__instance.initialCooldown > 0f) __instance.initialCooldown -= deltaTime;

        return false;
    }

    [HarmonyPatch(typeof(DoorsSystemType), nameof(DoorsSystemType.GetTimer))]
    [HarmonyPrefix]
    public static bool ManualGetTimerPrefix(DoorsSystemType __instance, SystemTypes room, ref float __result)
    {
        if (__instance.initialCooldown > 0f)
            __result = __instance.initialCooldown / InitialCooldown;
        else
            __result = __instance.timers.TryGetValue(room, out var timer) ? timer / Cooldown : 0f;

        return false;
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Mushrooms of the doors of The Fungle
    // ---------------------------------------------------------------------------------------------------------------

    [HarmonyPatch(typeof(MushroomDoorSabotageMinigame), nameof(MushroomDoorSabotageMinigame.SetCounterText))]
    [HarmonyPrefix]
    public static bool MushroomCounterPrefix(MushroomDoorSabotageMinigame __instance, int whackedCount)
    {
        __instance.counterText.text = $"{whackedCount}/{(int)Options.Mushrooms.Value}";
        return false;
    }

    [HarmonyPatch(typeof(MushroomDoorSabotageMinigame), nameof(MushroomDoorSabotageMinigame.UpdateMushroomWhackCount))]
    [HarmonyPrefix]
    public static bool MushroomWhackPrefix(MushroomDoorSabotageMinigame __instance, MushroomDoorSabotageMinigameMushroom mushroom)
    {
        __instance.spawnPointBag.Add(mushroom.transform.parent);
        __instance.mushroomWhackCount++;
        __instance.SetCounterText(__instance.mushroomWhackCount);
        if (__instance.mushroomWhackCount >= (int)Options.Mushrooms.Value)
            __instance.FixDoorAndCloseMinigame();

        return false;
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Doors and sabotages
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>
    ///     Doors of the sabotage map usable during a sabotage or not (the game: only on Polus).
    /// </summary>
    [HarmonyPatch(typeof(InfectedOverlay), nameof(InfectedOverlay.CanUseDoors), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool CanUseDoorsPrefix(InfectedOverlay __instance, ref bool __result)
    {
        __result = DoorsDuringSabotage || __instance.sabSystem == null || !__instance.sabSystem.AnyActive;
        return false;
    }

    /// <summary>
    ///     Doors that stop the sabotages while closed, given to the sabotage map (none: the sabotages are not stopped).
    /// </summary>
    /// <returns>The doors, or null.</returns>
    public static IActivatable? SabotageStopper() => DoorsStopSabotages ? new BorClosedDoors().Cast<IActivatable>() : null;
}

/// <summary>
///     Active while a door of the map is closed (as AutoDoorsSystemType of the game, for any doors), the doors of the
///     decontaminations left out: given to the sabotage map to stop the sabotages (<see cref="BorDoorRules"/>).
/// </summary>
[RegisterInIl2Cpp(typeof(IActivatable))]
[SuppressMessage("Performance", "CA1822", Justification = "Member of an interface of the game: instance member for Il2Cpp.")]
[SuppressMessage("Minor Code Smell", "S2325", Justification = "Member of an interface of the game: instance member for Il2Cpp.")]
public sealed class BorClosedDoors(nint cppPtr) : Il2CppSystem.Object(cppPtr)
{
    public BorClosedDoors()
        : this(ClassInjector.DerivedConstructorPointer<BorClosedDoors>())
    {
        ClassInjector.DerivedConstructorBody(this);
    }

    public bool IsActive => ShipStatus.Instance && ShipStatus.Instance.AllDoors.Any(x => x && !x.IsOpen && BorDoors.IsRoomDoor(x));
}
