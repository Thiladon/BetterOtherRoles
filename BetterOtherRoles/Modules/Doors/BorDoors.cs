using System.Collections;
using BetterOtherRoles.Options.Maps;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MiraAPI.GameOptions;
using PowerTools;
using Reactor.Networking.Attributes;
using Reactor.Utilities;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Modules.Doors;

/// <summary>
///     Doors of each map (<see cref="DoorOptions"/>, TOU-Mira 1.7.9 MapDoorPatches), changed on every player when the
///     map is made:
///     - none: the doors and their buttons on the sabotage map are removed;
///     - The Skeld: they open by themselves (<see cref="BorAutoDoorsSystem"/>, <see cref="BorAutoMushroomDoor"/>);
///     - Polus, Airship, The Fungle: opened with the minigame of that map (<see cref="BorManualDoorsSystem"/> on The
///       Skeld), loaded from its prefab.
///     The minigames ask the game system of the doors: sent to the system of the map instead.
/// </summary>
[HarmonyPatch]
public static class BorDoors
{
    // Prefabs of the maps whose minigames are used, released with the map.
    private static readonly List<AsyncOperationHandle<GameObject>> Handles = [];

    // Map already changed (OnEnable of the map and of its class).
    private static int _changedShip;

    // Doors of the decontaminations of the map, left as they are (DoorsOfMap).
    private static readonly HashSet<nint> DeconDoors = [];

    /// <summary>
    ///     Gets the doors used in this game (random drawn), or null for a map without doors.
    /// </summary>
    public static BorDoorType? CurrentType { get; private set; }

    /// <summary>
    ///     Gets the doors a map has in the game, or null for a map without doors.
    /// </summary>
    /// <param name="map">The map.</param>
    /// <returns>Its doors.</returns>
    public static BorDoorType? NativeType(MapNames map) => map switch
    {
        MapNames.Skeld or MapNames.Dleks => BorDoorType.Skeld,
        MapNames.Polus => BorDoorType.Polus,
        MapNames.Airship => BorDoorType.Airship,
        MapNames.Fungle => BorDoorType.Fungle,
        _ => null,
    };

    /// <summary>
    ///     The systems of BetterOtherRoles synced with the ones of the game (each system of the list is sent).
    /// </summary>
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.Awake))]
    [HarmonyPostfix]
    public static void ClientAwakePostfix()
    {
        if (SystemTypeHelpers.AllTypes.Contains(BorAutoDoorsSystem.SystemType)) return;

        SystemTypeHelpers.AllTypes = SystemTypeHelpers.AllTypes
            .Concat([BorAutoDoorsSystem.SystemType, BorManualDoorsSystem.SystemType])
            .ToArray();
    }

    // The Skeld, dlekS, MIRA HQ.
    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.OnEnable))]
    [HarmonyPostfix]
    public static void ShipEnablePostfix(ShipStatus __instance) => Change(__instance);

    [HarmonyPatch(typeof(PolusShipStatus), nameof(PolusShipStatus.OnEnable))]
    [HarmonyPostfix]
    public static void PolusEnablePostfix(PolusShipStatus __instance) => Change(__instance);

    [HarmonyPatch(typeof(AirshipStatus), nameof(AirshipStatus.OnEnable))]
    [HarmonyPostfix]
    public static void AirshipEnablePostfix(AirshipStatus __instance) => Change(__instance);

    [HarmonyPatch(typeof(FungleShipStatus), nameof(FungleShipStatus.OnEnable))]
    [HarmonyPostfix]
    public static void FungleEnablePostfix(FungleShipStatus __instance) => Change(__instance);

    /// <summary>
    ///     The map destroyed (end of the game): prefabs released, doors drawn forgotten.
    /// </summary>
    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.OnDestroy))]
    [HarmonyPostfix]
    public static void ShipDestroyPostfix()
    {
        foreach (var handle in Handles)
            Addressables.Release(handle);

        Handles.Clear();
        _changedShip = 0;
        CurrentType = null;
        DeconDoors.Clear();
        BorRandomDoors.Clear();
    }

    /// <summary>
    ///     Doors closed by the system of the map (the game only knows its own).
    /// </summary>
    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CloseDoorsOfType))]
    [HarmonyPrefix]
    public static bool CloseDoorsOfTypePrefix(ShipStatus __instance, SystemTypes room)
    {
        if (Own(__instance) is not { } system) return true;

        system.Cast<IDoorSystem>().CloseDoorsOfType(room);
        return false;
    }

    /// <summary>
    ///     A door opened by its minigame on The Skeld: sent to the system of the map (the server expects no door
    ///     message there from the game).
    /// </summary>
    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.RpcUpdateSystem), typeof(SystemTypes), typeof(byte))]
    [HarmonyPrefix]
    public static bool RpcUpdateSystemPrefix(ShipStatus __instance, SystemTypes systemType, byte amount)
    {
        if (systemType != SystemTypes.Doors || !__instance.Systems.ContainsKey(BorManualDoorsSystem.SystemType)) return true;

        RpcOpenDoor(PlayerControl.LocalPlayer, amount);
        return false;
    }

    /// <summary>
    ///     Cooldown of the doors on the sabotage map, from the system of the map.
    /// </summary>
    [HarmonyPatch(typeof(MapRoom), nameof(MapRoom.DoorsUpdate))]
    [HarmonyPrefix]
    public static bool DoorsUpdatePrefix(MapRoom __instance)
    {
        if (!__instance.door || !ShipStatus.Instance) return true;

        var system = DoorSystem(ShipStatus.Instance);
        if (system == null) return false;

        var timer = system.Cast<RunTimer>().GetTimer(__instance.room);
        __instance.door.material.SetFloat("_Percent", __instance.Parent.CanUseDoors ? timer : 1f);
        return false;
    }

    /// <summary>
    ///     Doors of a room closed from the sabotage map, with the system of the map (the game only knows its own), and
    ///     the rule of BetterOtherRoles for the doors during a sabotage (<see cref="BorDoorRules"/>).
    /// </summary>
    [HarmonyPatch(typeof(MapRoom), nameof(MapRoom.SabotageDoors))]
    [HarmonyPrefix]
    public static bool SabotageDoorsPrefix(MapRoom __instance)
    {
        if (!ShipStatus.Instance || DoorSystem(ShipStatus.Instance) is not { } system) return true;

        if (!__instance.Parent.CanUseDoors || system.Cast<RunTimer>().GetTimer(__instance.room) > 0f) return false;

        ShipStatus.Instance.RpcCloseDoorsOfType(__instance.room);
        return false;
    }

    /// <summary>
    ///     Closed doors stop the sabotages or not, with the rule of BetterOtherRoles (<see cref="BorDoorRules"/>).
    /// </summary>
    [HarmonyPatch(typeof(InfectedOverlay), nameof(InfectedOverlay.Start))]
    [HarmonyPostfix]
    public static void OverlayStartPostfix(InfectedOverlay __instance)
    {
        if (ShipStatus.Instance) __instance.doors = BorDoorRules.SabotageStopper();
    }

    /// <summary>
    ///     No doors: their buttons on the sabotage map are removed.
    /// </summary>
    [HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.ShowSabotageMap))]
    [HarmonyPostfix]
    public static void ShowSabotageMapPostfix(MapBehaviour __instance)
    {
        var overlay = __instance.infectedOverlay;
        if (!ShipStatus.Instance || DoorSystem(ShipStatus.Instance) != null || !overlay) return;

        var removed = false;
        foreach (var room in overlay!.rooms)
        {
            if (!room || !room.door) continue;

            Object.DestroyImmediate(room.door!.gameObject);
            room.door = null;
            removed = true;
        }

        if (!removed) return;

        // Buttons reached with a controller, without the removed ones.
        overlay.allButtons = (Il2CppReferenceArray<ButtonBehavior>)overlay.GetComponentsInChildren<ButtonBehavior>(true);
        if (!overlay.selectedButton) overlay.selectedButton = null;
    }

    /// <summary>
    ///     Cooldown of the doors at the start of the game, as the game does for its own system (HudManager.CoShowIntro).
    /// </summary>
    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.OnDestroy))]
    [HarmonyPostfix]
    public static void IntroDestroyPostfix()
    {
        if (ShipStatus.Instance && Own(ShipStatus.Instance) is { } system)
            system.Cast<IDoorSystem>().SetInitialSabotageCooldown();
    }

    /// <summary>
    ///     Ends the cooldowns of the doors of BetterOtherRoles (Debug menu).
    /// </summary>
    /// <returns>Whether the map has them.</returns>
    public static bool ResetCooldowns()
    {
        if (!ShipStatus.Instance || Own(ShipStatus.Instance) is not { } system) return false;

        system.TryCast<BorAutoDoorsSystem>()?.ResetCooldowns();
        system.TryCast<BorManualDoorsSystem>()?.ResetCooldowns();
        return true;
    }

    // System of the doors of BetterOtherRoles on the map, if any.
    private static ISystemType? Own(ShipStatus ship)
    {
        if (ship.Systems == null) return null;

        if (ship.Systems.TryGetValue(BorAutoDoorsSystem.SystemType, out var auto)) return auto;

        return ship.Systems.TryGetValue(BorManualDoorsSystem.SystemType, out var manual) ? manual : null;
    }

    // System of the doors on the map (of the game or of BetterOtherRoles), if any.
    private static ISystemType? DoorSystem(ShipStatus ship)
    {
        if (ship.Systems != null && ship.Systems.TryGetValue(SystemTypes.Doors, out var system)) return system;

        return Own(ship);
    }

    // The host opens the door with the system of the map (sent to everyone with it); the others open it at once.
    [MethodRpc((uint)BorRpc.DoorOpen)]
    private static void RpcOpenDoor(PlayerControl player, byte amount)
    {
        if (!ShipStatus.Instance || !player) return;

        if (AmongUsClient.Instance.AmHost)
            ShipStatus.Instance.UpdateSystem(BorManualDoorsSystem.SystemType, player, amount);
        else if (ShipStatus.Instance.Systems.ContainsKey(BorManualDoorsSystem.SystemType))
            BorManualDoorsSystem.Open(amount & 31);
    }

    private static void Change(ShipStatus ship)
    {
        if (!ship || ship.Systems == null || ship.GetInstanceID() == _changedShip || GameOptionsManager.Instance?.CurrentGameOptions == null) return;

        _changedShip = ship.GetInstanceID();
        CurrentType = null;
        FindDeconDoors(ship);
        var map = (MapNames)GameOptionsManager.Instance.CurrentGameOptions.MapId;
        if (NativeType(map) is not { } native) return;

        var type = OptionGroupSingleton<DoorOptions>.Instance.Get(map) ?? native;
        if (type == BorDoorType.Random)
            type = TutorialManager.InstanceExists ? BorRandomDoors.Roll(native) : BorRandomDoors.Drawn ?? native;

        CurrentType = type;
        if (type == native) return;

        try
        {
            if (type == BorDoorType.None)
                RemoveDoors(ship);
            else if (native == BorDoorType.Skeld)
                ToManual(ship, type);
            else if (type == BorDoorType.Skeld)
                ToAuto(ship, native);
            else
                Coroutines.Start(CoSetMinigames(ship, type));
        }
        catch (Exception e)
        {
            Error($"Doors: could not change the doors of {map}: {e}");
        }
    }

    // Every door of the map removed, with its system (not the ones of the decontaminations).
    private static void RemoveDoors(ShipStatus ship)
    {
        var kept = new List<OpenableDoor>();
        foreach (var door in DoorsOfMap<OpenableDoor>(ship, kept))
            Object.Destroy(door.gameObject);

        ship.AllDoors = kept.ToArray();
        ship.Systems.Remove(SystemTypes.Doors);
    }

    // Doors of The Skeld opened with a minigame (PlainDoor and its console).
    private static void ToManual(ShipStatus ship, BorDoorType type)
    {
        var doors = new List<OpenableDoor>();
        var kept = new List<OpenableDoor>();
        foreach (var autoDoor in DoorsOfMap<AutoOpenDoor>(ship, kept))
        {
            var door = autoDoor.gameObject;
            var animator = door.GetComponent<SpriteAnim>();
            var closeDoorAnim = autoDoor.CloseDoorAnim;
            var closeSound = autoDoor.CloseSound;
            var myCollider = autoDoor.myCollider;
            var openDoorAnim = autoDoor.OpenDoorAnim;
            var openSound = autoDoor.OpenSound;
            var shadowCollider = autoDoor.shadowCollider;
            var id = autoDoor.Id;
            var room = autoDoor.Room;
            Object.DestroyImmediate(autoDoor);

            var plainDoor = door.AddComponent<PlainDoor>();
            plainDoor.animator = animator;
            plainDoor.CloseDoorAnim = closeDoorAnim;
            plainDoor.CloseSound = closeSound;
            plainDoor.myCollider = myCollider;
            plainDoor.OpenDoorAnim = openDoorAnim;
            plainDoor.OpenSound = openSound;
            plainDoor.shadowCollider = shadowCollider;
            plainDoor.Id = id;
            plainDoor.Room = room;
            var size = myCollider.size;
            plainDoor.size = size.x > size.y ? size.y : size.x;
            plainDoor.Open = myCollider.isTrigger;
            plainDoor.animator.Play(plainDoor.Open ? plainDoor.OpenDoorAnim : plainDoor.CloseDoorAnim, 1000f);
            plainDoor.UpdateShadow();

            var console = door.AddComponent<DoorConsole>();
            console.MyDoor = plainDoor;
            doors.Add(plainDoor);
        }

        ship.AllDoors = doors.Concat(kept).ToArray();
        ship.Systems.Remove(SystemTypes.Doors);
        ship.Systems.Add(BorManualDoorsSystem.SystemType, new BorManualDoorsSystem().Cast<ISystemType>());
        Coroutines.Start(CoSetMinigames(ship, type));
    }

    // Doors of Polus, the Airship or The Fungle that open by themselves (consoles removed).
    private static void ToAuto(ShipStatus ship, BorDoorType native)
    {
        var doors = new List<OpenableDoor>();
        var kept = new List<OpenableDoor>();
        if (native == BorDoorType.Fungle)
        {
            foreach (var wall in DoorsOfMap<MushroomWallDoor>(ship, kept))
                doors.Add(ToAutoMushroom(wall));
        }
        else
        {
            foreach (var plainDoor in DoorsOfMap<PlainDoor>(ship, kept))
                doors.Add(ToAutoPlain(plainDoor));
        }

        ship.AllDoors = doors.Concat(kept).ToArray();
        ship.Systems.Remove(SystemTypes.Doors);
        ship.Systems.Add(BorAutoDoorsSystem.SystemType, new BorAutoDoorsSystem().Cast<ISystemType>());
    }

    /// <summary>
    ///     Gets whether a door of the map closes a room (not a door of a decontamination, opened and closed by its own
    ///     system and closed most of the time).
    /// </summary>
    /// <param name="door">A door of AllDoors.</param>
    /// <returns>Whether it closes a room.</returns>
    public static bool IsRoomDoor(OpenableDoor door) => !DeconDoors.Contains(door.Pointer);

    // The doors of the decontaminations (Polus) are in the door list of the map (AllDoors) too.
    private static void FindDeconDoors(ShipStatus ship)
    {
        DeconDoors.Clear();
        foreach (var decon in ship.GetComponentsInChildren<DeconSystem>(true))
        {
            if (decon.UpperDoor) DeconDoors.Add(decon.UpperDoor.Pointer);
            if (decon.LowerDoor) DeconDoors.Add(decon.LowerDoor.Pointer);
        }
    }

    // Doors of the map of this type, from its door list (AllDoors). The doors of the decontaminations are kept as they
    // are, in the list (sent with the others).
    private static List<T> DoorsOfMap<T>(ShipStatus ship, List<OpenableDoor> kept)
        where T : OpenableDoor
    {
        var doors = new List<T>();
        foreach (var door in ship.AllDoors)
        {
            if (!door) continue;

            var typed = IsRoomDoor(door) ? door.TryCast<T>() : null;
            if (typed)
            {
                doors.Add(typed!);
                continue;
            }

            kept.Add(door);
            if (IsRoomDoor(door)) Warning($"Doors: door {door.name} of the map is not a {typeof(T).Name}, kept as it is.");
        }

        return doors;
    }

    private static AutoOpenDoor ToAutoPlain(PlainDoor plainDoor)
    {
        var door = plainDoor.gameObject;
        var animator = door.GetComponent<SpriteAnim>();
        var closeDoorAnim = plainDoor.CloseDoorAnim;
        var closeSound = plainDoor.CloseSound;
        var myCollider = plainDoor.myCollider;
        var openDoorAnim = plainDoor.OpenDoorAnim;
        var openSound = plainDoor.OpenSound;
        var shadowCollider = plainDoor.shadowCollider;
        var id = plainDoor.Id;
        var size = plainDoor.size;
        var room = plainDoor.Room;
        var console = door.GetComponent<DoorConsole>();
        if (console) Object.DestroyImmediate(console);
        Object.DestroyImmediate(plainDoor);

        var autoDoor = door.AddComponent<AutoOpenDoor>();
        autoDoor.animator = animator;
        autoDoor.CloseDoorAnim = closeDoorAnim;
        autoDoor.CloseSound = closeSound;
        autoDoor.myCollider = myCollider;
        autoDoor.OpenDoorAnim = openDoorAnim;
        autoDoor.OpenSound = openSound;
        autoDoor.shadowCollider = shadowCollider;
        autoDoor.Id = id;
        autoDoor.size = size;
        autoDoor.Room = room;

        // As PlainDoor.Start: the map is made before the doors start (their Open field not set yet).
        autoDoor.Open = myCollider.isTrigger;
        return autoDoor;
    }

    private static BorAutoMushroomDoor ToAutoMushroom(MushroomWallDoor wall)
    {
        var door = wall.gameObject;
        var openSound = wall.openSound;
        var closeSound = wall.closeSound;
        var wallCollider = wall.wallCollider;
        var shadowColl = wall.shadowColl;
        var bottomColl = wall.bottomColl;
        var mushrooms = wall.mushrooms.ToArray();
        var id = wall.Id;
        var room = wall.Room;
        var console = door.GetComponent<DoorConsole>();
        if (console) Object.DestroyImmediate(console);
        Object.DestroyImmediate(wall);

        var autoDoor = door.AddComponent<BorAutoMushroomDoor>();
        autoDoor.openSound = openSound;
        autoDoor.closeSound = closeSound;
        autoDoor.wallCollider = wallCollider;
        autoDoor.shadowColl = shadowColl;
        autoDoor.bottomColl = bottomColl;
        autoDoor.mushrooms = mushrooms;
        autoDoor.Id = id;
        autoDoor.Room = room;
        autoDoor.SetDoorway(true);
        return autoDoor;
    }

    // Minigame of the doors of another map, from its prefab (loaded while the intro plays).
    private static IEnumerator CoSetMinigames(ShipStatus ship, BorDoorType type)
    {
        var source = type switch
        {
            BorDoorType.Polus => MapNames.Polus,
            BorDoorType.Airship => MapNames.Airship,
            _ => MapNames.Fungle,
        };

        var handle = Addressables.LoadAssetAsync<GameObject>(AmongUsClient.Instance.ShipPrefabs[(int)source].RuntimeKey);
        Handles.Add(handle);
        while (!handle.IsDone) yield return null;

        if (!ship || ShipStatus.Instance != ship || handle.Status != AsyncOperationStatus.Succeeded) yield break;

        var minigame = handle.Result.GetComponentInChildren<DoorConsole>(true)?.MinigamePrefab;
        if (!minigame)
        {
            Warning($"Doors: no minigame found on {source}.");
            yield break;
        }

        foreach (var console in ship.GetComponentsInChildren<DoorConsole>(true))
            console.MinigamePrefab = minigame;
    }
}
