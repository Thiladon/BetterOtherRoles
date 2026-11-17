using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace BetterOtherRoles.Utilities;

/// <summary>
///     What the local player sees (names hidden out of sight, vent animations hidden out of sight):
///     <list type="bullet">
///         <item>in sight: a point is in the light and nothing that casts a shadow stands between it and the light, as
///         the shadows of the game (LightSourceGpuRenderer). TOU-Mira 1.7.9 only checks one point against the walls;</item>
///         <item>on the cams watched now: a box in the frame of a cam of the minigame open (the cams have no shadows),
///         none while they show static (comms sabotaged): the four of the Skeld, the one shown on Polus and the
///         Airship, the binoculars of the Fungle.</item>
///     </list>
/// </summary>
public static class BorSight
{
    // Points of a player checked, from its center (world units): feet, center, head, sides.
    private const float HeadHeight = 0.4f;
    private const float SideWidth = 0.25f;

    // Points of a box checked: center, middle of its sides and corners, just inside (a ray to its very edge may stop on
    // the wall it stands against).
    private const float BoxEdge = 0.98f;

    // Outer part of the light (share of its radius) left out: the light fades out there, a point in it is barely seen.
    private const float EdgeFade = 0.03f;

    private static readonly Il2CppStructArray<RaycastHit2D> Hits = new(32);

    // Cams of the minigame open now, found again only when another minigame opens.
    private static readonly List<Camera> Cams = [];
    private static Minigame? _camsMinigame;

    /// <summary>
    ///     Gets whether a part of the player (feet, center, head, sides) is in the sight of the local player.
    /// </summary>
    public static bool InSight(PlayerControl player)
    {
        Vector2 center = player.transform.position;
        return InSight(player.GetTruePosition()) || InSight(center) || InSight(center + Vector2.up * HeadHeight) ||
               InSight(center + Vector2.left * SideWidth) || InSight(center + Vector2.right * SideWidth);
    }

    /// <summary>
    ///     Gets whether a part of a box (center, middle of its sides, corners) is in the sight of the local player.
    /// </summary>
    public static bool InSight(Bounds bounds)
    {
        Vector2 center = bounds.center;
        Vector2 x = Vector2.right * (bounds.extents.x * BoxEdge);
        Vector2 y = Vector2.up * (bounds.extents.y * BoxEdge);
        return InSight(center) || InSight(center + y) || InSight(center - y) || InSight(center - x) || InSight(center + x) ||
               InSight(center - x + y) || InSight(center + x + y) || InSight(center - x - y) || InSight(center + x - y);
    }

    /// <summary>
    ///     Gets whether a point is in the light of the local player, with nothing casting a shadow in between.
    /// </summary>
    public static bool InSight(Vector2 target)
    {
        var local = PlayerControl.LocalPlayer;
        var light = local ? local.lightSource : null;
        if (!light) return false;

        Vector2 origin = light!.transform.position;
        var vector = target - origin;
        var distance = vector.magnitude;
        if (distance > light.ViewDistance * (1f - EdgeFade)) return false;
        if (distance < 0.01f) return true;

        var count = Physics2D.RaycastNonAlloc(origin, vector / distance, Hits, distance, Constants.ShadowMask);
        for (var i = 0; i < count; i++)
        {
            var collider = Hits[i].collider;
            if (collider && CastsShadow(light, collider)) return false;
        }

        return true;
    }

    /// <summary>
    ///     Gets whether two players stand in the same room.
    /// </summary>
    public static bool InSameRoom(PlayerControl player, PlayerControl other) => InSameRoom(player.GetTruePosition(), other.GetTruePosition());

    /// <summary>
    ///     Gets whether two points stand in the same room.
    /// </summary>
    public static bool InSameRoom(Vector2 position, Vector2 otherPosition)
    {
        foreach (var room in ShipStatus.Instance.FastRooms.Values)
        {
            if (room && room.roomArea && room.roomArea.OverlapPoint(position) && room.roomArea.OverlapPoint(otherPosition))
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Gets whether a part of the player (its body, or the points checked in sight) is on a cam watched now.
    /// </summary>
    public static bool OnWatchedCams(PlayerControl player)
    {
        if (!player.Visible) return false;

        var body = player.cosmetics.currentBodySprite?.BodySprite;
        return OnWatchedCams(body ? body!.bounds : new Bounds(player.transform.position, new Vector3(SideWidth * 2f, HeadHeight * 2f, 0f)));
    }

    /// <summary>
    ///     Gets whether a part of a box is on a cam watched now.
    /// </summary>
    public static bool OnWatchedCams(Bounds bounds)
    {
        foreach (var cam in GetWatchedCams())
        {
            if (!cam) continue;

            Vector2 center = cam.transform.position;
            var halfHeight = cam.orthographicSize;
            var halfWidth = halfHeight * cam.aspect;
            if (Mathf.Abs(bounds.center.x - center.x) < halfWidth + bounds.extents.x &&
                Mathf.Abs(bounds.center.y - center.y) < halfHeight + bounds.extents.y)
                return true;
        }

        return false;
    }

    // As LightSourceGpuRenderer: no shadow from triggers, from the hit override of an object without shadow, nor from a
    // one-way shadow seen from its open side.
    private static bool CastsShadow(LightSource light, Collider2D collider)
    {
        if (collider.isTrigger) return false;

        var obj = collider.gameObject;
        if (LightSource.NoShadows.TryGetValue(obj, out var noShadow) && noShadow && noShadow.hitOverride == collider) return false;

        return !(LightSource.OneWayShadows.TryGetValue(obj, out var oneWay) && oneWay && oneWay.IsIgnored(light));
    }

    private static List<Camera> GetWatchedCams()
    {
        var minigame = Minigame.Instance;
        if (minigame != _camsMinigame)
        {
            _camsMinigame = minigame;
            Cams.Clear();
            if (minigame) FindCams(minigame!);
        }

        if (!minigame || Cams.Count == 0) return Cams;

        var skeld = minigame!.TryCast<SurveillanceMinigame>();
        if (skeld && skeld!.isStatic) return [];

        var planet = minigame.TryCast<PlanetSurveillanceMinigame>();
        if (planet && planet!.isStatic) return [];

        return Cams;
    }

    private static void FindCams(Minigame minigame)
    {
        var skeld = minigame.TryCast<SurveillanceMinigame>();
        if (skeld)
        {
            foreach (var cam in skeld!.GetComponentsInChildren<Camera>(true))
            {
                if (cam && cam.targetTexture) Cams.Add(cam);
            }

            return;
        }

        var planet = minigame.TryCast<PlanetSurveillanceMinigame>();
        if (planet)
        {
            if (planet!.Camera) Cams.Add(planet.Camera);
            return;
        }

        var fungle = minigame.TryCast<FungleSurveillanceMinigame>();
        if (fungle && fungle!.securityCamera && fungle.securityCamera.cam)
            Cams.Add(fungle.securityCamera.cam);
    }
}
