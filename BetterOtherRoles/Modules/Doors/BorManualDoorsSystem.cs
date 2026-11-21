using System.Diagnostics.CodeAnalysis;
using Hazel;
using Il2CppInterop.Runtime.Injection;
using Reactor.Utilities.Attributes;
using UnityEngine;

namespace BetterOtherRoles.Modules.Doors;

/// <summary>
///     Doors opened with a minigame on The Skeld and dlekS (TOU-Mira 1.7.9 ManualDoorsSystemType): a copy of
///     DoorsSystemType of the game under its own id, the servers expecting the systems of each map unchanged.
/// </summary>
[RegisterInIl2Cpp(typeof(ISystemType), typeof(IActivatable), typeof(RunTimer), typeof(IDoorSystem))]
[SuppressMessage("Performance", "CA1822", Justification = "Members of the interfaces of the game: instance members for Il2Cpp.")]
[SuppressMessage("Minor Code Smell", "S2325", Justification = "Members of the interfaces of the game: instance members for Il2Cpp.")]
public sealed class BorManualDoorsSystem(nint cppPtr) : Il2CppSystem.Object(cppPtr)
{
    /// <summary>
    ///     Id of the system (after the ones of the game).
    /// </summary>
    public const SystemTypes SystemType = (SystemTypes)152;

    // Message of the minigame (DoorsSystemType): id of the door and action.
    private const byte OpenDoor = 64;
    private const byte ActionMask = 192;
    private const byte IdMask = 31;

    private readonly Dictionary<SystemTypes, float> _timers = [];
    private float _initialCooldown;

    public BorManualDoorsSystem()
        : this(ClassInjector.DerivedConstructorPointer<BorManualDoorsSystem>())
    {
        ClassInjector.DerivedConstructorBody(this);
    }

    // As DoorsSystemType: closed doors do not stop the sabotages.
    public bool IsActive => false;

    public bool IsDirty { get; private set; }

    public void Deteriorate(float deltaTime)
    {
        foreach (var room in _timers.Keys.ToList())
            _timers[room] = Mathf.Clamp(_timers[room] - deltaTime, 0f, BorDoorRules.Cooldown);

        if (_initialCooldown > 0f) _initialCooldown -= deltaTime;
    }

    public void UpdateSystem(PlayerControl player, MessageReader msgReader)
    {
        var message = msgReader.ReadByte();
        if ((message & ActionMask) == OpenDoor) Open(message & IdMask);

        IsDirty = true;
    }

    public void MarkClean() => IsDirty = false;

    public void Serialize(MessageWriter writer, bool initialState)
    {
        writer.Write((byte)_timers.Count);
        foreach (var (room, timer) in _timers)
        {
            writer.Write((byte)room);
            writer.Write(timer);
        }

        foreach (var door in ShipStatus.Instance.AllDoors)
            door.Serialize(writer);
    }

    public void Deserialize(MessageReader reader, bool initialState)
    {
        int count = reader.ReadByte();
        for (var i = 0; i < count; i++)
        {
            var room = (SystemTypes)reader.ReadByte();
            _timers[room] = reader.ReadSingle();
        }

        foreach (var door in ShipStatus.Instance.AllDoors)
            door.Deserialize(reader);
    }

    public void CloseDoorsOfType(SystemTypes room)
    {
        _timers[room] = BorDoorRules.Cooldown;
        foreach (var door in ShipStatus.Instance.AllDoors)
        {
            if (door.Room == room) door.SetDoorway(false);
        }

        IsDirty = true;
    }

    public float GetTimer(SystemTypes system)
    {
        if (_initialCooldown > 0f) return _initialCooldown / BorDoorRules.InitialCooldown;

        return _timers.TryGetValue(system, out var timer) ? timer / BorDoorRules.Cooldown : 0f;
    }

    public void SetInitialSabotageCooldown() => _initialCooldown = BorDoorRules.InitialCooldown;

    /// <summary>
    ///     Ends the cooldowns (Debug menu).
    /// </summary>
    public void ResetCooldowns()
    {
        _initialCooldown = 0f;
        foreach (var room in _timers.Keys.ToList())
            _timers[room] = 0f;

        IsDirty = true;
    }

    /// <summary>
    ///     Opens a door (minigame done).
    /// </summary>
    /// <param name="id">Id of the door.</param>
    public static void Open(int id)
    {
        var door = ShipStatus.Instance.AllDoors.FirstOrDefault(x => x.Id == id);
        if (door) door!.SetDoorway(true);
        else Warning($"Doors: door {id} not found.");
    }
}
