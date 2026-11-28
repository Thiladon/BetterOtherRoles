using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Utilities;

/// <summary>
///     Window in the style of the game, anywhere (main menu included): a copy of its error popup (DisconnectPopup: frame
///     7 × 3.5, close cross, opening and closing animation), without its error behaviour; texts and buttons (copies of
///     its "Okay" button, any width) are added at places in the frame (center 0, 0; edges at ±3.5 and ±1.75).
/// </summary>
public sealed class BorPopup
{
    // Sprites of the "Okay" button are at 84 % (StandardInactive, 3 × 0.7).
    private const float ButtonSpriteScale = 0.84f;

    private readonly GameObject _root;
    private readonly GameObject _buttonModel;
    private readonly TextMeshPro _textModel;

    private BorPopup(GameObject root)
    {
        _root = root;
        _buttonModel = root.transform.Find("OkayButton").gameObject;
        _buttonModel.SetActive(false);
        _textModel = root.transform.Find("Text_TMP").GetComponent<TextMeshPro>();
        _textModel.gameObject.SetActive(false);

        var transition = root.GetComponent<TransitionOpen>();
        transition.OnClose = new Button.ButtonClickedEvent();
        transition.OnClose.AddListener((Action)(() => Object.Destroy(_root)));

        var cross = root.transform.Find("CloseButton")?.GetComponent<PassiveButton>();
        if (cross)
        {
            cross!.OnClick = new Button.ButtonClickedEvent();
            cross.OnClick.AddListener((Action)Close);
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the window is still there.
    /// </summary>
    public bool IsOpen => _root;

    /// <summary>
    ///     Makes a closed window (shown by <see cref="Show"/>), or null when the error popup of the game is not there.
    /// </summary>
    /// <param name="name">The name of the object.</param>
    /// <returns>The window.</returns>
    public static BorPopup? Create(string name)
    {
        if (!DestroyableSingleton<DisconnectPopup>.InstanceExists) return null;

        var original = DestroyableSingleton<DisconnectPopup>.Instance.gameObject;
        var root = Object.Instantiate(original, original.transform.parent);
        root.name = name;
        root.SetActive(false);

        // Without the error behaviour (texts, buttons, controller menu of the error popup).
        Object.DestroyImmediate(root.GetComponent<DisconnectPopup>());
        var nav = root.GetComponent<ControllerNavMenu>();
        if (nav)
            Object.DestroyImmediate(nav);

        return new BorPopup(root);
    }

    /// <summary>
    ///     Shows the window (opening animation of the game).
    /// </summary>
    public void Show()
    {
        if (_root)
            _root.SetActive(true);
    }

    /// <summary>
    ///     Closes the window (closing animation, then removed).
    /// </summary>
    public void Close()
    {
        if (_root)
            _root.GetComponent<TransitionOpen>().Close();
    }

    /// <summary>
    ///     Adds a text, centered on a place.
    /// </summary>
    /// <param name="y">The height of its center.</param>
    /// <param name="width">The width of its box.</param>
    /// <param name="height">The height of its box.</param>
    /// <param name="fontSize">The size of the font.</param>
    /// <returns>The text.</returns>
    public TextMeshPro AddText(float y, float width, float height, float fontSize)
    {
        var text = Object.Instantiate(_textModel, _root.transform);
        text.name = "BorText";
        text.gameObject.SetActive(true);
        text.transform.localPosition = new Vector3(0f, y, -1f);
        text.rectTransform.sizeDelta = new Vector2(width, height);
        text.enableAutoSizing = false;
        text.fontSize = fontSize;
        text.enableWordWrapping = true;
        text.alignment = TextAlignmentOptions.Center;
        text.text = string.Empty;
        return text;
    }

    /// <summary>
    ///     Adds a button like the "Okay" one.
    /// </summary>
    /// <param name="label">The text.</param>
    /// <param name="x">The center, across.</param>
    /// <param name="y">The center, up.</param>
    /// <param name="width">The width.</param>
    /// <param name="onClick">The action.</param>
    /// <returns>The button and its text.</returns>
    public (PassiveButton Button, TextMeshPro Text) AddButton(string label, float x, float y, float width, Action onClick)
    {
        var copy = Object.Instantiate(_buttonModel, _root.transform);
        copy.name = "BorButton";
        copy.SetActive(true);
        copy.transform.localPosition = new Vector3(x, y, -1f);

        foreach (var sprite in copy.GetComponentsInChildren<SpriteRenderer>(true))
            sprite.size = new Vector2(width / ButtonSpriteScale, sprite.size.y);
        var collider = copy.GetComponent<BoxCollider2D>();
        collider.size = new Vector2(width, collider.size.y);

        var text = copy.GetComponentInChildren<TextMeshPro>(true);
        var translator = text.GetComponent<TextTranslatorTMP>();
        if (translator)
            Object.DestroyImmediate(translator);
        // Centered in the button (the "Okay" text sits on the right of its place).
        text.rectTransform.sizeDelta = new Vector2(Math.Max(0.2f, width - 0.3f), text.rectTransform.sizeDelta.y);
        text.transform.localPosition = new Vector3(0f, text.transform.localPosition.y, text.transform.localPosition.z);
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 1f;
        text.text = label;

        var button = copy.GetComponent<PassiveButton>();
        button.OnClick = new Button.ButtonClickedEvent();
        button.OnClick.AddListener(onClick);
        return (button, text);
    }
}
