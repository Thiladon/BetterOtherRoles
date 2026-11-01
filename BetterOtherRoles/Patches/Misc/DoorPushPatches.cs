using HarmonyLib;
using Reactor.Networking.Attributes;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Doors closing on the local player (The Skeld, dlekS): the player stayed stuck between the door and its shadow
///     collider ("Shadow (Horizontal)"), which keeps them away from the door so that their vision does not go through
///     it (ignoring it let them see the other side). When a door closes, a player touching the door or its shadow is
///     pushed out of both on their side (up / down for a horizontal door, left / right for a vertical one), their feet
///     (collider) and their center (vision) both off them, sliding for a short time through a velocity, as a walk, the
///     door and its shadow not blocking them meanwhile. The other players get the push (<see cref="RpcDoorPush"/>) and
///     play the same slide, their network movement of this player paused meanwhile (the game sends the moves too
///     rarely: the slide looked like a jump). Checked at each physics update of the local player, pushed only when the
///     door closes (no jump while walking against a closed door).
/// </summary>
[HarmonyPatch]
public static class DoorPushPatches
{
    // Space left between the player and the door once pushed out.
    private const float Margin = 0.05f;

    // Distance to the door from which a player is pushed when it closes.
    private const float NearDistance = 0.1f;

    // Duration of the push (ease out).
    private const float PushDuration = 0.1f;

    // Last known state of the doors (open), by door instance.
    private static readonly Dictionary<int, bool> DoorsOpen = [];

    // Pushes in progress, by player id.
    private static readonly Dictionary<byte, Push> Pushes = [];

    [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.FixedUpdate))]
    [HarmonyPostfix]
    public static void FixedUpdatePostfix(PlayerPhysics __instance)
    {
        var player = __instance.myPlayer;
        if (!player || !player.Collider)
        {
            return;
        }

        if (Pushes.TryGetValue(player.PlayerId, out var push))
        {
            ContinuePush(player, push);
            return;
        }

        if (!player.AmOwner || !ShipStatus.Instance || ShipStatus.Instance.AllDoors == null)
        {
            return;
        }

        // Every door state is updated at each update, the push is for the first door closing on the player.
        var body = player.Collider.bounds;
        Bounds? pushedBy = null;
        foreach (var openable in ShipStatus.Instance.AllDoors)
        {
            var door = openable ? openable.TryCast<PlainDoor>() : null;
            if (!door)
            {
                continue;
            }

            var open = door!.IsOpen;
            var wasOpen = DoorsOpen.TryGetValue(door.GetInstanceID(), out var known) ? known : open;
            DoorsOpen[door.GetInstanceID()] = open;
            if (pushedBy == null && wasOpen && !open && player.Data != null && !player.Data.IsDead && !player.inVent &&
                !player.onLadder && door.myCollider && IsNear(GetDoorBounds(door), body, player.transform.position))
            {
                pushedBy = GetDoorBounds(door);
            }
        }

        if (pushedBy != null)
        {
            PushOut(player, pushedBy.Value, body);
        }
    }

    // A new ship: the doors are new.
    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Awake))]
    [HarmonyPostfix]
    public static void ShipAwakePostfix()
    {
        DoorsOpen.Clear();
        Pushes.Clear();
    }

    // The door and its shadow collider together.
    private static Bounds GetDoorBounds(PlainDoor door)
    {
        var bounds = door.myCollider.bounds;
        if (door.shadowCollider)
        {
            bounds.Encapsulate(door.shadowCollider.bounds);
        }

        return bounds;
    }

    // Feet touching the door (the game may already have moved them a little), or center (vision) in it.
    // Computed in 2D by hand: the Bounds methods (Intersects, Contains) gave doors far from the player.
    private static bool IsNear(Bounds door, Bounds body, Vector3 center)
    {
        var touching = body.min.x <= door.max.x + NearDistance && body.max.x >= door.min.x - NearDistance &&
                       body.min.y <= door.max.y + NearDistance && body.max.y >= door.min.y - NearDistance;
        var inside = center.x >= door.min.x && center.x <= door.max.x && center.y >= door.min.y && center.y <= door.max.y;
        return touching || inside;
    }

    private static void PushOut(PlayerControl player, Bounds area, Bounds body)
    {
        var center = (Vector2)player.transform.position;
        var move = Vector2.zero;
        if (area.size.x >= area.size.y)
        {
            // Above: feet and center over the door; below: feet and center under it.
            move.y = body.center.y >= area.center.y
                ? Mathf.Max(area.max.y + Margin - body.min.y, area.max.y + Margin - center.y, 0f)
                : Mathf.Min(area.min.y - Margin - body.max.y, area.min.y - Margin - center.y, 0f);
        }
        else
        {
            move.x = body.center.x >= area.center.x
                ? Mathf.Max(area.max.x + Margin - body.min.x, area.max.x + Margin - center.x, 0f)
                : Mathf.Min(area.min.x - Margin - body.max.x, area.min.x - Margin - center.x, 0f);
        }

        if (move.sqrMagnitude < 0.0001f)
        {
            return;
        }

        var position = center + move;
        RpcDoorPush(player, position);
    }

    /// <summary>
    ///     Plays the push of a player, sent by this player to everyone (them included).
    /// </summary>
    [MethodRpc((uint)BorRpc.DoorPush)]
    public static void RpcDoorPush(PlayerControl player, Vector2 to)
    {
        if (!player || !player.Collider || !player.MyPhysics)
        {
            return;
        }

        var push = new Push(player.MyPhysics.body.position, to);
        if (player.AmOwner)
        {
            // The door and its shadow do not stop the slide.
            var area = new Bounds(new Vector3((push.From.x + to.x) / 2f, (push.From.y + to.y) / 2f, 0f), Vector3.zero);
            area.Encapsulate(player.Collider.bounds);
            area.Encapsulate(new Vector3(to.x, to.y, 0f));
            foreach (var door in ShipStatus.Instance && ShipStatus.Instance.AllDoors != null ? ShipStatus.Instance.AllDoors : new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<OpenableDoor>(0))
            {
                var plain = door ? door.TryCast<PlainDoor>() : null;
                if (plain && plain!.myCollider && Overlaps(GetDoorBounds(plain), area))
                {
                    push.Ignored.AddRange(new[] { plain.myCollider, plain.shadowCollider }.Where(x => x));
                }
            }

            foreach (var collider in push.Ignored)
            {
                Physics2D.IgnoreCollision(player.Collider, collider, true);
            }
        }
        else if (player.NetTransform)
        {
            player.NetTransform.isPaused = true;
            player.Collider.enabled = false;
        }

        Pushes[player.PlayerId] = push;
        ContinuePush(player, push);
    }

    private static bool Overlaps(Bounds a, Bounds b)
    {
        return a.min.x <= b.max.x && a.max.x >= b.min.x && a.min.y <= b.max.y && a.max.y >= b.min.y;
    }

    // Velocity toward the next place of the slide (ease out), after the game set the walk velocity of this update.
    private static void ContinuePush(PlayerControl player, Push push)
    {
        var body = player.MyPhysics.body;
        push.Time += Time.fixedDeltaTime;
        if (push.Time > PushDuration + Time.fixedDeltaTime)
        {
            body.velocity = Vector2.zero;
            Pushes.Remove(player.PlayerId);
            if (player.AmOwner)
            {
                foreach (var collider in push.Ignored.Where(x => x))
                {
                    Physics2D.IgnoreCollision(player.Collider, collider, false);
                }
            }
            else if (player.NetTransform)
            {
                // The moves received meanwhile are dropped: the player is where the slide ended.
                player.NetTransform.SnapTo(push.To);
                player.NetTransform.isPaused = false;
                player.Collider.enabled = true;
            }

            return;
        }

        var t = 1f - Mathf.Pow(1f - Mathf.Clamp01(push.Time / PushDuration), 2f);
        body.velocity = (Vector2.Lerp(push.From, push.To, t) - body.position) / Time.fixedDeltaTime;
    }

    private sealed class Push(Vector2 from, Vector2 to)
    {
        public Vector2 From { get; } = from;

        public Vector2 To { get; } = to;

        public float Time { get; set; }

        public List<Collider2D> Ignored { get; } = [];
    }
}
