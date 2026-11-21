using Hazel;
using Reactor.Utilities.Attributes;
using UnityEngine;

namespace BetterOtherRoles.Modules.Doors;

/// <summary>
///     Mushroom wall of The Fungle that opens by itself (as the doors of The Skeld, TOU-Mira 1.7.9 AutoOpenMushroomDoor):
///     closed for a while, then a cooldown before it can be closed again (<see cref="BorDoorRules"/>).
/// </summary>
[RegisterInIl2Cpp]
public sealed class BorAutoMushroomDoor(nint cppPtr) : AutoOpenDoor(cppPtr)
{
    // Distance of the sounds (MushroomWallDoor).
    private const float SoundMin = 7f;
    private const float SoundMax = 50f;

    public BoxCollider2D? wallCollider;

    public Collider2D? shadowColl;

    public Collider2D? bottomColl;

    public MushroomWallMushroom[] mushrooms = [];

    public AudioClip? openSound;

    public AudioClip? closeSound;

    private bool _open;

    public override bool IsOpen => _open;

    public override bool DoUpdate(float dt)
    {
        CooldownTimer = Math.Max(CooldownTimer - dt, 0f);
        if (ClosedTimer <= 0f) return false;

        ClosedTimer = Math.Max(ClosedTimer - dt, 0f);
        if (ClosedTimer > 0f) return false;

        SetDoorway(true);
        return true;
    }

    public override void SetDoorway(bool open)
    {
        if (!open)
        {
            ClosedTimer = BorDoorRules.ClosedDuration;
            CooldownTimer = BorDoorRules.Cooldown;
        }

        if (_open == open) return;

        _open = open;
        if (wallCollider) wallCollider!.isTrigger = open;
        if (shadowColl) shadowColl!.enabled = !open;
        if (bottomColl) bottomColl!.enabled = !open;
        foreach (var mushroom in mushrooms)
        {
            if (!mushroom) continue;

            if (open) mushroom.Hide();
            else mushroom.Show();
        }

        var sound = open ? openSound : closeSound;
        if (!sound) return;

        if (Constants.ShouldPlaySfx())
        {
            var source = SoundManager.Instance.PlaySound(sound, false, 1f, SoundManager.Instance.SfxChannel);
            if (source && PlayerControl.LocalPlayer)
                source.volume = SoundManager.GetSoundVolume(transform.position, PlayerControl.LocalPlayer.GetTruePosition(), SoundMin, SoundMax, 0.5f);
        }

        VibrationManager.Vibrate(2.5f, transform.position, 3f, 0f, VibrationManager.VibrationFalloff.None, sound, false);
    }

    public override void Serialize(MessageWriter writer) => writer.Write(_open);

    public override void Deserialize(MessageReader reader) => SetDoorway(reader.ReadBoolean());
}
