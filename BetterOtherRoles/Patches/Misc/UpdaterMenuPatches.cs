using BetterOtherRoles.Modules.Updater;
using HarmonyLib;
using MiraAPI.Translation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Updater on the main menu (<see cref="BorUpdater"/>): the releases checked when the main menu shows, and an
///     "Updates" button in the bottom row of the left panel, between "Credits" and "Quit" (a copy of "Credits"; the
///     three buttons share the width of the two, with the same gap). The question of the updater
///     (<see cref="BorUpdaterWindow.Offer"/>) opens by itself once per launch when an update is there, unless the player
///     chose not to see it again.
///     The game sizes these buttons to the screen (AspectScaledAsset.ScaleObject) and places them from its edges
///     (AspectPosition.AdjustPosition, possibly every frame): what it gives is noted after each of them, and the row
///     is cut in three from that, at the end of the frame.
/// </summary>
[HarmonyPatch]
public static class UpdaterMenuPatches
{
    private static PassiveButton? _button;
    private static PassiveButton? _credits;
    private static PassiveButton? _quit;
    private static bool _dirty;
    private static bool _offered;

    // What the game gives to each button of the row, by object id.
    private static readonly Dictionary<int, ButtonState> States = [];

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
    [HarmonyPostfix]
    public static void StartPostfix(MainMenuManager __instance)
    {
        BorUpdater.Check();
        States.Clear();
        _credits = __instance.creditsButton;
        _quit = __instance.quitButton;
        _button = MakeButton(_credits);
        foreach (var button in new[] { _credits, _button, _quit })
            States[button.gameObject.GetInstanceID()] = new ButtonState(button);
        _dirty = true;
    }

    // Sizes given by the game to a button of the row.
    [HarmonyPatch(typeof(AspectScaledAsset), nameof(AspectScaledAsset.ScaleObject))]
    [HarmonyPostfix]
    public static void ScaleObjectPostfix(AspectScaledAsset __instance)
    {
        if (!States.TryGetValue(__instance.gameObject.GetInstanceID(), out var state)) return;

        state.RecordSizes(__instance);
        _dirty = true;
    }

    // Place given by the game to a button of the row.
    [HarmonyPatch(typeof(AspectPosition), nameof(AspectPosition.AdjustPosition), typeof(float))]
    [HarmonyPostfix]
    public static void AdjustPositionPostfix(AspectPosition __instance)
    {
        if (!States.TryGetValue(__instance.gameObject.GetInstanceID(), out var state)) return;

        state.RecordPosition();
        _dirty = true;
    }

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.LateUpdate))]
    [HarmonyPostfix]
    public static void LateUpdatePostfix()
    {
        BorUpdater.Poll();
        if (_dirty && _credits && _quit && _button)
        {
            _dirty = false;
            Layout();
        }

        // Once per launch, unless the player chose not to see that update again.
        if (!_offered && BorUpdater.State == UpdaterState.Checked && BorUpdater.UpdateToOffer is { } update)
        {
            _offered = true;
            BorUpdaterWindow.Offer(update);
        }
    }

    // A copy of "Credits" with its own text and action.
    private static PassiveButton MakeButton(PassiveButton credits)
    {
        var copy = Object.Instantiate(credits.gameObject, credits.transform.parent);
        copy.name = "BorUpdatesButton";
        var text = copy.GetComponentInChildren<TextMeshPro>(true);
        var translator = text.GetComponent<TextTranslatorTMP>();
        if (translator)
            Object.DestroyImmediate(translator);
        text.text = MiraLocaleManager.Get("BetterOtherRoles.Updater.Button", "updates");

        var button = copy.GetComponent<PassiveButton>();
        button.OnClick = new Button.ButtonClickedEvent();
        button.OnClick.AddListener((Action)BorUpdaterWindow.Open);
        return button;
    }

    // The row from the left edge of "Credits" to the right edge of "Quit" (as the game places them), cut in three.
    private static void Layout()
    {
        var credits = States[_credits!.gameObject.GetInstanceID()];
        var quit = States[_quit!.gameObject.GetInstanceID()];
        var button = States[_button!.gameObject.GetInstanceID()];

        var creditsHalf = credits.WorldWidth / 2f;
        var quitHalf = quit.WorldWidth / 2f;
        var start = credits.Position.x - creditsHalf;
        var end = quit.Position.x + quitHalf;
        var gap = Math.Max(0f, (quit.Position.x - quitHalf) - (credits.Position.x + creditsHalf));
        var width = (end - start - (2f * gap)) / 3f;

        credits.Apply(start + (width / 2f), credits.Position.y, width);
        button.Apply(start + (width * 1.5f) + gap, credits.Position.y, width);
        quit.Apply(end - (width / 2f), quit.Position.y, width);
    }

    /// <summary>
    ///     A button of the row: the sizes and place the game gives it, and the places of its parts (never changed by
    ///     the game), from which its width is changed.
    /// </summary>
    private sealed class ButtonState
    {
        private readonly PassiveButton _owner;
        private readonly List<(SpriteRenderer Sprite, Vector3 Position)> _sprites = [];
        private readonly List<(TextMeshPro Text, Vector3 Position)> _texts = [];
        private readonly List<Vector2> _spriteSizes = [];
        private readonly List<Vector2> _textBoxes = [];
        private Vector2 _collider;

        public ButtonState(PassiveButton button)
        {
            _owner = button;
            foreach (var sprite in button.GetComponentsInChildren<SpriteRenderer>(true))
                _sprites.Add((sprite, sprite.transform.localPosition));
            foreach (var text in button.GetComponentsInChildren<TextMeshPro>(true))
                _texts.Add((text, text.transform.localPosition));

            // As the game made them, before any change.
            _spriteSizes.AddRange(_sprites.Select(x => x.Sprite.size));
            _textBoxes.AddRange(_texts.Select(x => x.Text.rectTransform.sizeDelta));
            var collider = button.GetComponent<BoxCollider2D>();
            _collider = collider ? collider!.size : Vector2.zero;
            RecordPosition();
        }

        public Vector3 Position { get; private set; }

        public float WorldWidth => _collider.x * _owner.transform.lossyScale.x;

        // Only the parts the game has just sized (the others keep the sizes noted before, theirs).
        public void RecordSizes(AspectScaledAsset asset)
        {
            for (var i = 0; i < _sprites.Count; i++)
            {
                if (asset.spritesToScale.Contains(_sprites[i].Sprite))
                    _spriteSizes[i] = _sprites[i].Sprite.size;
            }

            for (var i = 0; i < _texts.Count; i++)
            {
                if (asset.textToScale.Contains(_texts[i].Text))
                    _textBoxes[i] = _texts[i].Text.rectTransform.sizeDelta;
            }

            var collider = _owner.GetComponent<BoxCollider2D>();
            if (collider && asset.collidersToScale.Contains(collider))
                _collider = collider!.size;
        }

        public void RecordPosition() => Position = _owner.transform.position;

        // At a width (in the world) and a place: the parts at the same share of their width.
        public void Apply(float x, float y, float width)
        {
            var factor = WorldWidth > 0f ? width / WorldWidth : 1f;
            for (var i = 0; i < _sprites.Count; i++)
            {
                var (sprite, position) = _sprites[i];
                sprite.size = new Vector2(_spriteSizes[i].x * factor, _spriteSizes[i].y);
                sprite.transform.localPosition = new Vector3(position.x * factor, position.y, position.z);
            }

            for (var i = 0; i < _texts.Count; i++)
            {
                var (text, position) = _texts[i];
                text.rectTransform.sizeDelta = new Vector2(_textBoxes[i].x * factor, _textBoxes[i].y);
                text.transform.localPosition = new Vector3(position.x * factor, position.y, position.z);
                text.enableAutoSizing = true;
                text.fontSizeMin = 1f;
                text.fontSizeMax = Math.Max(text.fontSizeMax, text.fontSize);
            }

            var collider = _owner.GetComponent<BoxCollider2D>();
            if (collider)
                collider!.size = new Vector2(_collider.x * factor, _collider.y);

            _owner.transform.position = new Vector3(x, y, Position.z);
        }
    }
}
