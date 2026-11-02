using System.Collections;
using BetterOtherRoles.Modules.Presets;
using BetterOtherRoles.Utilities;
using HarmonyLib;
using MiraAPI.Translation;
using Reactor.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.Options;

/// <summary>
///     Popups of the presets tab, copies of the MiraAPI "save preset" popup (<see cref="SavePresetPopup"/>): the name to
///     save (filled with the selected preset), a question (name box and its icon replaced by the question, buttons
///     "Yes" / "No"), a message (button "Close" only), or a name under a message (buttons "Validate" / "Cancel", the
///     name box shakes when the name is refused).
/// </summary>
[HarmonyPatch]
public static class PresetPopups
{
    // Text boxes given a caret by AddCaret, and placeholders given by AddPlaceholder.
    private static readonly HashSet<IntPtr> CaretBoxes = [];
    private static readonly Dictionary<IntPtr, TextMeshPro> Placeholders = [];

    // Font of the MiraAPI popup texts (set in its Awake).
    private const string FontStart = "<font=\"LiberationSans SDF\" material=\"LiberationSans SDF RadialMenu Material\">";
    private const string FontEnd = "</font>";

    // Message in place of the name box (TextboxHolder at y 1.5 in the popup), in the width of the glass.
    private const float MessageY = 1.55f;
    private const float MessageWidth = 5.8f;
    private const float MessageHeight = 1.4f;
    private const float MessageScale = 1.8f;

    // Message above the name box, when both are shown, and error under it.
    private const float MessageAboveBoxY = 2.4f;
    private const float MessageAboveBoxHeight = 0.8f;
    private const float ErrorY = 0.95f;
    private const float ErrorScale = 1.1f;
    private const string ErrorColor = "#FF7070";

    // Share of the background height cut at the top (dark band above the frame, 20 px out of 396 on screen).
    private const float BackgroundTopCut = 0.05f;

    // Depth of the glass: in front of the background (0), behind the name box, texts and buttons (-1).
    private const float GlassZ = -0.5f;

    // The glass overflows the blue (100 units per pixel on screen): 1 px on the left, top and right, 2 px at the bottom.
    private const float GlassTrimX = 0.02f;
    private const float GlassTrimTop = 0.01f;
    private const float GlassTrimBottom = 0.02f;

    // Grey ring between the blue and the dark inner outline of the frame: filled with the blue, up to the outline
    // (pixels darker than this).
    private const byte OutlineBrightness = 40;

    // Rounded corners of the blue (100 units per pixel on screen: 15 px).
    private const float CornerRadius = 0.15f;

    // Caret text, around the caret position given by the game: size, then offset (right, down) through its pivot.
    private const float PipeWidth = 0.2f;
    private const float PipeHeight = 0.6f;
    private const float PipeScale = 1.5f;
    private const float PipeRight = 0.04f;
    private const float PipeDown = 0.09f;

    // Caret before the end of a letter, as the game puts it (0.03 to 0.045 in the popup): kept after a space.
    private const float CaretGap = 0.04f;

    private static Sprite? _background;

    // The name box shakes when the name is refused.
    private const float ShakeDuration = 0.35f;
    private const float ShakeDistance = 0.12f;
    private const float ShakeSpeed = 60f;

    /// <summary>
    ///     Asks for a name (under a message, when not empty), with "Validate" and "Cancel": a refused name shakes the
    ///     name box, shows an error under it and keeps the popup open.
    /// </summary>
    public static void AskValidName(string message, string name, Func<string, bool> isValid, Action<string> onValidate)
    {
        var popup = Create(_ => { });
        if (!popup)
        {
            return;
        }

        var box = popup!.GetComponentInChildren<TextBoxTMP>(true);
        var holder = popup.transform.Find("TextboxHolder");
        var save = popup.transform.Find("SaveButton")?.GetComponent<PassiveButton>();
        var close = popup.transform.Find("CloseButton")?.GetComponent<PassiveButton>();
        if (!box || !holder || !save || !close)
        {
            return;
        }

        if (name.Length > 0)
        {
            box!.SetText(name.ToUpperInvariant(), string.Empty);
        }

        SetButtonText(popup, "SaveButton", Get("Validate", "Validate"));
        SetButtonText(popup, "CloseButton", Get("Cancel", "Cancel"));
        var text = message.Length > 0 ? AddMessage(popup, message) : null;
        if (text)
        {
            text!.transform.localPosition = new Vector3(0f, MessageAboveBoxY, -1f);
            text.rectTransform.sizeDelta = new Vector2(MessageWidth, MessageAboveBoxHeight);
        }

        var error = AddMessage(popup, $"<color={ErrorColor}>{Get("InvalidName", "Invalid or already used name")}</color>", ErrorScale);
        if (error)
        {
            error!.transform.localPosition = new Vector3(0f, ErrorY, -1f);
            error.rectTransform.sizeDelta = new Vector2(MessageWidth, MessageAboveBoxHeight);
            error.gameObject.SetActive(false);
        }

        // Replaces the MiraAPI action (which always closes the popup).
        var origin = holder!.localPosition;
        save!.OnClick = new Button.ButtonClickedEvent();
        save.OnClick.AddListener((Action)(() =>
        {
            var value = BorPresets.ToPresetName(box!.text);
            if (!isValid(value))
            {
                if (error)
                {
                    error!.gameObject.SetActive(true);
                }

                Coroutines.Start(CoShake(holder, origin));
                return;
            }

            close!.OnClick.Invoke();
            onValidate(value);
        }));
    }

    /// <summary>
    ///     Asks a question; the action runs on "Yes", the other one (when given) on "No", on the next frame.
    /// </summary>
    public static void Confirm(string question, Action onConfirm, Action? onCancel = null)
    {
        var popup = Create(_ => onConfirm());
        if (!popup)
        {
            return;
        }

        SetButtonText(popup!, "SaveButton", Get("Yes", "Yes"));
        SetButtonText(popup!, "CloseButton", Get("No", "No"));
        ShowMessage(popup!, question);

        var save = popup!.transform.Find("SaveButton")?.GetComponent<PassiveButton>();
        var close = popup.transform.Find("CloseButton")?.GetComponent<PassiveButton>();
        if (onCancel == null || !save || !close)
        {
            return;
        }

        // Replaces the MiraAPI action, to tell "Yes" from "No" when the popup closes.
        var confirmed = false;
        save!.OnClick = new Button.ButtonClickedEvent();
        save.OnClick.AddListener((Action)(() =>
        {
            confirmed = true;
            close!.OnClick.Invoke();
            onConfirm();
        }));
        close!.OnClick.AddListener((Action)(() =>
        {
            if (!confirmed)
            {
                Coroutines.Start(CoLater(onCancel));
            }
        }));
    }

    /// <summary>
    ///     Shows a message with the "Close" button only (or another text for it).
    /// </summary>
    public static void Inform(string message, string? closeText = null)
    {
        var popup = Create(_ => { });
        if (!popup)
        {
            return;
        }

        var save = popup!.transform.Find("SaveButton");
        var close = popup.transform.Find("CloseButton");
        if (save && close)
        {
            save!.gameObject.SetActive(false);
            close!.localPosition = new Vector3(0f, close.localPosition.y, close.localPosition.z);
        }

        if (closeText != null)
        {
            SetButtonText(popup, "CloseButton", closeText);
        }

        ShowMessage(popup, message);
    }

    /// <summary>
    ///     Same as <see cref="Inform"/>, on the next frame: when shown from another popup, which closes after its action.
    /// </summary>
    public static void InformLater(string message)
    {
        Coroutines.Start(CoInform(message));
    }

    private static IEnumerator CoInform(string message)
    {
        yield return null;
        Inform(message);
    }

    private static IEnumerator CoShake(Transform holder, Vector3 origin)
    {
        for (var time = 0f; time < ShakeDuration && holder; time += Time.deltaTime)
        {
            var offset = Mathf.Sin(time * ShakeSpeed) * ShakeDistance * (1f - (time / ShakeDuration));
            holder.localPosition = origin + new Vector3(offset, 0f, 0f);
            yield return null;
        }

        if (holder)
        {
            holder.localPosition = origin;
        }
    }

    // The name box and its folder icon (TextboxHolder) are replaced by the message.
    private static void ShowMessage(SavePresetPopup popup, string message)
    {
        var holder = popup.transform.Find("TextboxHolder");
        if (holder && AddMessage(popup, message))
        {
            holder!.gameObject.SetActive(false);
            popup.GetComponentInChildren<TextBoxTMP>(true)?.LoseFocus();
        }
    }

    // White message written like the button texts, in place of the name box.
    private static TextMeshPro? AddMessage(SavePresetPopup popup, string message, float scale = MessageScale)
    {
        var model = popup.transform.Find("SaveButton")?.GetComponentInChildren<TextMeshPro>(true);
        if (!model)
        {
            return null;
        }

        var text = Object.Instantiate(model!, popup.transform);
        text.name = "BorMessage";
        text.transform.localPosition = new Vector3(0f, MessageY, -1f);
        text.transform.localScale = Vector3.one;
        text.text = FontStart + message + FontEnd;
        text.color = Color.white;
        text.enableAutoSizing = false;
        text.fontSize = model!.fontSize * scale;
        text.enableWordWrapping = true;
        text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.sizeDelta = new Vector2(MessageWidth, MessageHeight);
        return text;
    }

    private static void SetButtonText(SavePresetPopup popup, string button, string label)
    {
        var text = popup.transform.Find(button)?.GetComponentInChildren<TextMeshPro>(true);
        if (text)
        {
            text!.text = FontStart + label + FontEnd;
        }
    }

    /// <summary>
    ///     Same as <see cref="Confirm"/>, on the next frame: when asked from another popup, which closes after its action.
    /// </summary>
    public static void ConfirmLater(string question, Action onConfirm, Action? onCancel = null)
    {
        Coroutines.Start(CoLater(() => Confirm(question, onConfirm, onCancel)));
    }

    private static IEnumerator CoLater(Action action)
    {
        yield return null;
        action();
    }

    private static SavePresetPopup? Create(Action<string> onConfirm)
    {
        SavePresetPopup.CreatePopup(onConfirm);

        // CreatePopup puts its holder last under the main camera.
        var camera = Camera.main;
        var holder = camera && camera!.transform.childCount > 0 ? camera.transform.GetChild(camera.transform.childCount - 1) : null;
        var popup = holder ? holder!.GetComponentInChildren<SavePresetPopup>(true) : null;
        if (popup)
        {
            CropBackground(popup!);
            RaiseGlass(popup!);
            SetUpTextBox(popup!);
        }

        return popup;
    }

    // The name box gets a blinking caret and the focus; Enter clicks the confirm button (its current action).
    private static void SetUpTextBox(SavePresetPopup popup)
    {
        var box = popup.GetComponentInChildren<TextBoxTMP>(true);
        var save = popup.transform.Find("SaveButton")?.GetComponent<PassiveButton>();
        if (!box || !save)
        {
            return;
        }

        AddCaret(box!);

        // Spaces and symbols ("-", "_", ".") allowed: MiraAPI removed the spaces from the shown text only.
        box.AllowSymbols = true;

        // Names shown in upper case, saved in lower case (BorPresets.ToPresetName).
        box.ForceUppercase = true;
        box.OnChange = new Button.ButtonClickedEvent();

        box.OnEnter = new Button.ButtonClickedEvent();
        box.OnEnter.AddListener((Action)(() =>
        {
            if (save)
            {
                save!.OnClick.Invoke();
            }
        }));
        box.GiveFocus();
    }

    /// <summary>
    ///     Gives a blinking caret to a text box of a MiraAPI prefab (they have none), placed right after a space too.
    /// </summary>
    /// <param name="box">The text box.</param>
    public static void AddCaret(TextBoxTMP box)
    {
        if (box.Pipe || !box.outputText)
        {
            return;
        }

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
    ///     Gives a placeholder to a text box: a text in the box, in its font, shown while the box is empty and not
    ///     focused. It is not the text of the box (not searched for instance).
    /// </summary>
    /// <param name="box">The text box.</param>
    /// <param name="placeholder">The text shown.</param>
    /// <param name="color">Its color.</param>
    public static void AddPlaceholder(TextBoxTMP box, string placeholder, Color color)
    {
        if (!box.outputText || Placeholders.ContainsKey(box.Pointer))
        {
            return;
        }

        var text = Object.Instantiate(box.outputText, box.outputText.transform.parent);
        text.name = "BorPlaceholder";
        text.text = placeholder;
        text.color = color;
        Placeholders[box.Pointer] = text;
        RefreshPlaceholder(box);
    }

    private static void RefreshPlaceholder(TextBoxTMP box)
    {
        if (!Placeholders.TryGetValue(box.Pointer, out var placeholder))
        {
            return;
        }

        if (!placeholder)
        {
            Placeholders.Remove(box.Pointer);
            return;
        }

        var shown = !box.hasFocus && string.IsNullOrEmpty(box.text);
        if (placeholder.gameObject.activeSelf != shown)
        {
            placeholder.gameObject.SetActive(shown);
        }
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
        if (!text || !pipe || !CaretBoxes.Contains(box.Pointer))
        {
            return;
        }

        if (string.IsNullOrEmpty(box.text))
        {
            var left = text.rectTransform.rect.xMin + text.margin.x;
            var start = pipe.transform.parent.InverseTransformPoint(text.transform.TransformPoint(new Vector3(left, 0f, 0f)));
            var current = pipe.transform.localPosition;
            pipe.transform.localPosition = new Vector3(start.x, current.y, current.z);
            return;
        }

        var info = text.textInfo;
        var index = box.caretPos - 1;
        if (info == null || index < 0 || index >= info.characterCount || !char.IsWhiteSpace(info.characterInfo[index].character))
        {
            return;
        }

        var end = pipe.transform.parent.InverseTransformPoint(text.transform.TransformPoint(new Vector3(info.characterInfo[index].xAdvance, 0f, 0f)));
        var position = pipe.transform.localPosition;
        pipe.transform.localPosition = new Vector3(end.x - CaretGap, position.y, position.z);
    }

    // The glass (light blue with reflections) and the background are at the same depth, so drawn in any order: the
    // glass was sometimes hidden behind the background (dark blue). Put in front of it, and everything else (buttons,
    // icon, name box, texts) in front of the glass.
    private static void RaiseGlass(SavePresetPopup popup)
    {
        var glass = popup.transform.Find("Glass");
        var background = popup.transform.Find("Background")?.GetComponent<SpriteRenderer>();
        var glassRenderer = glass ? glass!.GetComponent<SpriteRenderer>() : null;
        if (!glassRenderer || !background)
        {
            return;
        }

        var size = glassRenderer!.bounds.size;
        var scale = glass!.localScale;
        glass.localScale = new Vector3(scale.x * (1f - (GlassTrimX / size.x)), scale.y * (1f - ((GlassTrimTop + GlassTrimBottom) / size.y)), scale.z);
        glass.localPosition = new Vector3(glass.localPosition.x, glass.localPosition.y + ((GlassTrimBottom - GlassTrimTop) / 2f), GlassZ);
        var order = background!.sortingOrder;
        glassRenderer.sortingOrder = order + 1;
        foreach (var renderer in popup.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != background && renderer != glassRenderer && renderer.sortingOrder < order + 2)
            {
                renderer.sortingOrder = order + 2;
            }
        }
    }

    // Fills with the blue of the center every pixel reached from it before the dark inner outline of the frame, then
    // rounds its corners with the color of the outline.
    private static void FillBlue(Texture2D texture, float radius)
    {
        var width = texture.width;
        var height = texture.height;
        var pixels = texture.GetPixels32();
        var center = ((height / 2) * width) + (width / 2);
        var blue = pixels[center];
        var seen = new bool[pixels.Length];
        var filled = new List<int>();
        var queue = new Queue<int>();
        seen[center] = true;
        queue.Enqueue(center);
        int minX = width, maxX = 0, minY = height, maxY = 0;
        while (queue.Count > 0)
        {
            var index = queue.Dequeue();
            pixels[index] = new Color32(blue.r, blue.g, blue.b, pixels[index].a);
            filled.Add(index);
            minX = Math.Min(minX, index % width);
            maxX = Math.Max(maxX, index % width);
            minY = Math.Min(minY, index / width);
            maxY = Math.Max(maxY, index / width);
            foreach (var next in Neighbours(index, width, height))
            {
                if (!seen[next] && !IsOutline(pixels[next]))
                {
                    seen[next] = true;
                    queue.Enqueue(next);
                }
            }
        }

        // Outside the quarter circles of the corners: color of the outline (left of the blue, middle row).
        var outline = pixels[((height / 2) * width) + Math.Max(0, minX - 2)];
        var maxDistance = radius * radius;
        foreach (var index in filled)
        {
            var x = index % width;
            var y = index / width;
            var dx = x - Mathf.Clamp(x, minX + radius, maxX - radius);
            var dy = y - Mathf.Clamp(y, minY + radius, maxY - radius);
            if ((dx * dx) + (dy * dy) > maxDistance)
            {
                pixels[index] = new Color32(outline.r, outline.g, outline.b, pixels[index].a);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
    }

    private static bool IsOutline(Color32 pixel) => pixel.a < 200 || Math.Max(pixel.r, Math.Max(pixel.g, pixel.b)) < OutlineBrightness;

    private static IEnumerable<int> Neighbours(int index, int width, int height)
    {
        var x = index % width;
        var y = index / width;
        if (x > 0)
        {
            yield return index - 1;
        }

        if (x < width - 1)
        {
            yield return index + 1;
        }

        if (y > 0)
        {
            yield return index - width;
        }

        if (y < height - 1)
        {
            yield return index + width;
        }
    }

    // The background sprite (from the "Download data" task) has a dark band above the frame: cut off. Its blue is
    // spread to the frame (grey ring in between).
    private static void CropBackground(SavePresetPopup popup)
    {
        var renderer = popup.transform.Find("Background")?.GetComponent<SpriteRenderer>();
        if (!renderer || !renderer!.sprite)
        {
            return;
        }

        if (!_background)
        {
            var copy = BorSpriteUtils.ToStandalone(renderer.sprite);
            if (copy == renderer.sprite)
            {
                return;
            }

            // Radius in texture pixels: the sprite is drawn at the renderer width.
            FillBlue(copy.texture, CornerRadius * copy.texture.width / renderer.bounds.size.x);
            var rect = copy.rect;
            var height = Mathf.Round(rect.height * (1f - BackgroundTopCut));
            _background = Sprite.Create(copy.texture, new Rect(rect.x, rect.y, rect.width, height), new Vector2(copy.pivot.x / rect.width, copy.pivot.y / height), copy.pixelsPerUnit);
            _background.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            copy.texture.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        }

        renderer.sprite = _background;

        // Drawn at a set size: shorter by the cut, or the rest is stretched over the whole height.
        if (renderer.drawMode != SpriteDrawMode.Simple)
        {
            renderer.size = new Vector2(renderer.size.x, renderer.size.y * (1f - BackgroundTopCut));
        }
    }

    private static string Get(string key, string fallback) => MiraLocaleManager.Get("BetterOtherRoles.Presets." + key, fallback);
}
