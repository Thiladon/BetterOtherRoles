using BetterOtherRoles.Utilities;
using HarmonyLib;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Chat box comfort (Town of Us): a limit of <see cref="CharacterLimit"/> characters instead of 100, with a counter
///     colored as the limit gets near; Ctrl+C copies the selection (<see cref="TextSelectionPatches"/>) or the whole text,
///     Ctrl+X cuts it, Ctrl+V pastes (game); Up and Down go through the last <see cref="HistorySize"/> messages sent during
///     the session (commands included). The keys only act while the chat box has the focus. Words longer than a line are
///     cut instead of shrunk, in the box and in the chat bubbles.
/// </summary>
[HarmonyPatch]
public static class ChatInputPatches
{
    /// <summary>
    ///     Gets the number of characters a message can have.
    /// </summary>
    public const int CharacterLimit = 300;

    private const int HistorySize = 20;

    // Messages sent, the oldest first, and the one shown in the box (History.Count: none, the box as typed).
    private static readonly List<string> History = [];
    private static int _historyIndex;

    // Place of the text in the chat box given by the game (AlignText).
    private static float? _textY;

    // Growth of the chat box the messages were placed for (AlignAllBubblesPostfix).
    private static float _growth;

    // Values of the game: height of the box on a single line, space between two messages, margin of the scrolling.
    private const float SingleLineHeight = 0.62f;
    private const float BubbleSpacing = 0.15f;
    private const float ScrollMargin = -0.3f;

    /// <summary>
    ///     Keeps a message sent from the chat box, for Up and Down. Called by <see cref="Modules.Commands.BorCommandManager"/>
    ///     before a command or a message is sent.
    /// </summary>
    /// <param name="text">The text of the box.</param>
    public static void AddToHistory(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return;

        if (History.Count == 0 || History[^1] != text)
        {
            History.Add(text);
            if (History.Count > HistorySize)
                History.RemoveAt(0);
        }

        _historyIndex = History.Count;
    }

    [HarmonyPatch(typeof(FreeChatInputField), nameof(FreeChatInputField.Awake))]
    [HarmonyPostfix]
    public static void AwakePostfix(FreeChatInputField __instance)
    {
        __instance.textArea.characterLimit = CharacterLimit;
        __instance.textArea.AllowPaste = true;
        CutLongWords(__instance.textArea.outputText);
        TextSelectionPatches.Enable(__instance.textArea, __instance.Background);
        _textY = null;
        _growth = 0f;
        __instance.UpdateCharCount();
    }

    [HarmonyPatch(typeof(ChatBubble), nameof(ChatBubble.SetText))]
    [HarmonyPrefix]
    public static void BubbleSetTextPrefix(ChatBubble __instance)
    {
        CutLongWords(__instance.TextArea);
    }

    // A word longer than the line is shrunk by the auto size of TMP instead of being cut (tiny text after ~45 letters
    // with no space): without the auto size, TMP cuts it at the end of the line. The text keeps its biggest size.
    private static void CutLongWords(TMPro.TextMeshPro? text)
    {
        if (!text || !text!.enableAutoSizing) return;

        text.enableAutoSizing = false;
        text.fontSize = text.fontSizeMax;
    }

    // The text box is made for two lines (the auto size shrank more): from the third one, the text goes out above the
    // background the game grows for it. With two lines or more, the text is centered in the background (a single line
    // stays where the game puts it).
    private static void AlignText(FreeChatInputField field)
    {
        var output = field.textArea.outputText;
        var background = field.Background;
        var info = output ? output.textInfo : null;
        if (!background || info == null) return;

        _textY ??= output!.transform.localPosition.y;
        var position = output!.transform.localPosition;
        output.transform.localPosition = new Vector3(position.x, _textY.Value, position.z);
        if (info.lineCount < 2 || info.characterCount == 0) return;

        var top = output.transform.TransformPoint(new Vector3(0f, info.lineInfo[0].ascender, 0f)).y;
        var bottom = output.transform.TransformPoint(new Vector3(0f, info.lineInfo[info.lineCount - 1].descender, 0f)).y;
        output.transform.position += new Vector3(0f, background.bounds.center.y - ((top + bottom) / 2f), 0f);
    }

    // The game writes "n/100" and its colors (yellow from 75): the real limit, yellow, orange then red near it.
    [HarmonyPatch(typeof(FreeChatInputField), nameof(FreeChatInputField.UpdateCharCount))]
    [HarmonyPostfix]
    public static void UpdateCharCountPostfix(FreeChatInputField __instance)
    {
        var counter = __instance.charCountText;
        if (!counter || __instance.textArea == null) return;

        var length = __instance.textArea.text.Length;
        var limit = __instance.textArea.characterLimit;
        counter.text = $"{length}/{limit}";
        counter.color = GetCounterColor(length, limit);
        AlignText(__instance);
        FitButton(__instance);

        // The messages go up with the box: placed again when its height changed.
        var growth = GetGrowth(__instance);
        if (Mathf.Approximately(growth, _growth)) return;

        _growth = growth;
        var chat = HudManager.InstanceExists ? HudManager.Instance.Chat : null;
        if (chat && chat!.freeChatField == __instance)
            chat.AlignAllBubbles();
    }

    // The game places the messages above a box of a single line, the box grows over them: they are raised by its growth
    // (in the space of the messages), the top of the list as much (scrolling bounds as computed by the game).
    [HarmonyPatch(typeof(ChatController), nameof(ChatController.AlignAllBubbles))]
    [HarmonyPostfix]
    public static void AlignAllBubblesPostfix(ChatController __instance)
    {
        var field = __instance.freeChatField;
        var inner = __instance.scroller ? __instance.scroller.Inner : null;
        if (!field || !inner) return;

        var growth = GetGrowth(field) * field.transform.lossyScale.y / inner!.lossyScale.y;
        if (growth <= 0f) return;

        var height = 0f;
        foreach (var child in __instance.chatBubblePool.activeChildren)
        {
            var bubble = child.TryCast<ChatBubble>();
            if (!bubble) continue;

            var position = bubble!.transform.localPosition;
            bubble.transform.localPosition = new Vector3(position.x, position.y + growth, position.z);
            height += bubble.Background.size.y + BubbleSpacing;
        }

        var hitbox = __instance.scroller.Hitbox.bounds.size.y;
        __instance.scroller.SetYBoundsMin(Mathf.Min(0f, -(height - BubbleSpacing) - growth + hitbox + ScrollMargin));
    }

    // Height of the box above the one of a single line (FreeChatInputField.UpdateCharCount).
    private static float GetGrowth(FreeChatInputField field)
    {
        var background = field.Background;
        return background ? Mathf.Max(0f, background.size.y - SingleLineHeight) : 0f;
    }

    // The button of the chat box (hover in green, click) keeps the height of a single line: given the height of the
    // background, which grows with the text. Its width stays (the Send button is in the background, on the right).
    private static void FitButton(FreeChatInputField field)
    {
        var button = field.fieldButton;
        var background = field.Background;
        if (!button || !background) return;

        var bounds = background.bounds;
        foreach (var collider in button.Colliders)
        {
            var box = collider ? collider.TryCast<BoxCollider2D>() : null;
            if (!box) continue;

            var transform = box!.transform;
            var min = transform.InverseTransformPoint(bounds.min);
            var max = transform.InverseTransformPoint(bounds.max);
            box.size = new Vector2(box.size.x, Mathf.Abs(max.y - min.y));
            box.offset = new Vector2(box.offset.x, (min.y + max.y) / 2f);
        }
    }

    private static Color GetCounterColor(int length, int limit)
    {
        if (length >= limit) return Color.red;
        if (length >= limit * 5 / 6) return new Color(1f, 0.5f, 0f, 1f);
        if (length >= limit * 3 / 4) return new Color(1f, 1f, 0f, 1f);
        return Color.black;
    }

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
    [HarmonyPostfix]
    public static void ChatUpdatePostfix(ChatController __instance)
    {
        var field = __instance.freeChatField;
        var box = field ? field.textArea : null;
        if (box == null || !box.enabled || !box.hasFocus) return;

        // With the keyboard layout (BorLetterKeys), read every frame.
        var copy = BorLetterKeys.GetKeyDown('C');
        var cut = BorLetterKeys.GetKeyDown('X');
        var control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (control && copy)
        {
            var copied = TextSelectionPatches.TryGetSelection(box, out var selected) ? selected : box.text;
            if (copied.Length > 0)
                GUIUtility.systemCopyBuffer = copied;
        }
        else if (control && cut)
        {
            if (TextSelectionPatches.TryGetSelection(box, out var selected))
            {
                GUIUtility.systemCopyBuffer = selected;
                TextSelectionPatches.DeleteSelection(box);
                return;
            }

            if (box.text.Length == 0) return;

            GUIUtility.systemCopyBuffer = box.text;
            box.Clear();
        }
        else if (Input.GetKeyDown(KeyCode.UpArrow) && History.Count > 0)
        {
            ShowHistory(box, Math.Max(_historyIndex - 1, 0));
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow) && _historyIndex < History.Count)
        {
            ShowHistory(box, _historyIndex + 1);
        }
    }

    private static void ShowHistory(TextBoxTMP box, int index)
    {
        _historyIndex = index;

        // Cleared first: the caret ends after the text.
        box.Clear();
        if (index < History.Count)
            box.SetText(History[index]);
    }
}
