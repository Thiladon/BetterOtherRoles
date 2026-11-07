using BetterOtherRoles.Utilities;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Text selection in a text box (<see cref="TextBoxTMP"/>), which the game does not have, for the boxes given to
///     <see cref="Enable"/> (chat box):
///     <list type="bullet">
///         <item>keyboard: Shift with the arrows, Home and End; Ctrl+A; Ctrl with the arrows moves by word; Home, End;</item>
///         <item>mouse: click places the caret, drag selects, double click selects a word, triple click everything;</item>
///         <item>typing, Backspace, Delete and Ctrl+V replace the selection; Delete also removes the character after the caret.</item>
///     </list>
///     The selection is shown with a TMP mark tag in the shown text (the text of the box is not changed). The caret is
///     also placed on its line when the text has more than two lines (the game only handles two, with 100 characters).
/// </summary>
[HarmonyPatch]
public static class TextSelectionPatches
{
    private const string MarkStart = "<mark=#3B82F670>";
    private const string MarkEnd = "</mark>";
    private const float DoubleClickTime = 0.35f;

    // Added to the repeat timer of the game while an arrow is held: 0.2 s between two moves becomes 0.2 / 1.6 = 0.125 s,
    // and 0.2 / 4 = 0.05 s with Shift (selection).
    private const float RepeatSpeedUp = 0.6f;
    private const float SelectRepeatSpeedUp = 3f;

    // Game caret: 0.01 before the edge of the character before it.
    private const float CaretGap = 0.01f;

    private static readonly Dictionary<IntPtr, State> States = [];

    /// <summary>
    ///     Gives the text selection to a text box.
    /// </summary>
    /// <param name="box">The text box.</param>
    /// <param name="area">
    ///     The area of the box for the mouse, when the box has no collider (chat box: its background), or null.
    /// </param>
    public static void Enable(TextBoxTMP box, SpriteRenderer? area = null)
    {
        if (!box || States.ContainsKey(box.Pointer)) return;

        // The rich text shows the selection mark; the text typed is shown as is (Plain), "<" can be typed in the chat.
        if (box.outputText)
            box.outputText.richText = true;
        States[box.Pointer] = new State { Area = area };
    }

    /// <summary>
    ///     Gets the selected text of a text box.
    /// </summary>
    /// <param name="box">The text box.</param>
    /// <param name="selected">The selected text.</param>
    /// <returns>True if some text is selected.</returns>
    public static bool TryGetSelection(TextBoxTMP box, out string selected)
    {
        selected = string.Empty;
        if (!States.TryGetValue(box.Pointer, out var state) || !TryGetRange(box, state, out var start, out var end)) return false;

        selected = box.text[start..end];
        return true;
    }

    /// <summary>
    ///     Removes the selected text of a text box, the caret where it was.
    /// </summary>
    /// <param name="box">The text box.</param>
    /// <returns>True if some text was selected.</returns>
    public static bool DeleteSelection(TextBoxTMP box)
    {
        if (!States.TryGetValue(box.Pointer, out var state) || !TryGetRange(box, state, out var start, out var end)) return false;

        // SetText moves the caret by the change of length: from the end of the selection to its start.
        box.caretPos = end;
        box.SetText(box.text.Remove(start, end - start));
        state.Anchor = -1;
        return true;
    }

    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.Update))]
    [HarmonyPrefix]
    public static bool UpdatePrefix(TextBoxTMP __instance)
    {
        if (!States.TryGetValue(__instance.Pointer, out var state)) return true;
        if (!__instance.enabled || !__instance.hasFocus) return true;

        var text = __instance.text ?? string.Empty;
        var shift = IsShiftHeld();
        state.SavedCaret = Mathf.Clamp(__instance.caretPos, 0, text.Length);
        state.HadSelection = TryGetRange(__instance, state, out state.SavedStart, out state.SavedEnd);
        state.ArrowHeld = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow);
        state.Edited = false;

        // Arrow held: the game moves the caret every 0.2 s, faster here, even more with Shift.
        if (state.ArrowHeld)
            __instance.caretRepeatTimer += Time.deltaTime * (shift ? SelectRepeatSpeedUp : RepeatSpeedUp);

        var navigation = state.ArrowHeld || Input.GetKeyDown(KeyCode.Home) || Input.GetKeyDown(KeyCode.End);
        if (shift && navigation && state.Anchor < 0)
            state.Anchor = state.SavedCaret;

        // Typed characters and Backspace ("\b") replace the selection, not the control characters given with Ctrl.
        var typed = Input.inputString.Where(x => x == '\b' || !char.IsControl(x)).ToArray();
        var paste = __instance.AllowPaste && IsControlHeld() && Input.GetKeyDown(KeyCode.V) && !string.IsNullOrEmpty(GUIUtility.systemCopyBuffer);
        var delete = Input.GetKeyDown(KeyCode.Delete);

        if (state.HadSelection && (typed.Length > 0 || paste || delete))
        {
            DeleteSelection(__instance);
            state.Edited = true;

            // The game would remove one more character for the Backspace.
            if (typed.Length > 0 && typed.All(x => x == '\b')) return false;
        }
        else if (delete && state.SavedCaret < text.Length)
        {
            // The character after the caret: SetText moves the caret back by one, so it is put after it first.
            __instance.caretPos = state.SavedCaret + 1;
            __instance.SetText(text.Remove(state.SavedCaret, 1));
            state.Edited = true;
        }

        return true;
    }

    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.Update))]
    [HarmonyPostfix]
    public static void UpdatePostfix(TextBoxTMP __instance)
    {
        if (!States.TryGetValue(__instance.Pointer, out var state)) return;

        if (!__instance.enabled || !__instance.hasFocus)
        {
            state.Anchor = -1;
            state.Dragging = false;
            RefreshMark(__instance, state);
            return;
        }

        var text = __instance.text ?? string.Empty;
        var length = text.Length;
        var caret = Mathf.Clamp(__instance.caretPos, 0, length);
        var shift = IsShiftHeld();
        var control = IsControlHeld();
        var navigation = false;

        // Arrows: moved by one by the game, its move is replaced by a move by word (Ctrl), or by the side of the
        // selection (without Shift).
        var direction = GetArrowDirection(state, caret);
        if (direction != 0 && !state.Edited)
        {
            navigation = true;
            if (state.HadSelection && !shift)
                caret = direction < 0 ? state.SavedStart : state.SavedEnd;
            else if (control)
                caret = direction < 0 ? WordLeft(text, state.SavedCaret) : WordRight(text, state.SavedCaret);
        }

        if (Input.GetKeyDown(KeyCode.Home))
        {
            caret = 0;
            navigation = true;
        }
        else if (Input.GetKeyDown(KeyCode.End))
        {
            caret = length;
            navigation = true;
        }

        if (navigation && !shift)
            state.Anchor = -1;

        // Read every frame (BorLetterKeys), then with Ctrl.
        var selectAll = BorLetterKeys.GetKeyDown('A');
        if (control && selectAll)
        {
            state.Anchor = 0;
            caret = length;
        }

        caret = HandleMouse(__instance, state, text, caret, shift);

        if (state.Anchor == caret && !state.Dragging)
            state.Anchor = -1;

        if (caret != __instance.caretPos)
        {
            __instance.caretPos = caret;
            __instance.pipeBlinkTimer = 0f;
        }

        RefreshMark(__instance, state);
        PlaceCaret(__instance, state);
    }

    // First click on the box: the game gives the focus with the caret at the end, put where the box was clicked.
    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.GiveFocus))]
    [HarmonyPrefix]
    public static void GiveFocusPrefix(TextBoxTMP __instance, out bool __state)
    {
        __state = __instance.hasFocus;
    }

    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.GiveFocus))]
    [HarmonyPostfix]
    public static void GiveFocusPostfix(TextBoxTMP __instance, bool __state)
    {
        if (__state || !__instance.hasFocus || !States.TryGetValue(__instance.Pointer, out var state)) return;
        if (!Input.GetMouseButton(0) && !Input.GetMouseButtonUp(0)) return;

        var world = MouseWorld();
        if (!IsOver(__instance, state, world)) return;

        var index = IndexAt(__instance, world);
        __instance.caretPos = index;
        state.Anchor = index;
        state.Dragging = Input.GetMouseButton(0);
        state.LastClickTime = Time.unscaledTime;
        state.LastClickIndex = index;
        state.ClickCount = 1;
    }

    // A click in the box takes its focus away (PassiveButtonManager: the button of the chat box is in front of it), and
    // gives it back when released, with the caret at the end: a drag or a double click could not select. The box keeps
    // the focus when it is clicked.
    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.LoseFocus))]
    [HarmonyPrefix]
    public static bool LoseFocusPrefix(TextBoxTMP __instance)
    {
        if (!__instance.hasFocus || !States.TryGetValue(__instance.Pointer, out var state)) return true;

        return !Input.GetMouseButton(0) || !IsOver(__instance, state, MouseWorld());
    }

    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.LoseFocus))]
    [HarmonyPostfix]
    public static void LoseFocusPostfix(TextBoxTMP __instance)
    {
        // Harmony runs the postfix even when the prefix kept the focus (click or drag in the box): nothing lost.
        if (__instance.hasFocus || !States.TryGetValue(__instance.Pointer, out var state)) return;

        state.Anchor = -1;
        state.Dragging = false;
        RefreshMark(__instance, state);
    }

    // A text box whose Start has not run yet has no collider list: the game threw a NullReferenceException when it
    // checked a click on it (start of a game). Not hit, without calling the game.
    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.CheckCollision))]
    [HarmonyPrefix]
    public static bool CheckCollisionPrefix(TextBoxTMP __instance, ref bool __result)
    {
        if (__instance.colliders != null) return true;

        __result = false;
        return false;
    }

    // The box keeps the focus when it is clicked (the chat box has no collider: the game would take the focus away
    // then give it back with the caret at the end).
    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.CheckCollision))]
    [HarmonyPostfix]
    public static void CheckCollisionPostfix(TextBoxTMP __instance, Vector2 pt, ref bool __result)
    {
        if (__result || !States.TryGetValue(__instance.Pointer, out var state)) return;

        __result = IsInArea(state, pt);
    }

    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.SetText))]
    [HarmonyPrefix]
    public static void SetTextPrefix(TextBoxTMP __instance, out string __state)
    {
        __state = __instance.text ?? string.Empty;
    }

    // A new text (typed, sent, history): no selection. The game also calls SetText for a refused character (Ctrl+A
    // gives one): the selection is kept when the text did not change.
    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.SetText))]
    [HarmonyPostfix]
    public static void SetTextPostfix(TextBoxTMP __instance, string __state)
    {
        if (!States.TryGetValue(__instance.Pointer, out var state)) return;

        if ((__instance.text ?? string.Empty) != __state)
        {
            state.Anchor = -1;
            state.Dragging = false;
        }

        RefreshMark(__instance, state);
        PlaceCaret(__instance, state);
    }

    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.OnDestroy))]
    [HarmonyPostfix]
    public static void OnDestroyPostfix(TextBoxTMP __instance)
    {
        States.Remove(__instance.Pointer);
    }

    // -1 or 1: an arrow pressed, or the move of the game while it is held; 0: none.
    private static int GetArrowDirection(State state, int caret)
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow)) return -1;
        if (Input.GetKeyDown(KeyCode.RightArrow)) return 1;
        if (state.ArrowHeld && !state.Edited) return Math.Sign(caret - state.SavedCaret);
        return 0;
    }

    private static int HandleMouse(TextBoxTMP box, State state, string text, int caret, bool shift)
    {
        if (Input.GetMouseButtonDown(0))
        {
            var world = MouseWorld();
            if (!IsOver(box, state, world)) return caret;

            var index = IndexAt(box, world);
            var again = Time.unscaledTime - state.LastClickTime < DoubleClickTime && Math.Abs(index - state.LastClickIndex) <= 1;
            state.ClickCount = again ? state.ClickCount + 1 : 1;
            state.LastClickTime = Time.unscaledTime;
            state.LastClickIndex = index;

            switch (state.ClickCount)
            {
                case 2:
                    WordAt(text, index, out var start, out var end);
                    state.Anchor = start;
                    state.Dragging = false;
                    return end;
                case >= 3:
                    state.Anchor = 0;
                    state.Dragging = false;
                    return text.Length;
            }

            if (!shift || state.Anchor < 0)
                state.Anchor = shift ? caret : index;
            state.Dragging = true;
            return index;
        }

        if (!state.Dragging) return caret;

        if (!Input.GetMouseButton(0))
        {
            state.Dragging = false;
            return caret;
        }

        return IndexAt(box, MouseWorld());
    }

    private static bool TryGetRange(TextBoxTMP box, State state, out int start, out int end)
    {
        var length = box.text?.Length ?? 0;
        var anchor = Math.Min(state.Anchor, length);
        var caret = Mathf.Clamp(box.caretPos, 0, length);
        start = Math.Min(anchor, caret);
        end = Math.Max(anchor, caret);
        return state.Anchor >= 0 && start != end;
    }

    // Typed text shown as is: its tags are not applied, and each character stays at its index (caret, selection).
    private static string Plain(string text)
    {
        return text.Contains('<') ? $"<noparse>{text.Replace("</noparse>", string.Empty, StringComparison.OrdinalIgnoreCase)}</noparse>" : text;
    }

    // Shown text: the text of the box, with the selection marked. Left to the game without a mark nor a tag to escape
    // (it may show a placeholder).
    private static void RefreshMark(TextBoxTMP box, State state)
    {
        var output = box.outputText;
        if (!output) return;

        var text = box.text ?? string.Empty;
        string shown;
        if (box.hasFocus && TryGetRange(box, state, out var start, out var end))
        {
            shown = Plain(text[..start]) + MarkStart + Plain(text[start..end]) + MarkEnd + Plain(text[end..]);
            state.Marked = true;
        }
        else if (state.Marked || text.Contains('<'))
        {
            shown = Plain(text);
            state.Marked = false;
        }
        else
        {
            return;
        }

        if (output.text == shown) return;

        output.text = shown;
        output.ForceMeshUpdate(true, true);
    }

    // The game puts the caret at the same height from the third line on: put on the line of the character before it,
    // at the height the game gives a single line (measured on a single line, or the middle of the text).
    private static void PlaceCaret(TextBoxTMP box, State state)
    {
        var pipe = box.Pipe;
        var output = box.outputText;
        var info = output ? output.textInfo : null;
        if (!pipe || info == null || info.lineCount == 0 || info.characterCount == 0) return;

        var caret = Mathf.Clamp(box.caretPos, 0, info.characterCount);
        if (info.lineCount == 1)
            state.Calibration = box.caretYOffset - LineMiddle(pipe, output!, info, 0);

        var calibration = state.Calibration
            ?? box.caretYOffset - ((LineMiddle(pipe, output!, info, 0) + LineMiddle(pipe, output!, info, info.lineCount - 1)) / 2f);

        var line = 0;
        var x = info.characterInfo[0].topLeft.x - CaretGap;
        if (caret > 0)
        {
            line = info.characterInfo[caret - 1].lineNumber;
            x = info.characterInfo[caret - 1].topRight.x - CaretGap;
        }

        pipe.transform.localPosition = new Vector3(x, LineMiddle(pipe, output!, info, line) + calibration, pipe.transform.localPosition.z);
    }

    // Middle of a line, in the space of the caret (the text can be moved: ChatInputPatches.AlignText).
    private static float LineMiddle(MeshRenderer pipe, TextMeshPro output, TMP_TextInfo info, int line)
    {
        var lineInfo = info.lineInfo[line];
        var middle = new Vector3(0f, (lineInfo.ascender + lineInfo.descender) / 2f, 0f);
        return pipe.transform.parent.InverseTransformPoint(output.transform.TransformPoint(middle)).y;
    }

    // Index of the caret nearest to a point: its line, then the character whose middle is after it.
    private static int IndexAt(TextBoxTMP box, Vector3 world)
    {
        var length = box.text?.Length ?? 0;
        var output = box.outputText;
        var info = output ? output.textInfo : null;
        if (length == 0 || info == null || info.characterCount == 0 || info.lineCount == 0) return 0;

        var local = output!.transform.InverseTransformPoint(world);
        var line = info.lineCount - 1;
        for (var i = 0; i < info.lineCount; i++)
        {
            if (local.y < info.lineInfo[i].descender) continue;

            line = i;
            break;
        }

        var lineInfo = info.lineInfo[line];
        var last = Math.Min(lineInfo.lastCharacterIndex, info.characterCount - 1);
        for (var i = lineInfo.firstCharacterIndex; i <= last; i++)
        {
            var character = info.characterInfo[i];
            if (local.x < (character.origin + character.xAdvance) / 2f) return Math.Min(i, length);
        }

        return Math.Min(last + 1, length);
    }

    private static bool IsOver(TextBoxTMP box, State state, Vector3 world)
    {
        return box.CheckCollision(world) || IsInArea(state, world);
    }

    private static bool IsInArea(State state, Vector2 point)
    {
        if (!state.Area || !state.Area!.isVisible) return false;

        var bounds = state.Area.bounds;
        return point.x >= bounds.min.x && point.x <= bounds.max.x && point.y >= bounds.min.y && point.y <= bounds.max.y;
    }

    private static Vector3 MouseWorld()
    {
        var camera = Camera.main;
        return camera ? camera.ScreenToWorldPoint(Input.mousePosition) : Vector3.zero;
    }

    private static int WordLeft(string text, int index)
    {
        while (index > 0 && char.IsWhiteSpace(text[index - 1])) index--;
        while (index > 0 && !char.IsWhiteSpace(text[index - 1])) index--;
        return index;
    }

    private static int WordRight(string text, int index)
    {
        while (index < text.Length && !char.IsWhiteSpace(text[index])) index++;
        while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
        return index;
    }

    // The word (or the spaces) at an index: the characters of the same kind around it.
    private static void WordAt(string text, int index, out int start, out int end)
    {
        start = end = Mathf.Clamp(index, 0, text.Length);
        if (text.Length == 0) return;

        var space = char.IsWhiteSpace(text[Math.Min(start, text.Length - 1)]);
        while (start > 0 && char.IsWhiteSpace(text[start - 1]) == space) start--;
        while (end < text.Length && char.IsWhiteSpace(text[end]) == space) end++;
    }

    private static bool IsShiftHeld() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

    private static bool IsControlHeld() => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

    private sealed class State
    {
        public SpriteRenderer? Area;
        public int Anchor = -1;
        public bool Dragging;
        public bool Marked;
        public float? Calibration;

        public int SavedCaret;
        public bool HadSelection;
        public int SavedStart;
        public int SavedEnd;
        public bool ArrowHeld;
        public bool Edited;

        public float LastClickTime = -1f;
        public int LastClickIndex;
        public int ClickCount;
    }
}
