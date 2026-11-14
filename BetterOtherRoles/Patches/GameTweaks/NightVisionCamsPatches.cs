using BetterOtherRoles.Options;
using BetterOtherRoles.Utilities;
using HarmonyLib;
using MiraAPI.GameOptions;
using MiraAPI.Utilities.Assets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.GameTweaks;

/// <summary>
///     Night vision cams (BetterOtherRoles 1.7.1 SurveillanceMinigamePatch, <see cref="GameTweaksOptions"/>): when the
///     lights are off, the cams show every player in the same color, without cosmetics nor name, behind a green
///     overlay. The Skeld and dlekS (four screens), Polus and the Airship (one screen), the Fungle (one screen).
///     Only the cams see it: they are rendered by hand, the players changed just before and given back just after
///     (BOR changed the players for the whole screen, also seen on the map around the cams and while they open or close).
/// </summary>
[HarmonyPatch]
public static class NightVisionCamsPatches
{
    // Lime (BOR): green players under the green overlay.
    private const int NightColor = 11;

    // Overlay in front of the screen.
    private const float OverlayDepth = -0.5f;

    // Part of the overlay kept (its center): its black corners out of the screen.
    private const float OverlayCrop = 0.6f;

    private static readonly LoadableAsset<Sprite> OverlayAsset = new LoadableResourceAsset("BetterOtherRoles.Resources.Cams.NightVisionOverlay.png");

    private static Sprite? _overlaySprite;

    // Overlays of the cams open now (children of their screens, destroyed with them).
    private static readonly List<GameObject> Overlays = [];
    private static Minigame? _minigame;

    // Renderers hidden for the cams, given back after.
    private static readonly List<Renderer> Hidden = [];

    private static GameTweaksOptions Options => OptionGroupSingleton<GameTweaksOptions>.Instance;

    [HarmonyPatch(typeof(SurveillanceMinigame), nameof(SurveillanceMinigame.Update))]
    [HarmonyPostfix]
    public static void SkeldUpdatePostfix(SurveillanceMinigame __instance) =>
        Update(__instance, __instance.ViewPorts, __instance.GetComponentsInChildren<Camera>(true));

    [HarmonyPatch(typeof(PlanetSurveillanceMinigame), nameof(PlanetSurveillanceMinigame.Update))]
    [HarmonyPostfix]
    public static void PlanetUpdatePostfix(PlanetSurveillanceMinigame __instance) =>
        Update(__instance, [__instance.ViewPort], [__instance.Camera]);

    [HarmonyPatch(typeof(FungleSurveillanceMinigame), nameof(FungleSurveillanceMinigame.Update))]
    [HarmonyPostfix]
    public static void FungleUpdatePostfix(FungleSurveillanceMinigame __instance) =>
        Update(__instance, [__instance.viewport], [__instance.securityCamera ? __instance.securityCamera.cam : null]);

    private static void Update(Minigame minigame, MeshRenderer[] viewports, Camera?[] cameras)
    {
        if (_minigame != minigame)
        {
            _minigame = minigame;
            Overlays.Clear();
        }

        var shown = ShouldShow();
        SetOverlays(viewports, shown);
        foreach (var camera in cameras)
        {
            if (camera) camera!.enabled = !shown;
        }

        if (!shown) return;

        // Rendered now, with the night look, before the screen of the game is drawn with the normal one.
        SetNightLook();
        try
        {
            foreach (var camera in cameras)
            {
                if (camera && camera!.targetTexture) camera.Render();
            }
        }
        finally
        {
            GiveLookBack();
        }
    }

    private static bool ShouldShow()
    {
        var player = PlayerControl.LocalPlayer;
        if (!Options.NightVisionCams.Value || !player || player.Data == null || player.Data.IsDead) return false;
        if (Options.ImpostorsIgnoreNightVision.Value && player.Data.Role && player.Data.Role.IsImpostor) return false;

        // Lights off: everyone gets the task to fix them.
        return player.myTasks.ToArray().Any(task => task && task.TaskType == TaskTypes.FixLights);
    }

    private static void SetOverlays(MeshRenderer[] viewports, bool shown)
    {
        if (Overlays.Count == 0 && shown)
        {
            var sprite = OverlaySprite();
            foreach (var viewport in viewports)
            {
                if (viewport) Overlays.Add(MakeOverlay(viewport, sprite));
            }
        }

        foreach (var overlay in Overlays)
        {
            if (overlay) overlay.SetActive(shown);
        }
    }

    // Players in lime, cosmetics and names hidden (everything drawn by their cosmetics but the body, and the pet).
    private static void SetNightLook()
    {
        Hidden.Clear();
        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (!player || !player.cosmetics) continue;

            var body = player.cosmetics.currentBodySprite?.BodySprite;
            if (body) PlayerMaterial.SetColors(NightColor, body!);

            foreach (var renderer in player.cosmetics.GetComponentsInChildren<Renderer>())
            {
                if (renderer != body) Hide(renderer);
            }

            // Name (with the color blind text and the role written with it): not under the cosmetics.
            var name = player.cosmetics.nameTextContainer ? player.cosmetics.nameTextContainer : player.cosmetics.nameText?.gameObject;
            if (name)
            {
                foreach (var renderer in name!.GetComponentsInChildren<Renderer>())
                {
                    Hide(renderer);
                }
            }

            var pet = player.cosmetics.CurrentPet;
            if (!pet) continue;

            foreach (var renderer in pet!.GetComponentsInChildren<Renderer>())
            {
                Hide(renderer);
            }
        }

        foreach (var body in Object.FindObjectsOfType<DeadBody>())
        {
            SetBodyColor(body, NightColor);
        }
    }

    private static void GiveLookBack()
    {
        foreach (var renderer in Hidden)
        {
            if (renderer) renderer.enabled = true;
        }

        Hidden.Clear();

        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (!player || !player.cosmetics) continue;

            var body = player.cosmetics.currentBodySprite?.BodySprite;
            if (body) PlayerMaterial.SetColors(player.CurrentOutfit.ColorId, body!);
        }

        foreach (var body in Object.FindObjectsOfType<DeadBody>())
        {
            var data = GameData.Instance ? GameData.Instance.GetPlayerById(body.ParentId) : null;
            if (data) SetBodyColor(body, data!.DefaultOutfit.ColorId);
        }
    }

    private static void Hide(Renderer renderer)
    {
        if (!renderer || !renderer.enabled) return;

        renderer.enabled = false;
        Hidden.Add(renderer);
    }

    private static void SetBodyColor(DeadBody body, int colorId)
    {
        if (!body) return;

        foreach (var renderer in body.bodyRenderers)
        {
            if (renderer) PlayerMaterial.SetColors(colorId, renderer);
        }
    }

    // Center of the image of BOR: green to the corners of the screen.
    private static Sprite OverlaySprite()
    {
        if (_overlaySprite) return _overlaySprite!;

        var texture = OverlayAsset.LoadAsset().texture;
        var size = new Vector2(texture.width, texture.height) * OverlayCrop;
        var corner = (new Vector2(texture.width, texture.height) - size) / 2f;
        _overlaySprite = Sprite.Create(texture, new Rect(corner, size), new Vector2(0.5f, 0.5f), 100f);
        BorSpriteUtils.KeepLoaded(_overlaySprite);
        return _overlaySprite;
    }

    // Overlay over the whole screen, on its layer (seen by the camera of the minigame), destroyed with it.
    private static GameObject MakeOverlay(MeshRenderer viewport, Sprite sprite)
    {
        var overlay = new GameObject("NightVisionOverlay") { layer = viewport.gameObject.layer };
        overlay.transform.SetParent(viewport.transform, false);
        overlay.AddComponent<SpriteRenderer>().sprite = sprite;

        var bounds = viewport.bounds;
        var spriteSize = sprite.bounds.size;
        var parentScale = viewport.transform.lossyScale;
        overlay.transform.position = new Vector3(bounds.center.x, bounds.center.y, viewport.transform.position.z + OverlayDepth);
        overlay.transform.localScale = new Vector3(
            bounds.size.x / spriteSize.x / parentScale.x,
            bounds.size.y / spriteSize.y / parentScale.y,
            1f);
        overlay.SetActive(false);
        return overlay;
    }
}
