using System.Reflection;
using BetterOtherRoles.Assets;
using HarmonyLib;
using MiraAPI.Modifiers;
using MiraAPI.Utilities.Assets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Buttons added to the title bar of the role guide (Match Info), copied from the MiraAPI sort button:
///     - back: a left arrow (MiraAPI "previous page" sprites) on the left of the close button, on the role and modifier pages,
///       going back to the roles or modifiers list, hidden on any tab change;
///     - close: a cross in the top right corner of the window, always shown, closing the guide (like Escape).
/// </summary>
[HarmonyPatch]
public static class GuideBackButtonPatches
{
    private static readonly FieldInfo WikiScrollerField = AccessTools.Field(typeof(MiraAPI.Patches.RoleGuidePatches), "_wikiScroller");
    private static readonly FieldInfo OrderButtonField = AccessTools.Field(typeof(MiraAPI.Patches.RoleGuidePatches), "_searchSortingOrderButton");
    private static readonly FieldInfo TitleTextField = AccessTools.Field(typeof(MiraAPI.Patches.RoleGuidePatches), "_titleText");

    // Drawn over the wiki page (role descriptions use the guide sorting orders).
    private const int SortingOrder = 500;

    // Distance of the close button center from the outer corner of the window, in button sizes
    // (the top border of the window is thinner than its right one).
    private const float CornerOffsetX = 1.0f;
    private const float CornerOffsetY = 0.95f;

    // When the window is wider than the screen (4:3): distance of the close button center from the screen edge.
    private const float ScreenEdgeOffset = 0.57f;

    // Space between the back arrow and the close button, in button sizes.
    private const float BackButtonGap = 0.29f;

    // Measured on screenshots (800x600 and 1366x768): the drawn arrow is off its sprite bounds by this much,
    // in close button sizes (to the right and downwards).
    private const float BackButtonCorrectionX = 0.13f;
    private const float BackButtonCorrectionY = 0.07f;

    private static PassiveButton? _backButton;
    private static PassiveButton? _closeButton;
    private static bool _backToModifiers;

    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(MiraAPI.Patches.RoleGuidePatches), "DisplayAdvancedWiki", [typeof(MatchInfoGuide), typeof(RoleBehaviour)]);
        yield return AccessTools.Method(typeof(MiraAPI.Patches.RoleGuidePatches), "DisplayAdvancedWiki", [typeof(MatchInfoGuide), typeof(BaseModifier)]);
    }

    [HarmonyPostfix]
    public static void DisplayAdvancedWikiPostfix(MethodBase __originalMethod)
    {
        var wikiScroller = WikiScrollerField.GetValue(null) as Scroller;
        if (wikiScroller == null || !wikiScroller.gameObject.activeSelf)
        {
            // MiraAPI waits for the end of the opening animation: the page is shown by a later call.
            return;
        }

        _backToModifiers = __originalMethod.GetParameters()[1].ParameterType == typeof(BaseModifier);
        var title = TitleTextField.GetValue(null) as TextMeshPro;
        if (title == null)
        {
            return;
        }

        var parent = title.transform.parent;
        if (!_backButton || _backButton!.transform.parent != parent)
        {
            _backButton = CreateButton(
                parent,
                "BorGuideBackButton",
                new Vector3(title.transform.position.x, title.transform.position.y, title.transform.position.z - 1f),
                MiraAssets.PageLeftIdle.LoadAsset(),
                MiraAssets.PageLeftHover.LoadAsset(),
                GoBack);
        }

        if (_backButton)
        {
            PlaceNextToCloseButton(_backButton!);
        }

        if (_backButton)
        {
            _backButton!.gameObject.SetActive(true);
        }
    }

    /// <summary>
    ///     Any tab change (roles list, modifiers, settings, players) leaves the role page.
    ///     In its own class: HarmonyX does not allow TargetMethods with other annotated patches in the same class.
    /// </summary>
    [HarmonyPatch(typeof(MatchInfoGuide), nameof(MatchInfoGuide.SetActiveTab))]
    public static class SetActiveTabPatch
    {
        public static void Postfix()
        {
            if (_backButton)
            {
                _backButton!.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    ///     Close button, created once the opening animation of the guide is over (positions are wrong during it).
    /// </summary>
    [HarmonyPatch(typeof(MatchInfoGuide))]
    public static class CloseButtonPatch
    {
        private static float _openedAt;

        [HarmonyPatch(nameof(MatchInfoGuide.Open))]
        [HarmonyPostfix]
        public static void OpenPostfix()
        {
            _openedAt = Time.time;
        }

        [HarmonyPatch(nameof(MatchInfoGuide.Update))]
        [HarmonyPostfix]
        public static void UpdatePostfix(MatchInfoGuide __instance)
        {
            if (!__instance.MatchInfoParent.activeSelf || Time.time < _openedAt + __instance.transitionOpen.duration + 0.1f)
            {
                return;
            }

            var title = TitleTextField.GetValue(null) as TextMeshPro;
            if (title == null || (_closeButton && _closeButton!.transform.parent == title.transform.parent))
            {
                return;
            }

            // Top right corner of the window frame: the smallest sprite around both the title and the sort buttons
            // (the biggest one is the full screen shade).
            var orderButton = OrderButtonField.GetValue(null) as PassiveButton;
            var titlePosition = title.transform.position;
            var orderPosition = orderButton ? orderButton!.transform.position : titlePosition;
            var background = __instance.MatchInfoParent.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(x => x.sprite && Contains(x.bounds, titlePosition) && Contains(x.bounds, orderPosition))
                .OrderBy(x => x.bounds.size.x * x.bounds.size.y)
                .FirstOrDefault();
            if (!background)
            {
                // Tried again on the next opening only.
                Warning("[Guide] No window frame found for the close button");
                _openedAt = float.MaxValue;
                return;
            }

            _closeButton = CreateCloseButton(__instance, title, background!.bounds.max);
        }

        /// <summary>
        ///     A close button like the game settings one, at the size of the guide button of the HUD, in the top right corner of the
        ///     window, kept inside the screen (the window is wider than a 4:3 screen).
        /// </summary>
        private static PassiveButton CreateCloseButton(MatchInfoGuide guide, TextMeshPro title, Vector3 corner)
        {
            var size = 0.6f;
            var hudButton = HudManager.Instance ? HudManager.Instance.MatchInfoButton : null;
            var hudSprite = hudButton ? hudButton!.GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(x => x.enabled && x.sprite) : null;
            if (hudSprite)
            {
                size = hudSprite!.bounds.size.y;
            }

            // Inside the window frame (about 0.4 button thick), in its top right corner.
            corner.x -= size * CornerOffsetX;
            corner.y -= size * CornerOffsetY;
            var camera = HudManager.Instance ? HudManager.Instance.UICamera : Camera.main;
            if (camera)
            {
                var screenMax = camera!.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));
                corner.x = Mathf.Min(corner.x, screenMax.x - (size * ScreenEdgeOffset));
            }

            var gameObject = new GameObject("BorGuideCloseButton") { layer = title.gameObject.layer };
            gameObject.transform.SetParent(title.transform.parent, false);
            gameObject.transform.position = new Vector3(corner.x, corner.y, title.transform.position.z - 1f);

            var renderer = gameObject.AddComponent<SpriteRenderer>();
            var idle = BorAssets.CloseButton.LoadAsset();
            var hover = BorAssets.CloseButtonHover.LoadAsset();
            renderer.sprite = idle;
            renderer.sortingOrder = SortingOrder;
            gameObject.transform.localScale = Vector3.one;
            var scale = size / renderer.bounds.size.y;
            gameObject.transform.localScale = new Vector3(scale, scale, 1f);

            var collider = gameObject.AddComponent<BoxCollider2D>();
            collider.size = idle.bounds.size;

            var button = gameObject.AddComponent<PassiveButton>();
            button.Colliders = new Collider2D[] { collider };
            button.ClickSound = HudManager.Instance ? HudManager.Instance.MapButton.ClickSound : null;
            button.OnMouseOver = new UnityEngine.Events.UnityEvent();
            button.OnMouseOut = new UnityEngine.Events.UnityEvent();
            button.OnMouseOver.AddListener((Action)(() => renderer.sprite = hover));
            button.OnMouseOut.AddListener((Action)(() => renderer.sprite = idle));
            button.OnClick = new Button.ButtonClickedEvent();
            button.OnClick.AddListener((Action)(() => guide.Close()));
            return button;
        }

        private static bool Contains(Bounds bounds, Vector3 position)
        {
            return position.x >= bounds.min.x && position.x <= bounds.max.x && position.y >= bounds.min.y && position.y <= bounds.max.y;
        }
    }

    /// <summary>
    ///     On the left of the close button, centered on it. Positioned with all its drawn sprites
    ///     (the copied sort button draws its background square with another renderer than the arrow).
    /// </summary>
    private static void PlaceNextToCloseButton(PassiveButton button)
    {
        var close = _closeButton ? _closeButton!.GetComponent<SpriteRenderer>() : null;
        var arrow = button.inactiveSprites ? button.inactiveSprites.GetComponent<SpriteRenderer>() : null;
        if (!close || !arrow || arrow!.bounds.size.y <= 0f)
        {
            return;
        }

        button.transform.localScale *= close!.bounds.size.y / arrow.bounds.size.y;

        var drawn = button.GetComponentsInChildren<SpriteRenderer>(true)
            .Where(x => x.enabled && x.gameObject.activeSelf && x.sprite)
            .Select(x => x.bounds)
            .ToList();
        if (drawn.Count == 0)
        {
            return;
        }

        var bounds = drawn[0];
        foreach (var other in drawn.Skip(1))
        {
            bounds.Encapsulate(other);
        }

        var closeBounds = close.bounds;
        var gap = closeBounds.size.y * BackButtonGap;
        button.transform.position += new Vector3(
            closeBounds.min.x - gap - bounds.max.x + (closeBounds.size.y * BackButtonCorrectionX),
            closeBounds.center.y - bounds.center.y - (closeBounds.size.y * BackButtonCorrectionY),
            0f);
        var position = button.transform.position;
        button.transform.position = new Vector3(position.x, position.y, close.transform.position.z);
    }

    private static void GoBack()
    {
        if (_backToModifiers)
        {
            MiraAPI.Patches.RoleGuidePatches.OpenModifiersTab();
        }
        else
        {
            MiraAPI.Patches.RoleGuidePatches.OpenRolesTab();
        }
    }

    /// <summary>
    ///     Copy of the MiraAPI sort button, without its label, with other sprites and action.
    /// </summary>
    private static PassiveButton? CreateButton(Transform parent, string name, Vector3 position, Sprite idle, Sprite hover, Action onClick)
    {
        var orderButton = OrderButtonField.GetValue(null) as PassiveButton;
        if (orderButton == null)
        {
            return null;
        }

        var button = Object.Instantiate(orderButton, parent);
        button.name = name;
        button.gameObject.SetActive(true);
        button.transform.localScale = orderButton.transform.lossyScale.x > 0f
            ? orderButton.transform.lossyScale / parent.lossyScale.x
            : Vector3.one;
        button.transform.position = position;

        // The sort button only takes clicks inside the search bar mask, hidden on the wiki pages.
        button.ClickMask = null;
        foreach (var renderer in button.GetComponentsInChildren<SpriteRenderer>(true))
        {
            renderer.maskInteraction = SpriteMaskInteraction.None;
            renderer.sortingOrder = SortingOrder;
        }

        foreach (var text in button.GetComponentsInChildren<TextMeshPro>(true))
        {
            Object.Destroy(text.gameObject);
        }

        SetSprite(button.inactiveSprites.GetComponent<SpriteRenderer>(), idle);
        SetSprite(button.activeSprites.GetComponent<SpriteRenderer>(), hover);

        button.OnClick = new Button.ButtonClickedEvent();
        button.OnClick.AddListener(onClick);
        return button;
    }

    /// <summary>
    ///     Same size as the sort button sprite (as MiraAPI does for its "previous" / "next" page buttons).
    /// </summary>
    private static void SetSprite(SpriteRenderer renderer, Sprite sprite)
    {
        if (!renderer)
        {
            return;
        }

        var height = renderer.sprite ? renderer.sprite.bounds.size.y * renderer.transform.localScale.y : 0f;
        var scale = height > 0f && sprite.bounds.size.y > 0f ? height / sprite.bounds.size.y : 1f;
        renderer.sprite = sprite;
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }
}
