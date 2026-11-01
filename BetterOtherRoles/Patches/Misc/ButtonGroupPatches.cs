using BetterOtherRoles.Utilities;
using HarmonyLib;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Button group at the top right, in the lobby and in game: the settings button and every button on its left (chat,
///     future buttons) share a single frame instead of one frame each, and are moved closer to the settings. The map
///     and friends list buttons, on the right of the settings, keep their own frame. The frame is hidden during the
///     meeting animation, like the buttons (<see cref="KillOverlayHudPatches"/>).
///     The buttons are placed by the game in the TopRight container and only move at transitions (lobby, game start,
///     meeting, chat shown or hidden): the frame is only rebuilt when a button of the container moves or is shown or
///     hidden, or when the container moves (the frame is next to it, not inside).
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class ButtonGroupPatches
{
    private const string PanelSpritePrefix = "IMAGE-ButtonPanel";

    // Rounded ends of the panel sprite (148 x 140 px), kept as is when the frame is stretched.
    private const float PanelBorder = 40f;

    // Distance between two buttons of the group (the game uses 0.85, one frame each), chosen in game.
    private const float ButtonSpacing = 0.55f;

    private static readonly List<SpriteRenderer> HiddenPanels = [];
    private static SpriteRenderer? _frame;
    private static Sprite? _frameSprite;
    private static int _lastLayout;

    [HarmonyPostfix]
    public static void UpdatePostfix(HudManager __instance)
    {
        if (!__instance.SettingsButton)
        {
            return;
        }

        var container = __instance.SettingsButton.transform.parent;
        if (!container)
        {
            return;
        }

        var layout = GetLayout(container);
        if (layout != _lastLayout || !_frame)
        {
            Rebuild(container, __instance.SettingsButton.transform);

            // The buttons moved closer: their new places are the expected layout.
            _lastLayout = GetLayout(container);
        }

        // The game never toggles the panels, but keep them hidden if it ever does.
        foreach (var panel in HiddenPanels)
        {
            if (panel && panel.enabled)
            {
                panel.enabled = false;
            }
        }
    }

    /// <summary>
    ///     The game arranges the container again after our update (death of the local player...): the buttons and the
    ///     frame went back to the game places for a frame or two (flash). Placed back at once.
    /// </summary>
    [HarmonyPatch(typeof(GridArrange), nameof(GridArrange.ArrangeChilds))]
    public static class ArrangeChildsPatch
    {
        public static void Postfix(GridArrange __instance)
        {
            var hud = HudManager.InstanceExists ? HudManager.Instance : null;
            if (!hud || !hud!.SettingsButton || hud.SettingsButton.transform.parent != __instance.transform)
            {
                return;
            }

            Rebuild(__instance.transform, hud.SettingsButton.transform);
            _lastLayout = GetLayout(__instance.transform);
        }
    }

    // Cheap fingerprint of the container: where it is, which buttons are shown and where.
    private static int GetLayout(Transform container)
    {
        var hash = container.childCount + (KillOverlayHudPatches.IsMeetingAnimation ? 1000 : 0);
        hash = (hash * 31) + Mathf.RoundToInt(container.localPosition.x * 100f);
        hash = (hash * 31) + Mathf.RoundToInt(container.localPosition.y * 100f);
        for (var i = 0; i < container.childCount; i++)
        {
            var child = container.GetChild(i);
            hash = (hash * 31) + (child.gameObject.activeSelf ? 1 : 0);
            hash = (hash * 31) + Mathf.RoundToInt(child.localPosition.x * 100f);
            hash = (hash * 31) + Mathf.RoundToInt(child.localPosition.y * 100f);
        }

        return hash;
    }

    private static void Rebuild(Transform container, Transform settings)
    {
        var buttons = new List<(Transform Button, SpriteRenderer Panel)>();
        for (var i = 0; i < container.childCount; i++)
        {
            var child = container.GetChild(i);
            if (!child.gameObject.activeSelf || child.localPosition.x > settings.localPosition.x + 0.01f)
            {
                continue;
            }

            var panel = FindPanel(child);
            if (panel)
            {
                buttons.Add((child, panel!));
            }
        }

        // From the settings to the left: each button moves closer to the previous one.
        buttons.Sort((a, b) => b.Button.localPosition.x.CompareTo(a.Button.localPosition.x));
        for (var i = 0; i < buttons.Count; i++)
        {
            var button = buttons[i].Button;
            var position = button.localPosition;
            button.localPosition = new Vector3(settings.localPosition.x - (i * ButtonSpacing), position.y, position.z);
        }

        var members = buttons.ConvertAll(button => button.Panel);

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
            _frame = CreateFrame(container);
        }

        if (members.Count == 0 || KillOverlayHudPatches.IsMeetingAnimation)
        {
            _frame!.gameObject.SetActive(false);
            return;
        }

        // Positions and scale of the panels, in the container space.
        var scale = members[0].transform.lossyScale.x / container.lossyScale.x;
        var containerScale = container.localScale.x;
        var minX = float.MaxValue;
        var maxX = float.MinValue;
        var y = 0f;
        var z = float.MinValue;
        foreach (var panel in members)
        {
            var position = container.InverseTransformPoint(panel.transform.position);
            minX = Mathf.Min(minX, position.x);
            maxX = Mathf.Max(maxX, position.x);
            y = position.y;

            // Behind every button of the group.
            z = Mathf.Max(z, position.z);
        }

        var source = members[0];
        if (!_frameSprite)
        {
            _frameSprite = BorSpriteUtils.ToSliced(source.sprite, new Vector4(PanelBorder, 0f, PanelBorder, 0f));
        }


        var frame = _frame!;
        frame.sprite = _frameSprite!;
        frame.drawMode = SpriteDrawMode.Sliced;
        frame.sharedMaterial = source.sharedMaterial;
        frame.color = source.color;
        frame.sortingLayerID = source.sortingLayerID;
        frame.sortingOrder = source.sortingOrder;
        frame.size = new Vector2(_frameSprite!.rect.width / _frameSprite.pixelsPerUnit + ((maxX - minX) / scale), _frameSprite.rect.height / _frameSprite.pixelsPerUnit);
        frame.transform.localScale = new Vector3(scale * containerScale, scale * containerScale, 1f);
        frame.transform.localPosition = container.localPosition + (new Vector3((minX + maxX) / 2f, y, z) * containerScale);
        frame.gameObject.layer = container.gameObject.layer;
        frame.gameObject.SetActive(true);

        foreach (var panel in members)
        {
            panel.enabled = false;
            HiddenPanels.Add(panel);
        }
    }

    /// <summary>
    ///     The frame of a top right button (vanilla panel sprite), among its direct children.
    /// </summary>
    /// <param name="button">The button.</param>
    /// <returns>The frame renderer, or null.</returns>
    public static SpriteRenderer? FindPanel(Transform button)
    {
        for (var i = 0; i < button.childCount; i++)
        {
            var renderer = button.GetChild(i).GetComponent<SpriteRenderer>();
            if (renderer && renderer.sprite && renderer.sprite.name.StartsWith(PanelSpritePrefix, StringComparison.Ordinal))
            {
                return renderer;
            }
        }

        return null;
    }

    // Next to the container, not inside: its GridArrange would place the frame as a button.
    private static SpriteRenderer CreateFrame(Transform container)
    {
        var frame = new GameObject("BorButtonGroupFrame");
        frame.transform.SetParent(container.parent, false);
        return frame.AddComponent<SpriteRenderer>();
    }
}
