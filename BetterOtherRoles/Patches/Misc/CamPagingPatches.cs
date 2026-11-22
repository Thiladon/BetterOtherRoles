using BetterOtherRoles.LocalSettings;
using BetterOtherRoles.Utilities;
using HarmonyLib;
using MiraAPI.LocalSettings;
using Rewired;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Cams changed by hand or by themselves (BetterOtherRoles 1.7.1 SurveillanceMinigamePatch,
///     <see cref="BorLocalSettingsTab.AutoCams"/>):
///     <list type="bullet">
///         <item>Polus and the Airship (one camera at a time): the left and right move keys of the game (KBLeft and
///         KBRight in Rewired, changed in Remap Controls) go to the previous or next camera; with Automatic Cams, the
///         next camera comes by itself after the delay.</item>
///         <item>The Skeld and dlekS (four screens): with more than four cams (a role adding some, later), they are shown
///         by pages of four, changed by the same keys or by themselves; the screens after the last cam show static. One
///         dot per page under the screens, the page shown lit (as the dots of the cams of Polus, TOU-Mira 1.7.9).</item>
///     </list>
///     Nothing changes while the comms are sabotaged. Any change of camera or page starts the delay again.
/// </summary>
[HarmonyPatch]
public static class CamPagingPatches
{
    // Rewired actions of the left and right move keys (keyboard, category 0, always on).
    private const string LeftAction = "KBLeft";
    private const string RightAction = "KBRight";

    // Dots of the pages: size and space between their centers (as Polus), gap under the screens.
    private const int DotPixels = 32;
    private const float DotSize = 0.12f;
    private const float DotSpacing = 0.25f;
    private const float DotGap = 0.15f;

    private static readonly Color DotOn = Color.white;
    private static readonly Color DotOff = new(1f, 1f, 1f, 0.3f);

    // The Skeld: page shown, and whether its screens showed static at the last frame.
    private static int _page;
    private static bool _skeldWasStatic;

    // Dots of the cams open now (children of the screens of the minigame, destroyed with it).
    private static readonly List<SpriteRenderer> Dots = [];
    private static Sprite? _dotSprite;

    // Time since the last change of camera or page.
    private static float _timer;

    private static BorLocalSettingsTab? Settings => LocalSettingsTabSingleton<BorLocalSettingsTab>.Instance;

    [HarmonyPatch(typeof(PlanetSurveillanceMinigame), nameof(PlanetSurveillanceMinigame.Begin))]
    [HarmonyPostfix]
    public static void PlanetBeginPostfix() => _timer = 0f;

    [HarmonyPatch(typeof(PlanetSurveillanceMinigame), nameof(PlanetSurveillanceMinigame.NextCamera))]
    [HarmonyPostfix]
    public static void PlanetNextCameraPostfix() => _timer = 0f;

    [HarmonyPatch(typeof(PlanetSurveillanceMinigame), nameof(PlanetSurveillanceMinigame.Update))]
    [HarmonyPostfix]
    public static void PlanetUpdatePostfix(PlanetSurveillanceMinigame __instance)
    {
        if (__instance.isStatic || __instance.survCameras == null || __instance.survCameras.Length < 2) return;

        var direction = ReadDirection();
        if (direction != 0)
        {
            __instance.NextCamera(direction);
            return;
        }

        if (AutoChangeDue()) __instance.NextCamera(1);
    }

    [HarmonyPatch(typeof(SurveillanceMinigame), nameof(SurveillanceMinigame.Begin))]
    [HarmonyPostfix]
    public static void SkeldBeginPostfix()
    {
        _page = 0;
        _timer = 0f;
        _skeldWasStatic = false;
        Dots.Clear();
    }

    [HarmonyPatch(typeof(SurveillanceMinigame), nameof(SurveillanceMinigame.Update))]
    [HarmonyPostfix]
    public static void SkeldUpdatePostfix(SurveillanceMinigame __instance)
    {
        var screens = __instance.ViewPorts.Length;
        var textures = __instance.textures;
        if (screens == 0 || textures == null || textures.Length <= screens) return;

        var pages = Mathf.CeilToInt(textures.Length / (float)screens);
        if (Dots.Count != pages) MakeDots(__instance, pages);

        var isStatic = __instance.isStatic;
        var wasStatic = _skeldWasStatic;
        _skeldWasStatic = isStatic;
        if (isStatic) return;

        var direction = ReadDirection();
        if (direction == 0 && AutoChangeDue()) direction = 1;

        // The game puts the first four cams back when the comms are fixed: the page is shown again.
        if (direction == 0 && !wasStatic) return;

        _timer = 0f;
        if (direction != 0)
        {
            _page = (_page + direction + pages) % pages;
            if (Constants.ShouldPlaySfx() && HudManager.Instance) SoundManager.Instance.PlaySound(HudManager.Instance.MapButton.ClickSound, false);
        }

        ShowPage(__instance, screens);
    }

    private static void ShowPage(SurveillanceMinigame minigame, int screens)
    {
        var textures = minigame.textures;
        for (var i = 0; i < screens; i++)
        {
            var viewport = minigame.ViewPorts[i];
            minigame.SabText[i].gameObject.SetActive(false);

            var index = _page * screens + i;
            if (index >= textures.Length)
            {
                viewport.sharedMaterial = minigame.StaticMaterial;
                continue;
            }

            viewport.sharedMaterial = minigame.DefaultMaterial;
            viewport.material.SetTexture("_MainTex", textures[index]);
        }

        LightDots();
    }

    // One dot per page, centered under the screens, in front of them; shown and hidden with the screens.
    private static void MakeDots(SurveillanceMinigame minigame, int pages)
    {
        foreach (var dot in Dots)
        {
            if (dot) Object.Destroy(dot.gameObject);
        }

        Dots.Clear();

        var viewports = minigame.ViewPorts;
        var bounds = viewports[0].bounds;
        foreach (var viewport in viewports)
        {
            bounds.Encapsulate(viewport.bounds);
        }

        var layer = viewports[0].gameObject.layer;
        var z = viewports[0].transform.position.z - 0.5f;
        var y = bounds.min.y - DotGap;
        var firstX = bounds.center.x - (pages - 1) * DotSpacing / 2f;
        var sprite = DotSprite();
        for (var i = 0; i < pages; i++)
        {
            var dot = new GameObject($"BorCamPageDot{i}") { layer = layer };
            dot.transform.SetParent(minigame.Viewables.transform, false);
            dot.transform.position = new Vector3(firstX + i * DotSpacing, y, z);

            var renderer = dot.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            Dots.Add(renderer);
        }

        if (_page >= pages) _page = 0;
        LightDots();
    }

    private static void LightDots()
    {
        for (var i = 0; i < Dots.Count; i++)
        {
            if (Dots[i]) Dots[i].color = i == _page ? DotOn : DotOff;
        }
    }

    // White disc, smooth edge, tinted by the renderer.
    private static Sprite DotSprite()
    {
        if (_dotSprite) return _dotSprite!;

        var texture = new Texture2D(DotPixels, DotPixels, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var radius = DotPixels / 2f;
        for (var x = 0; x < DotPixels; x++)
        {
            for (var y = 0; y < DotPixels; y++)
            {
                var distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - distance)));
            }
        }

        texture.Apply();
        _dotSprite = Sprite.Create(texture, new Rect(0f, 0f, DotPixels, DotPixels), new Vector2(0.5f, 0.5f), DotPixels / DotSize);
        BorSpriteUtils.KeepLoaded(_dotSprite);
        return _dotSprite;
    }

    // -1 or 1 on the frame a move key is pressed, else 0. Not while typing in the chat (a dead player can open it).
    private static int ReadDirection()
    {
        var hud = HudManager.Instance;
        if (hud && hud.Chat && hud.Chat.IsOpenOrOpening) return 0;
        if (!ReInput.isReady) return 0;

        var player = ReInput.players.GetPlayer(0);
        if (player.GetButtonDown(RightAction)) return 1;
        if (player.GetButtonDown(LeftAction)) return -1;

        return 0;
    }

    private static bool AutoChangeDue()
    {
        var settings = Settings;
        if (settings == null || !settings.AutoCams.Value) return false;

        _timer += Time.deltaTime;
        return _timer >= settings.AutoCamsDelay.Value;
    }
}
