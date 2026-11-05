using System.Reflection;
using HarmonyLib;
using MiraAPI.GameOptions;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.Options;

/// <summary>
///     Group headers of the lobby game settings: MiraAPI draws them with its own sprite, thinner than the vanilla
///     headers and with another rounded end, and puts "(click to close / open)" close to that end.
///     They get the vanilla look back (sprite, height, rounded end, divider), longer on the right, with the hint at a
///     fixed distance from the end.
/// </summary>
[HarmonyPatch]
public static class GameOptionsHeaderPatches
{
    // Added to the vanilla width, on the right (MiraAPI added 1.5).
    private const float ExtraWidth = 1.8f;

    // Space between the right edge of the hint and the end of the header, in background units.
    private const float HintPadding = 0.4f;

    // Space kept between the end of the title and the hint.
    private const float TitleHintGap = 0.1f;

    // Height of the first group of a page in MiraAPI (GameOptionsMenuPatch.UpdatePatch), and how much lower it goes.
    private const float FirstGroupY = 2.1f;
    private const float FirstGroupOffset = 0.05f;

    /// <summary>
    ///     Name of the copy filling the gap under a header (also marks a header as done).
    /// </summary>
    public const string FillName = "BorHeaderFill";

    private static readonly PropertyInfo HeaderProperty = AccessTools.Property(typeof(AbstractOptionGroup), "Header");

    // Internal class of MiraAPI.
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(MiraPluginManager).Assembly.GetType("MiraAPI.Patches.Options.GameOptionsMenuPatch", true), "UpdateGroup");
    }

    /// <summary>
    ///     MiraAPI puts the first group at the very top of the scrolled area, its divider just above the mask:
    ///     move the page down a little so that it shows.
    /// </summary>
    public static void Prefix(ref float num)
    {
        if (Mathf.Approximately(num, FirstGroupY))
            num -= FirstGroupOffset;
    }

    public static void Postfix(AbstractOptionGroup? group)
    {
        if (group == null || HeaderProperty.GetValue(group) is not CategoryHeaderMasked header || !header || !header.Background ||
            !header.gameObject.activeInHierarchy)
            return;

        var menu = GameSettingMenu.Instance ? GameSettingMenu.Instance.GameSettingsTab : null;
        var origin = menu ? menu!.categoryHeaderOrigin : null;
        if (origin == null || !origin.Background || header.Background.sprite == origin.Background.sprite) return;

        ApplyVanillaLook(header, origin.Background, origin.Background.sprite, GetTint(group.GroupColor), 0f);
    }

    /// <summary>
    ///     Vanilla look for a MiraAPI header: sprite, height and rounded end of the vanilla header, longer on the right,
    ///     divider ending with it, "(click to close / open)" at a fixed distance from the end.
    ///     Also used by the roles tab (<see cref="RoleSettingsHeaderPatches"/>).
    /// </summary>
    /// <param name="header">The MiraAPI header.</param>
    /// <param name="vanilla">Background of the vanilla header (size and place).</param>
    /// <param name="sprite">Sprite to draw: the vanilla one, or its white copy to tint with any color.</param>
    /// <param name="tint">Color of the background and divider.</param>
    /// <param name="offsetX">Horizontal shift from the vanilla place, in background units.</param>
    public static void ApplyVanillaLook(CategoryHeaderMasked header, SpriteRenderer vanilla, Sprite sprite, Color tint, float offsetX)
    {
        var background = header.Background;
        var origin = vanilla.transform.localPosition + new Vector3(offsetX, 0f, 0f);

        // The ends of the vanilla sprite are wider than the vanilla header, so Unity squeezes them: a longer header
        // squeezes them less and changes the rounded end. Vanilla header at its own size, moved to the right, over a
        // copy at the vanilla place filling the gap (same plain color).
        background.sprite = sprite;
        background.drawMode = vanilla.drawMode;
        background.size = vanilla.size;
        background.color = tint;
        background.transform.localPosition = origin + new Vector3(ExtraWidth * vanilla.transform.localScale.x, 0f, 0f);

        var fill = Object.Instantiate(background.gameObject, background.transform.parent);
        fill.name = FillName;
        fill.transform.localPosition = origin + new Vector3(0f, 0f, 0.01f);

        if (header.Divider)
        {
            header.Divider.color = tint;
            FitDivider(header.Divider, background.bounds.max.x);
        }

        // "(click to close / open)": its right edge at a fixed distance from the end of the header (pivot on the right).
        var hint = header.GetComponentsInChildren<TextMeshPro>(true).FirstOrDefault(text => text != header.Title);
        if (hint == null) return;

        var pivotX = vanilla.sprite.pivot.x / vanilla.sprite.rect.width;
        var right = background.transform.TransformPoint(new Vector3((vanilla.size.x * (1f - pivotX)) - HintPadding, 0f, 0f));
        var rect = hint.rectTransform;
        var localRight = rect.parent.InverseTransformPoint(right);
        rect.pivot = new Vector2(1f, rect.pivot.y);
        hint.horizontalAlignment = HorizontalAlignmentOptions.Right;
        hint.enableWordWrapping = false;
        hint.overflowMode = TextOverflowModes.Overflow;
        rect.localPosition = new Vector3(localRight.x, rect.localPosition.y, rect.localPosition.z);
        FitTitle(header.Title, hint);
    }

    /// <summary>
    ///     A long title (French "Attribution des rôles") would run under the hint: its font is reduced to end before it.
    /// </summary>
    private static void FitTitle(TextMeshPro title, TextMeshPro hint)
    {
        if (!title || string.IsNullOrEmpty(title.text)) return;

        title.ForceMeshUpdate();
        hint.ForceMeshUpdate();
        var titleLeft = title.transform.TransformPoint(title.textBounds.min).x;
        var titleRight = title.transform.TransformPoint(title.textBounds.max).x;
        var hintLeft = hint.transform.TransformPoint(hint.textBounds.min).x - TitleHintGap;
        if (titleRight <= hintLeft || titleRight <= titleLeft) return;

        var factor = Mathf.Max(0.5f, (hintLeft - titleLeft) / (titleRight - titleLeft));
        title.enableAutoSizing = false;
        title.fontSize *= factor;
    }

    /// <summary>
    ///     The vanilla divider goes up to the scroll bar: same left edge, ending with the header.
    /// </summary>
    private static void FitDivider(SpriteRenderer divider, float right)
    {
        var bounds = divider.bounds;
        if (bounds.size.x <= 0f) return;

        var ratio = (right - bounds.min.x) / bounds.size.x;
        var transform = divider.transform;
        transform.localScale = new Vector3(transform.localScale.x * ratio, transform.localScale.y, transform.localScale.z);
        transform.position += new Vector3(bounds.min.x - divider.bounds.min.x, 0f, 0f);
    }

    /// <summary>
    ///     MiraAPI tints its white sprite with the group color (77 gray by default), the vanilla sprites are already
    ///     that gray: tint relative to it (white for the default color, the vanilla look).
    /// </summary>
    private static Color GetTint(Color color)
    {
        var gray = MiraAPI.MiraApiPlugin.DefaultHeaderColor;
        return new Color(
            Mathf.Clamp01(color.r / gray.r),
            Mathf.Clamp01(color.g / gray.g),
            Mathf.Clamp01(color.b / gray.b),
            color.a);
    }
}
