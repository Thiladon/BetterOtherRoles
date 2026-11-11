using BetterOtherRoles.Modules.Display;
using BetterOtherRoles.Utilities;
using HarmonyLib;
using MiraAPI.Utilities.Assets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Vertical button group under the role guide button (Match Info, placed by MiraAPI in its second top right row): our
///     buttons are added as children of the guide button (<see cref="Add"/>), stacked under it, and share a single frame
///     with it.
///     As children, they are shown and hidden with the guide button (intro, menus, minigames, meeting animation) and do not
///     disturb the GridArrange of MiraAPI. The frame is only rebuilt when a button of the group is shown or hidden.
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class VerticalButtonGroupPatches
{
    // Rounded ends of the panel sprite (148 x 140 px), kept as is when the frame is stretched.
    private const float PanelBorder = 40f;

    // Distance between two buttons of the group, in the space of the guide button parent, chosen in game.
    private const float ButtonSpacing = 0.52f;

    private const string FrameName = "BorVerticalGroupFrame";

    private static readonly List<SpriteRenderer> HiddenPanels = [];

    // Under this size, the guide button is being animated (or hidden by a scale of 0).
    private const float MinGuideScale = 0.01f;

    // Size of the guide button out of meetings, for the spacing of the buttons.
    private static float _guideScaleY;

    // Width on screen (world units) and pivot of the first icon of each renderer: the guide icon.
    private static readonly Dictionary<int, (float Width, Vector2 Pivot)> TemplateSizes = [];
    private static SpriteRenderer? _frame;
    private static Sprite? _frameSprite;
    private static int _lastLayout;

    /// <summary>
    ///     Adds a button under the role guide button, after the buttons already there.
    /// </summary>
    /// <param name="button">A button with the vanilla top right structure (panel sprite among its direct children).</param>
    /// <returns>False if the guide button does not exist yet.</returns>
    public static bool Add(GameObject button)
    {
        var guide = HudManager.InstanceExists ? HudManager.Instance.MatchInfoButton : null;
        if (!guide) return false;

        button.transform.SetParent(guide!.transform, false);
        button.transform.localPosition = Vector3.zero;
        button.transform.localScale = Vector3.one;
        button.layer = guide.gameObject.layer;
        _lastLayout = 0;
        return true;
    }

    /// <summary>
    ///     Makes a button of the group: a copy of the role guide button with other icons and click, hidden, added under
    ///     the guide button (<see cref="Add"/>).
    /// </summary>
    /// <param name="guide">The role guide button.</param>
    /// <param name="name">The name of the button object.</param>
    /// <param name="icon">The icon.</param>
    /// <param name="iconActive">The icon under the mouse.</param>
    /// <param name="onClick">What a click does.</param>
    /// <param name="scale">Size of the icons compared to the guide icon (<see cref="SetIcons"/>).</param>
    /// <returns>The button.</returns>
    public static GameObject CreateButton(Transform guide, string name, LoadableAsset<Sprite> icon, LoadableAsset<Sprite> iconActive, Action onClick, float scale = 1f)
    {
        // Copied without anything of ours added to the guide button.
        var copy = Object.Instantiate(guide.gameObject);
        copy.name = name;
        for (var i = copy.transform.childCount - 1; i >= 0; i--)
        {
            var child = copy.transform.GetChild(i);
            if (child.name.StartsWith("Bor", StringComparison.Ordinal))
                Object.Destroy(child.gameObject);
        }

        // Guide button scripts: AspectPosition would move the copy to the screen edge every frame (MiraAPI removes it
        // from the guide button, but later than the copy).
        Object.Destroy(copy.GetComponent<AspectPosition>());
        Object.Destroy(copy.GetComponent<MatchInfoHudButton>());

        SetIcons(copy.transform, icon, iconActive, scale);

        var button = copy.GetComponent<PassiveButton>();
        if (button)
        {
            button.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            button.OnClick.AddListener(onClick);
        }

        copy.SetActive(false);
        Add(copy);
        return copy;
    }

    /// <summary>
    ///     Replaces the icons of a button (Inactive and Active children, as on the vanilla top right buttons), with the
    ///     pivot of the guide icons and their size on screen, whatever the size of the image.
    /// </summary>
    /// <param name="button">The button.</param>
    /// <param name="icon">The icon.</param>
    /// <param name="iconActive">The icon under the mouse.</param>
    /// <param name="scale">Size compared to the guide icon (an image without margin is drawn smaller).</param>
    public static void SetIcons(Transform button, LoadableAsset<Sprite> icon, LoadableAsset<Sprite> iconActive, float scale = 1f)
    {
        foreach (var (name, asset) in new[] { ("Inactive", icon), ("Active", iconActive) })
        {
            var renderer = button.Find(name)?.GetComponent<SpriteRenderer>();
            if (!renderer || !renderer!.sprite)
            {
                var children = string.Join(", ", Enumerable.Range(0, button.childCount).Select(i => button.GetChild(i).name));
                Warning($"{button.name}: no {name} icon among {children}.");
                continue;
            }

            var texture = asset.LoadAsset().texture;
            if (renderer.sprite.texture == texture) continue;

            // Size of the guide icon, kept by the first icon set on this renderer.
            var template = renderer.sprite;
            var key = renderer.GetInstanceID();
            if (!TemplateSizes.TryGetValue(key, out var size))
            {
                size = (template.rect.width / template.pixelsPerUnit, template.pivot / template.rect.size);
                TemplateSizes[key] = size;
            }

            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), size.Pivot, texture.width / (size.Width * scale));
            sprite.name = texture.name;
            renderer.sprite = sprite;
        }
    }

    [HarmonyPostfix]
    public static void UpdatePostfix(HudManager __instance)
    {
        var guide = __instance.MatchInfoButton ? __instance.MatchInfoButton.transform : null;
        if (!guide) return;

        TestModePatches.RefreshDebugButton(guide!);
        BorZoom.RefreshButton(guide!);

        var layout = GetLayout(guide!);
        if (layout != _lastLayout || !_frame)
        {
            _lastLayout = layout;
            Rebuild(guide!);
        }

        foreach (var panel in HiddenPanels)
        {
            if (panel && panel.enabled)
                panel.enabled = false;
        }
    }

    // Our buttons: the direct children of the guide button that are buttons themselves.
    private static IEnumerable<Transform> GetAddedButtons(Transform guide)
    {
        for (var i = 0; i < guide.childCount; i++)
        {
            var child = guide.GetChild(i);
            if (child.name != FrameName && child.GetComponent<PassiveButton>())
                yield return child;
        }
    }

    private static int GetLayout(Transform guide)
    {
        var hash = 17;
        foreach (var button in GetAddedButtons(guide))
        {
            hash = (hash * 31) + (button.gameObject.activeSelf ? 1 : 2);
        }

        return hash;
    }

    private static void Rebuild(Transform guide)
    {
        var guidePanel = ButtonGroupPatches.FindPanel(guide);
        if (!guidePanel) return;

        // The size of the guide button out of meetings: the meeting opening animation changes it, the group may be
        // rebuilt meanwhile (a button hidden when the meeting starts).
        if (!MeetingHud.Instance && guide.localScale.y > MinGuideScale)
            _guideScaleY = guide.localScale.y;

        var members = new List<SpriteRenderer> { guidePanel! };
        var spacing = ButtonSpacing / (_guideScaleY > MinGuideScale ? _guideScaleY : Mathf.Max(guide.localScale.y, MinGuideScale));

        // Bottom of the last panel in the guide button space, from the local places only (not measured on screen).
        var bottom = 0f;
        foreach (var button in GetAddedButtons(guide))
        {
            if (!button.gameObject.activeSelf) continue;

            button.localPosition = new Vector3(0f, -members.Count * spacing, button.localPosition.z);
            var panel = ButtonGroupPatches.FindPanel(button);
            if (!panel) continue;

            members.Add(panel!);
            bottom = button.localPosition.y + (panel!.transform.localPosition.y * button.localScale.y);
        }

        foreach (var panel in HiddenPanels)
        {
            if (panel && !members.Contains(panel))
                panel.enabled = true;
        }

        HiddenPanels.Clear();

        if (!_frame)
        {
            var frameObject = new GameObject(FrameName) { layer = guide.gameObject.layer };
            frameObject.transform.SetParent(guide, false);
            _frame = frameObject.AddComponent<SpriteRenderer>();
        }

        var frame = _frame!;
        if (members.Count < 2)
        {
            // The guide button alone keeps its own frame.
            guidePanel!.enabled = true;
            frame.gameObject.SetActive(false);
            return;
        }

        if (!_frameSprite)
            _frameSprite = BorSpriteUtils.ToSliced(guidePanel!.sprite, new Vector4(0f, PanelBorder, 0f, PanelBorder));

        // In the guide button space: from its panel down to the last button.
        var top = guidePanel!.transform.localPosition;
        var scale = guidePanel.transform.localScale.y;
        frame.sprite = _frameSprite;
        frame.drawMode = SpriteDrawMode.Sliced;
        frame.sharedMaterial = guidePanel.sharedMaterial;
        frame.color = guidePanel.color;
        frame.sortingLayerID = guidePanel.sortingLayerID;
        frame.sortingOrder = guidePanel.sortingOrder;
        frame.size = new Vector2(_frameSprite!.rect.width / _frameSprite.pixelsPerUnit, (_frameSprite.rect.height / _frameSprite.pixelsPerUnit) + ((top.y - bottom) / scale));
        frame.transform.localScale = new Vector3(guidePanel.transform.localScale.x, scale, 1f);
        frame.transform.localPosition = new Vector3(top.x, (top.y + bottom) / 2f, top.z);
        frame.gameObject.SetActive(true);

        foreach (var panel in members)
        {
            panel.enabled = false;
            HiddenPanels.Add(panel);
        }
    }
}
