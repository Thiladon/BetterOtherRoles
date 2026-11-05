using HarmonyLib;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Text boxes (<see cref="TextBoxTMP"/>) of our menus: a blinking caret for the boxes of MiraAPI prefabs, placed at the
///     left of an empty box and right after a space, and a placeholder shown while a box is empty. Used by the presets
///     popups, the Debug menu search and the role guide search.
/// </summary>
[HarmonyPatch]
public static class TextBoxPatches
{
    // Text boxes given a caret by AddCaret, and placeholders given by AddPlaceholder.
    private static readonly HashSet<IntPtr> CaretBoxes = [];
    private static readonly Dictionary<IntPtr, TextMeshPro> Placeholders = [];

    // Caret text, around the caret position given by the game: size, then offset (right, down) through its pivot.
    private const float PipeWidth = 0.2f;
    private const float PipeHeight = 0.6f;
    private const float PipeScale = 1.5f;
    private const float PipeRight = 0.04f;
    private const float PipeDown = 0.09f;

    // Caret before the end of a letter, as the game puts it (0.03 to 0.045 in the popup): kept after a space.
    private const float CaretGap = 0.04f;

    /// <summary>
    ///     Gives a blinking caret to a text box of a MiraAPI prefab (they have none), placed right after a space too.
    /// </summary>
    /// <param name="box">The text box.</param>
    public static void AddCaret(TextBoxTMP box)
    {
        if (box.Pipe || !box.outputText) return;

        // Written from the left, the caret starting at the left of the box.
        box.outputText.horizontalAlignment = HorizontalAlignmentOptions.Left;

        var pipe = Object.Instantiate(box.outputText, box.outputText.transform.parent);
        pipe.name = "Pipe";
        pipe.text = "|";
        // Placed by the game at the caret: the "|" is centered on its position.
        pipe.alignment = TextAlignmentOptions.Center;
        pipe.enableAutoSizing = false;
        pipe.fontSize = box.outputText.fontSize * PipeScale;
        pipe.rectTransform.sizeDelta = new Vector2(PipeWidth, PipeHeight);
        pipe.rectTransform.pivot = new Vector2(0.5f - (PipeRight / PipeWidth), 0.5f + (PipeDown / PipeHeight));
        box.Pipe = pipe.GetComponent<MeshRenderer>();
        CaretBoxes.Add(box.Pointer);
    }

    /// <summary>
    ///     Places the caret of a text box that already has one (copy of the chat box): at the left of the box when it is
    ///     empty, and right after a space, as with <see cref="AddCaret"/>.
    /// </summary>
    /// <param name="box">The text box.</param>
    public static void FixCaret(TextBoxTMP box)
    {
        if (box && box.Pipe)
            CaretBoxes.Add(box.Pointer);
    }

    /// <summary>
    ///     Gives a placeholder to a text box: a text in the box, in its font, shown while the box is empty and not
    ///     focused. It is not the text of the box (not searched for instance).
    /// </summary>
    /// <param name="box">The text box.</param>
    /// <param name="placeholder">The text shown.</param>
    /// <param name="color">Its color.</param>
    public static void AddPlaceholder(TextBoxTMP box, string placeholder, Color color)
    {
        if (!box.outputText || Placeholders.ContainsKey(box.Pointer)) return;

        var text = Object.Instantiate(box.outputText, box.outputText.transform.parent);
        text.name = "BorPlaceholder";
        text.text = placeholder;
        text.color = color;
        Placeholders[box.Pointer] = text;
        RefreshPlaceholder(box);
    }

    private static void RefreshPlaceholder(TextBoxTMP box)
    {
        if (!Placeholders.TryGetValue(box.Pointer, out var placeholder)) return;

        if (!placeholder)
        {
            Placeholders.Remove(box.Pointer);
            return;
        }

        var shown = !box.hasFocus && string.IsNullOrEmpty(box.text);
        if (placeholder.gameObject.activeSelf != shown)
            placeholder.gameObject.SetActive(shown);
    }

    // The game puts the caret 0.01 before the right edge of the character before it, which is close to its left edge
    // for a space: right after a space, the caret is put where the next character starts, less the gap left after a
    // letter. With no text, the caret is put at the left of the box (the game centers it).
    // The game places it again in its Update (not through SetPipePosition): placed after both.
    [HarmonyPatch(typeof(TextBoxTMP), "SetPipePosition")]
    [HarmonyPostfix]
    public static void SetPipePositionPostfix(TextBoxTMP __instance)
    {
        PlaceCaret(__instance);
    }

    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.Update))]
    [HarmonyPostfix]
    public static void UpdatePostfix(TextBoxTMP __instance)
    {
        PlaceCaret(__instance);
        RefreshPlaceholder(__instance);
    }

    private static void PlaceCaret(TextBoxTMP box)
    {
        var text = box.outputText;
        var pipe = box.Pipe;
        if (!text || !pipe || !CaretBoxes.Contains(box.Pointer)) return;

        // Height: the game puts the caret at its caret offset when the box has text, but higher or lower when it is
        // empty, and without the offset of the text (raised in the role guide search). Always the caret offset, plus the
        // text offset when both are in the same parent.
        var textOffset = pipe.transform.parent == text.transform.parent ? text.transform.localPosition.y : 0f;
        var y = box.caretYOffset + textOffset;
        var position = pipe.transform.localPosition;
        var x = position.x;

        if (string.IsNullOrEmpty(box.text))
        {
            var left = text.rectTransform.rect.xMin + text.margin.x;
            x = pipe.transform.parent.InverseTransformPoint(text.transform.TransformPoint(new Vector3(left, 0f, 0f))).x;
        }
        else
        {
            var info = text.textInfo;
            var index = box.caretPos - 1;
            if (info != null && index >= 0 && index < info.characterCount && char.IsWhiteSpace(info.characterInfo[index].character))
            {
                var end = pipe.transform.parent.InverseTransformPoint(text.transform.TransformPoint(new Vector3(info.characterInfo[index].xAdvance, 0f, 0f)));
                x = end.x - CaretGap;
            }
        }

        pipe.transform.localPosition = new Vector3(x, y, position.z);
    }
}
