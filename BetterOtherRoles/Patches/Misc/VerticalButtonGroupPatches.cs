using BetterOtherRoles.Utilities;
using HarmonyLib;
using UnityEngine;

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
        if (!guide)
        {
            return false;
        }

        button.transform.SetParent(guide!.transform, false);
        button.transform.localPosition = Vector3.zero;
        button.transform.localScale = Vector3.one;
        button.layer = guide.gameObject.layer;
        _lastLayout = 0;
        return true;
    }

    [HarmonyPostfix]
    public static void UpdatePostfix(HudManager __instance)
    {
        var guide = __instance.MatchInfoButton ? __instance.MatchInfoButton.transform : null;
        if (!guide)
        {
            return;
        }

        TestModePatches.RefreshDebugButton(guide!);

        var layout = GetLayout(guide!);
        if (layout != _lastLayout || !_frame)
        {
            _lastLayout = layout;
            Rebuild(guide!);
        }

        foreach (var panel in HiddenPanels)
        {
            if (panel && panel.enabled)
            {
                panel.enabled = false;
            }
        }
    }

    // Our buttons: the direct children of the guide button that are buttons themselves.
    private static IEnumerable<Transform> GetAddedButtons(Transform guide)
    {
        for (var i = 0; i < guide.childCount; i++)
        {
            var child = guide.GetChild(i);
            if (child.name != FrameName && child.GetComponent<PassiveButton>())
            {
                yield return child;
            }
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
        if (!guidePanel)
        {
            return;
        }

        var members = new List<SpriteRenderer> { guidePanel! };
        var spacing = ButtonSpacing / guide.localScale.y;
        foreach (var button in GetAddedButtons(guide))
        {
            if (!button.gameObject.activeSelf)
            {
                continue;
            }

            button.localPosition = new Vector3(0f, -members.Count * spacing, button.localPosition.z);
            var panel = ButtonGroupPatches.FindPanel(button);
            if (panel)
            {
                members.Add(panel!);
            }
        }

        foreach (var panel in HiddenPanels)
        {
            if (panel && !members.Contains(panel))
            {
                panel.enabled = true;
            }
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
        {
            _frameSprite = BorSpriteUtils.ToSliced(guidePanel!.sprite, new Vector4(0f, PanelBorder, 0f, PanelBorder));
        }

        // In the guide button space: from its panel down to the last button.
        var top = guidePanel!.transform.localPosition;
        var bottom = guide.InverseTransformPoint(members[^1].transform.position).y;
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
