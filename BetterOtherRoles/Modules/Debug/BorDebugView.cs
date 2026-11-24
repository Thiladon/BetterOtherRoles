using HarmonyLib;
using UnityEngine;

namespace BetterOtherRoles.Modules.Debug;

/// <summary>
///     Debug menu, to take screenshots of the rooms, on the screen of the local player only: invisible (body, cosmetics,
///     pet and name; the others still see them), interface hidden (shown again while the Debug menu is open), full
///     vision (no shadows), through the walls and zoom out while alive. Off again at the end of the game.
/// </summary>
[HarmonyPatch]
public static class BorDebugView
{
    private static readonly int UiLayerMask = 1 << LayerMask.NameToLayer("UI");

    // The cameras whose interface layer was removed, to give it back to them alone.
    private static readonly List<Camera> HudCameras = [];

    /// <summary>
    ///     Gets a value indicating whether the local player is invisible.
    /// </summary>
    public static bool Invisible { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether the interface is hidden.
    /// </summary>
    public static bool HideHud { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether the shadows are removed.
    /// </summary>
    public static bool FullVision { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether the local player goes through the walls.
    /// </summary>
    public static bool NoClip { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether the local player can zoom out while alive (<see cref="Display.BorZoom"/>).
    /// </summary>
    public static bool AliveZoom { get; private set; }

    /// <summary>
    ///     Goes through the walls, or not any more.
    /// </summary>
    public static void ToggleNoClip()
    {
        NoClip = !NoClip;
        SetCollider(!NoClip);
    }

    /// <summary>
    ///     Allows the zoom while alive, or not any more (back to the normal view).
    /// </summary>
    public static void ToggleAliveZoom()
    {
        AliveZoom = !AliveZoom;
        if (!AliveZoom) Display.BorZoom.Reset();
    }

    /// <summary>
    ///     Makes the local player invisible, or visible again.
    /// </summary>
    public static void ToggleInvisible()
    {
        Invisible = !Invisible;
        var local = PlayerControl.LocalPlayer;
        if (local) local.Visible = !Invisible && !local.inVent;
    }

    /// <summary>
    ///     Hides the interface, or shows it again.
    /// </summary>
    public static void ToggleHud()
    {
        HideHud = !HideHud;
        SetHudShown(!HideHud);
    }

    /// <summary>
    ///     Removes the shadows, or puts them back.
    /// </summary>
    public static void ToggleFullVision()
    {
        FullVision = !FullVision;
        SetShadowsShown(!FullVision);
    }

    // The game shows the player and the shadows again in many places (vents, meetings...): set again at each frame.
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    [HarmonyPostfix]
    public static void FixedUpdatePostfix(PlayerControl __instance)
    {
        if (!__instance.AmOwner || (!Invisible && !HideHud && !FullVision && !NoClip && !AliveZoom)) return;

        if (!BorDebugActions.CanUse)
        {
            TurnOff();
            return;
        }

        if (Invisible && __instance.Visible) __instance.Visible = false;

        // The Debug menu is in the interface: shown while it is open, to give it back.
        if (HideHud) SetHudShown(BorPhoneMenu.Current != null);

        if (FullVision) SetShadowsShown(false);

        if (NoClip) SetCollider(false);
    }

    private static void TurnOff()
    {
        if (HideHud) SetHudShown(true);
        if (FullVision && !MeetingHud.Instance) SetShadowsShown(true);
        if (NoClip) SetCollider(true);
        Invisible = HideHud = FullVision = NoClip = AliveZoom = false;
    }

    private static void SetCollider(bool enabled)
    {
        var local = PlayerControl.LocalPlayer;
        if (local && local.Collider && local.Collider.enabled != enabled)
            local.Collider.enabled = enabled;
    }

    // The interface layer is removed from the cameras drawing it (the main camera only), and given back to them alone:
    // given to the other cameras, it drew a second interface.
    private static void SetHudShown(bool shown)
    {
        if (!shown)
        {
            foreach (var camera in Camera.allCameras)
            {
                if (!camera || (camera.cullingMask & UiLayerMask) == 0) continue;

                camera.cullingMask &= ~UiLayerMask;
                if (!HudCameras.Contains(camera)) HudCameras.Add(camera);
            }

            return;
        }

        foreach (var camera in HudCameras)
        {
            if (camera) camera.cullingMask |= UiLayerMask;
        }

        HudCameras.Clear();
    }

    private static void SetShadowsShown(bool shown)
    {
        var hud = HudManager.Instance;
        if (hud && hud.ShadowQuad && hud.ShadowQuad.gameObject.activeSelf != shown)
            hud.ShadowQuad.gameObject.SetActive(shown);
    }
}
