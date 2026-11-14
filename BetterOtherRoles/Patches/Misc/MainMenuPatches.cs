using BepInEx;
using BepInEx.Unity.IL2CPP;
using BetterOtherRoles.Utilities;
using HarmonyLib;
using MiraAPI.Utilities.Assets;
using Reactor;
using Reactor.Patches;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Main menu (BetterOtherRoles 1.7.1 CredentialsPatch.LogoPatch): in the screen on the right, the banner of BOR
///     centered at the top with its version and credits below it, and the text of Reactor at the bottom, reduced to one
///     line with the other mods (without Reactor and BOR). The logo of the game is kept; the version of the game, at the
///     bottom left, is followed by the versions of BepInEx and Reactor.
///     Everything is placed from the place Reactor gives to its text (top left of the screen), which follows the
///     width of the window.
/// </summary>
[HarmonyPatch]
public static class MainMenuPatches
{
    private const string BasedOn = "<color=#ff351f>TheOtherRoles</color>";
    private const string PortAuthor = "<color=#7897d6>Thilladon</color>";

    // Banner of BOR 1.7.1 (Resources/Banner.png) cropped to its text.
    private static readonly LoadableAsset<Sprite> Banner = new LoadableResourceAsset("BetterOtherRoles.Resources.Menus.Banner.png");

    // From the top left of the screen (top left of the text of Reactor), in world units (100 pixels in 800 × 600).
    private const float ScreenCenter = 2.25f;
    private const float BannerTop = 0.25f;
    private const float BannerWidth = 3.2f;
    private const float BannerInfoGap = 0.1f;

    // Space between the left edge of the screen and the version of the game, in world units.
    private const float VersionMargin = 0.1f;
    private const float VersionDepth = -5.5f;

    // Height from the top of the screen to the bottom of the text of Reactor.
    private const float ScreenHeight = 4.2f;

    private static Sprite? _banner;
    private static SpriteRenderer? _bannerRenderer;
    private static TextMeshPro? _bannerInfo;

    /// <summary>
    ///     Text of Reactor on the main menu: the mods with their version on one line
    ///     (without Reactor and BOR); the banner and credits of BOR above.
    /// </summary>
    [HarmonyPatch(typeof(ReactorVersionShower), nameof(ReactorVersionShower.UpdateText))]
    [HarmonyPostfix]
    public static void UpdateTextPostfix()
    {
        var text = ReactorVersionShower.Text;
        if (!text) return;

        var mods = IL2CPPChainloader.Instance.Plugins.Values
            .Where(plugin => plugin.Metadata.GUID != ReactorPlugin.Id && plugin.Metadata.GUID != BetterOtherRolesPlugin.Id)
            .Select(plugin => $"{plugin.Metadata.Name} {WithoutBuild(plugin.Metadata.Version.ToString())}")
            .OrderBy(mod => mod, StringComparer.OrdinalIgnoreCase)
            .ToList();
        text!.text = mods.Count > 0 ? "Mods: " + string.Join(", ", mods) : string.Empty;
        Place(text);
    }

    /// <summary>
    ///     Version of the game at the bottom left, on one line: "v19s (build 7489) + BepInEx v6.0.0-be.735 + Reactor v2.5.1".
    /// </summary>
    [HarmonyPatch(typeof(VersionShower), nameof(VersionShower.Start))]
    [HarmonyPostfix]
    public static void VersionShowerStartPostfix(VersionShower __instance)
    {
        var text = __instance.text;
        if (!text) return;

        text.text = text.text.Replace("(build num: ", "(build ", StringComparison.Ordinal)
            + $" + BepInEx v{WithoutBuild(Paths.BepInExVersion.ToString())} + Reactor v{WithoutBuild(ReactorPlugin.Version)}";

        // The game shrinks the text to fit its box, under the buttons (it became tiny): kept at its largest size,
        // written from the left edge of the screen, going on below the screen on the right.
        text.enableAutoSizing = false;
        text.fontSize = text.fontSizeMax;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.Left;

        // In front of the frame of the screen on the right (z -5), whose shadow reaches the line, behind the menus.
        var position = text.transform.position;
        text.transform.position = new Vector3(position.x, position.y, VersionDepth);

        var camera = Camera.main;
        var scale = text.transform.lossyScale.x;
        if (!camera || scale <= 0f) return;

        var screenLeft = camera!.transform.position.x - (camera.orthographicSize * camera.aspect) + VersionMargin;
        var boxLeft = (screenLeft - text.transform.position.x) / scale;
        text.rectTransform.sizeDelta = new Vector2(Math.Abs(boxLeft) * 2f, text.rectTransform.sizeDelta.y);
    }

    /// <summary>
    ///     Reactor places its text again when the window is resized.
    /// </summary>
    [HarmonyPatch(typeof(ReactorVersionShower), "SetMainMenuPositionFromAspect")]
    [HarmonyPostfix]
    public static void SetMainMenuPositionFromAspectPostfix()
    {
        var text = ReactorVersionShower.Text;
        if (text) Place(text!);
    }

    // On the main menu only (elsewhere, Reactor places its text with an AspectPosition).
    private static void Place(TextMeshPro text)
    {
        if (text.GetComponent<AspectPosition>()) return;

        // Reactor gives the center of its text box (20 × 5), the text being written from its top left corner.
        var aspect = (float)Screen.width / Screen.height;
        var origin = new Vector3((-1.2287f * aspect) + 10.9f, -0.57f, 4.5f);
        var box = text.rectTransform.rect;
        var topLeft = origin + new Vector3(box.xMin, box.yMax, 0f);
        var center = topLeft + (Vector3.right * ScreenCenter);

        var bannerHeight = 0f;
        var banner = GetBanner(text);
        if (banner)
        {
            bannerHeight = banner!.sprite.bounds.size.y;
            banner.transform.position = center + (Vector3.down * (BannerTop + (bannerHeight / 2f)));
        }

        var bannerInfo = GetBannerInfo(text);
        bannerInfo.transform.position = center + (Vector3.down * (BannerTop + bannerHeight + BannerInfoGap));

        text.transform.position = origin + (Vector3.down * (ScreenHeight - text.preferredHeight));
    }

    private static SpriteRenderer? GetBanner(TextMeshPro text)
    {
        if (_bannerRenderer) return _bannerRenderer;

        if (!_banner) _banner = MakeBanner();
        if (!_banner) return null;

        var banner = new GameObject("BorBanner") { layer = text.gameObject.layer };
        _bannerRenderer = banner.AddComponent<SpriteRenderer>();
        _bannerRenderer.sprite = _banner;
        return _bannerRenderer;
    }

    // Version and credits below the banner: copy of the text of Reactor (font, size, outline), centered and hanging
    // from its position.
    private static TextMeshPro GetBannerInfo(TextMeshPro text)
    {
        if (_bannerInfo) return _bannerInfo!;

        _bannerInfo = Object.Instantiate(text);
        _bannerInfo.name = "BorBannerInfo";
        _bannerInfo.rectTransform.sizeDelta = Vector2.zero;
        _bannerInfo.alignment = TextAlignmentOptions.Top;
        _bannerInfo.text = $"v{BetterOtherRolesPlugin.Version}\nBased on {BasedOn}\nPorted to MiraAPI by {PortAuthor}";
        return _bannerInfo;
    }

    // Copy of the banner with the pixels per unit giving it its width.
    private static Sprite? MakeBanner()
    {
        var source = Banner.LoadAsset();
        if (!source) return null;

        var texture = source.texture;
        var banner = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width / BannerWidth);
        banner.name = "BorBanner";
        BorSpriteUtils.KeepLoaded(banner);
        return banner;
    }

    // "2.5.1+abc" → "2.5.1".
    private static string WithoutBuild(string version)
    {
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? version : version[..plus];
    }
}
