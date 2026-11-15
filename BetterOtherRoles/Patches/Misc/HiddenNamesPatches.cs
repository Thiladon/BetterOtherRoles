using BetterOtherRoles.Options;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MiraAPI.GameOptions;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Names of the other players hidden out of sight (TOU-Mira 1.7.9 HideNamesOutOfSight,
///     <see cref="GameplayOptions.HideNamesOutOfSight"/>), for a living player:
///     <list type="bullet">
///         <item>basic: as in the game, every name is shown;</item>
///         <item>room-based: shown when the player is in sight, or in the same room, even behind a wall;</item>
///         <item>harsh: shown only when the player is in sight, the room does not count.</item>
///     </list>
///     Either way, shown while a part of the player is on a cam watched now (not while they show static).
///     In sight: a point of the player (feet, center, head, sides) is in the light and nothing that casts a shadow
///     stands between it and the light, as the shadows of the game (LightSourceGpuRenderer): one point is enough. TOU
///     only checks the feet against the walls. The name text is turned off (the role and progress written in it with
///     it, and the color blind text) rather than emptied, as the name is written again each frame
///     (<see cref="RoleNamePatches"/>). Only the names hidden here are shown again; none in the lobby, in meetings or
///     in Hide and Seek.
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class HiddenNamesPatches
{
    // Points of the player checked, from its center (world units).
    private const float HeadHeight = 0.4f;
    private const float SideWidth = 0.25f;

    private static readonly HashSet<byte> Hidden = [];
    private static readonly Il2CppStructArray<RaycastHit2D> Hits = new(32);

    // Cams of the minigame open now.
    private static readonly List<Camera> Cams = [];
    private static readonly List<Camera> NoCams = [];
    private static Minigame? _camsMinigame;

    public static void Postfix(HudManager __instance)
    {
        var mode = GetMode(__instance);
        if (mode == NameSight.Basic)
        {
            if (Hidden.Count > 0) ShowAll();
            return;
        }

        if (MeetingHud.Instance) return;

        var local = PlayerControl.LocalPlayer;
        var cams = GetCams();
        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (!player || player.AmOwner || player.Data == null || !player.cosmetics) continue;

            SetShown(player, IsShown(local, player, mode, cams));
        }
    }

    private static bool IsShown(PlayerControl local, PlayerControl player, NameSight mode, List<Camera> cams) =>
        InSight(local.lightSource, player) || (mode == NameSight.RoomBased && InSameRoom(local, player)) || OnCams(player, cams);

    // The cams watched now, none while they show static (comms sabotaged): the four of the Skeld, the one of Polus
    // and the Airship, the binoculars of the Fungle. Found again only when another minigame opens.
    private static List<Camera> GetCams()
    {
        var minigame = Minigame.Instance;
        if (minigame != _camsMinigame)
        {
            _camsMinigame = minigame;
            Cams.Clear();
            if (minigame) FindCams(minigame!);
        }

        if (!minigame) return Cams;

        var skeld = minigame!.TryCast<SurveillanceMinigame>();
        if (skeld && skeld!.isStatic) return NoCams;

        var planet = minigame.TryCast<PlanetSurveillanceMinigame>();
        if (planet && planet!.isStatic) return NoCams;

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

    // A part of the body in the frame of a cam (the cams have no shadows): its sprite, or the points checked in sight.
    private static bool OnCams(PlayerControl player, List<Camera> cams)
    {
        if (cams.Count == 0 || !player.Visible) return false;

        var body = player.cosmetics.currentBodySprite?.BodySprite;
        var bounds = body ? body!.bounds : new Bounds(player.transform.position, new Vector3(SideWidth * 2f, HeadHeight * 2f, 0f));
        foreach (var cam in cams)
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

    // Basic when the names are not hidden: not in game, Hide and Seek, dead or without shadows (the dead see all).
    private static NameSight GetMode(HudManager hud)
    {
        var local = PlayerControl.LocalPlayer;
        if (!ShipStatus.Instance || LobbyBehaviour.Instance || !local || local.Data == null || local.Data.IsDead ||
            !local.lightSource || !hud.ShadowQuad || !hud.ShadowQuad.gameObject.activeInHierarchy ||
            (GameManager.Instance && GameManager.Instance.IsHideAndSeek()))
            return NameSight.Basic;

        return (NameSight)OptionGroupSingleton<GameplayOptions>.Instance.HideNamesOutOfSight.Value;
    }

    private static bool InSight(LightSource light, PlayerControl player)
    {
        Vector2 origin = light.transform.position;
        Vector2 center = player.transform.position;
        var range = light.ViewDistance;

        return CanSee(light, origin, player.GetTruePosition(), range) ||
               CanSee(light, origin, center, range) ||
               CanSee(light, origin, center + Vector2.up * HeadHeight, range) ||
               CanSee(light, origin, center + Vector2.left * SideWidth, range) ||
               CanSee(light, origin, center + Vector2.right * SideWidth, range);
    }

    // In the light and nothing casting a shadow in between.
    private static bool CanSee(LightSource light, Vector2 origin, Vector2 target, float range)
    {
        var vector = target - origin;
        var distance = vector.magnitude;
        if (distance > range) return false;
        if (distance < 0.01f) return true;

        var count = Physics2D.RaycastNonAlloc(origin, vector / distance, Hits, distance, Constants.ShadowMask);
        for (var i = 0; i < count; i++)
        {
            var collider = Hits[i].collider;
            if (collider && CastsShadow(light, collider)) return false;
        }

        return true;
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

    private static bool InSameRoom(PlayerControl local, PlayerControl player)
    {
        var position = local.GetTruePosition();
        var other = player.GetTruePosition();
        foreach (var room in ShipStatus.Instance.FastRooms.Values)
        {
            if (room && room.roomArea && room.roomArea.OverlapPoint(position) && room.roomArea.OverlapPoint(other)) return true;
        }

        return false;
    }

    private static void SetShown(PlayerControl player, bool shown)
    {
        if (shown)
        {
            if (Hidden.Remove(player.PlayerId))
                Toggle(player, true);
            return;
        }

        Hidden.Add(player.PlayerId);
        Toggle(player, false);
    }

    private static void Toggle(PlayerControl player, bool shown)
    {
        var name = player.cosmetics.nameText;
        if (name && name.enabled != shown)
            name.enabled = shown;

        var colorBlind = player.cosmetics.colorBlindText;
        if (colorBlind && colorBlind.enabled != shown)
            colorBlind.enabled = shown;
    }

    // Out of the game, dead or option off: every hidden name back.
    private static void ShowAll()
    {
        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (player && player.cosmetics && Hidden.Contains(player.PlayerId))
                Toggle(player, true);
        }

        Hidden.Clear();
    }
}
