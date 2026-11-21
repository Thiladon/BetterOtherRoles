using System.Diagnostics.CodeAnalysis;
using Hazel;
using Il2CppInterop.Runtime.Injection;
using Reactor.Utilities.Attributes;

namespace BetterOtherRoles.Modules.Doors;

/// <summary>
///     Doors that open by themselves (as The Skeld) on another map (TOU-Mira 1.7.9 SkeldDoorsSystemType): a copy of
///     AutoDoorsSystemType of the game under its own id, the servers expecting the systems of each map unchanged.
/// </summary>
[RegisterInIl2Cpp(typeof(ISystemType), typeof(IActivatable), typeof(RunTimer), typeof(IDoorSystem))]
[SuppressMessage("Performance", "CA1822", Justification = "Members of the interfaces of the game: instance members for Il2Cpp.")]
[SuppressMessage("Minor Code Smell", "S2325", Justification = "Members of the interfaces of the game: instance members for Il2Cpp.")]
public sealed class BorAutoDoorsSystem(nint cppPtr) : Il2CppSystem.Object(cppPtr)
{
    /// <summary>
    ///     Id of the system (after the ones of the game).
    /// </summary>
    public const SystemTypes SystemType = (SystemTypes)151;

    private uint _dirtyBits;
    private float _initialCooldown;

    public BorAutoDoorsSystem()
        : this(ClassInjector.DerivedConstructorPointer<BorAutoDoorsSystem>())
    {
        ClassInjector.DerivedConstructorBody(this);
    }

    // The doors of the decontaminations (closed most of the time) do not stop the sabotages.
    public bool IsActive => ShipStatus.Instance.AllDoors.Any(x => !x.IsOpen && BorDoors.IsRoomDoor(x));

    public bool IsDirty => _dirtyBits > 0U;

    public void Deteriorate(float deltaTime)
    {
        var doors = ShipStatus.Instance.AllDoors;
        for (var i = 0; i < doors.Length; i++)
        {
            if (doors[i].DoUpdate(deltaTime)) _dirtyBits |= 1U << i;
        }

        if (_initialCooldown > 0f) _initialCooldown -= deltaTime;
    }

    public void UpdateSystem(PlayerControl player, MessageReader msgReader)
    {
        // Not used by the game for these doors (AutoDoorsSystemType): they are closed with CloseDoorsOfType.
    }

    public void MarkClean() => _dirtyBits = 0U;

    public void Serialize(MessageWriter writer, bool initialState)
    {
        var doors = ShipStatus.Instance.AllDoors;
        if (initialState)
        {
            foreach (var door in doors)
                door.Serialize(writer);

            return;
        }

        writer.WritePacked(_dirtyBits);
        for (var i = 0; i < doors.Length; i++)
        {
            if ((_dirtyBits & (1U << i)) != 0U) doors[i].Serialize(writer);
        }
    }

    public void Deserialize(MessageReader reader, bool initialState)
    {
        var doors = ShipStatus.Instance.AllDoors;
        if (initialState)
        {
            foreach (var door in doors)
                door.Deserialize(reader);

            return;
        }

        var before = BorDoorRules.OpenStates();
        var bits = reader.ReadPackedUInt32();
        for (var i = 0; i < doors.Length; i++)
        {
            if ((bits & (1U << i)) != 0U) doors[i].Deserialize(reader);
        }

        BorDoorRules.ApplyNewlyClosed(before);
    }

    public void CloseDoorsOfType(SystemTypes room)
    {
        var doors = ShipStatus.Instance.AllDoors;
        for (var i = 0; i < doors.Length; i++)
        {
            if (doors[i].Room != room || !BorDoors.IsRoomDoor(doors[i])) continue;

            doors[i].SetDoorway(false);
            BorDoorRules.ApplyClosed(doors[i]);
            _dirtyBits |= 1U << i;
        }
    }

    public float GetTimer(SystemTypes system)
    {
        if (_initialCooldown > 0f) return _initialCooldown / BorDoorRules.InitialCooldown;

        foreach (var door in ShipStatus.Instance.AllDoors)
        {
            if (door.Room == system && BorDoors.IsRoomDoor(door)) return door.Cast<AutoOpenDoor>().CooldownTimer / BorDoorRules.Cooldown;
        }

        return 0f;
    }

    public void SetInitialSabotageCooldown() => _initialCooldown = BorDoorRules.InitialCooldown;

    /// <summary>
    ///     Ends the cooldowns (Debug menu).
    /// </summary>
    public void ResetCooldowns()
    {
        _initialCooldown = 0f;
        foreach (var door in ShipStatus.Instance.AllDoors)
        {
            var autoDoor = door.TryCast<AutoOpenDoor>();
            if (autoDoor) autoDoor!.CooldownTimer = 0f;
        }
    }
}
