using System.Collections;
using System.Reflection;
using BepInEx.Configuration;
using BetterOtherRoles.Assets;
using BetterOtherRoles.Modules.Presets;
using BetterOtherRoles.Patches.Misc;
using BetterOtherRoles.Utilities;
using HarmonyLib;
using MiraAPI.Patches.Options;
using MiraAPI.PluginLoading;
using MiraAPI.Presets;
using MiraAPI.Translation;
using Reactor.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.Options;

/// <summary>
///     Presets of the mod (MiraAPI presets tab, logic in <see cref="BorPresets"/>), one nested class per part:
///     - <see cref="Tab"/>: presets tab (selection, Load, Save, Export, Import, Rename, Delete, active preset mark, sorting);
///     - <see cref="Popups"/>: popups of the tab, copies of the MiraAPI "save preset" popup (name, question, message);
///     - <see cref="LoadSave"/>: loaded preset values written to the config, vanilla settings applied, active preset;
///     - <see cref="ButtonGrid"/>: preset buttons placed in local space (MiraAPI 0.6.0 bug with the screen ratio).
/// </summary>
public static class PresetsPatches
{
    /// <summary>
    ///     Presets tab of BetterOtherRoles (MiraAPI page of the mod), on top of the MiraAPI one:
    ///     - a click on a preset only selects it (highlighted), a second click unselects it; "Load" (above "Save", greyed without selection) loads it
    ///       after a confirmation;
    ///     - the loaded preset has a green check mark, and a "*" when the settings changed since (<see cref="BorPresets"/>);
    ///     - "Save" is filled with the selected preset name, refuses an invalid name, asks before replacing a preset of the
    ///       same name, and tells it was saved;
    ///     - "Export" copies the code of the current settings with the preset name ("ImportedPreset" when changed since
    ///       loaded); "Import" asks the name of the preset to create from the code of the clipboard, then saves and loads it
    ///       (or tells the clipboard holds no code);
    ///     - a pencil button renames the selected preset, a trash button deletes it after a confirmation ("Default" can be
    ///       neither renamed nor deleted).
    ///     The popups are copies of the MiraAPI save popup (name box hidden for the confirmations).
    /// </summary>
    [HarmonyPatch]
    public static class Tab
    {
        // Check mark and "*" of the active preset: bold, darker on the highlighted (selected) button.
        private const string CheckMark = "<b><color={0}>✓</color></b> ";
        private const string ChangedMark = " <b><color={0}>*</color></b>";
        private const string CheckColor = "#4CE04C";
        private const string CheckColorSelected = "#0A6E0A";
        private const string ChangedColor = "#FFC03A";
        private const string ChangedColorSelected = "#A04A00";

        // Column of the large buttons, then the small ones two by two: Refresh, Rename / Trash, Folder.
        private const float ColumnX = 3.2f;
        private const float ColumnTop = 1.7f;
        private const float RowSpacing = 0.6f;
        private const float LeftX = 2.85f;
        private const float RightX = 3.55f;

        private static readonly Type MiraTabPatches = typeof(MenuState).Assembly.GetType("MiraAPI.Patches.Options.GamePresetsTabPatches", true)!;
        private static readonly FieldInfo SaveButtonField = AccessTools.Field(MiraTabPatches, "_saveButton");
        private static readonly FieldInfo RefreshButtonField = AccessTools.Field(MiraTabPatches, "_refreshButton");
        private static readonly FieldInfo FolderButtonField = AccessTools.Field(MiraTabPatches, "_folderButton");
        private static readonly MethodInfo RefreshMethod = AccessTools.Method(MiraTabPatches, "Refresh");

        private static string _selected = string.Empty;
        private static string _message = string.Empty;

        // The export message stays MessageDuration seconds, then fades out in MessageFade seconds.
        private const float MessageDuration = 5f;
        private const float MessageFade = 1f;
        private static float _messageAlpha = 1f;
        private static string _description = string.Empty;

        private static PassiveButton? _loadButton;
        private static PassiveButton? _exportButton;
        private static PassiveButton? _importButton;
        private static PassiveButton? _deleteButton;
        private static PassiveButton? _renameButton;

        private static bool IsBorPage => MenuState.Instance && MenuState.Instance.CurrentModIdx != 0 &&
                                         MenuState.Instance.CurrentMod.PluginId == BetterOtherRolesPlugin.Id;

        [HarmonyPatch(typeof(GamePresetsTab), nameof(GamePresetsTab.OnEnable))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        public static void OnEnablePostfix(GamePresetsTab __instance)
        {
            if (!GameSettingMenu.Instance) return;

            CreateButtons(__instance);
            var shown = IsBorPage;
            foreach (var button in new[] { _loadButton, _exportButton, _importButton, _deleteButton, _renameButton })
            {
                if (button)
                    button!.gameObject.SetActive(shown);
            }

            if (!shown) return;

            PlaceButtons();
            _message = string.Empty;
            Refresh(__instance);
        }

        [HarmonyPatch(typeof(GamePresetsTab), nameof(GamePresetsTab.OnDisable))]
        [HarmonyPostfix]
        public static void OnDisablePostfix()
        {
            foreach (var button in new[] { _loadButton, _exportButton, _importButton, _deleteButton, _renameButton })
            {
                if (button)
                    button!.gameObject.SetActive(false);
            }
        }

        // Preset buttons (select only), their labels, the description, and the greyed buttons.
        private static void Refresh(GamePresetsTab tab)
        {
            var plugin = BorPresets.Plugin;
            if (plugin == null) return;

            if (!BorPresets.GetPresetNames().Contains(_selected))
                _selected = string.Empty;

            var hasActive = BorPresets.TryGetActive(out var active, out var changed);
            TextMeshPro? marked = null;
            foreach (var preset in plugin.Presets)
            {
                if (!preset.PresetButton) continue;

                var button = preset.PresetButton!.GetComponent<PassiveButton>();
                var name = preset.Name;
                button.OnClick = new Button.ButtonClickedEvent();
                button.OnClick.AddListener((Action)(() =>
                {
                    _selected = _selected == name ? string.Empty : name;
                    Refresh(tab);
                }));

                var selected = name == _selected;
                var label = name;
                if (hasActive && name == active)
                {
                    label = string.Format(CheckMark, selected ? CheckColorSelected : CheckColor) + name +
                            (changed ? string.Format(ChangedMark, selected ? ChangedColorSelected : ChangedColor) : string.Empty);
                }

                button.buttonText.text = label;
                button.buttonText.enableAutoSizing = true;
                button.SelectButton(selected);
                if (label != name)
                    marked = button.buttonText;
            }

            KeepFontSize(plugin, marked);

            var description = !hasActive
                ? Get("Custom", "Custom settings")
                : string.Format(BetterOtherRolesPlugin.Culture, Get(changed ? "ActiveChanged" : "Active", changed ? "Active preset: {0} (changed)" : "Active preset: {0}"), active);
            _description = description;
            SetDescription(tab);

            SetEnabled(_loadButton, _selected.Length > 0);
            SetEnabled(_deleteButton, _selected.Length > 0 && _selected != BorPresets.DefaultName);
            SetEnabled(_renameButton, _selected.Length > 0 && _selected != BorPresets.DefaultName);
        }

        // The marks make the label longer, so the font smaller: the marked label keeps the size of the others.
        private static void KeepFontSize(MiraPluginInfo plugin, TextMeshPro? marked)
        {
            if (!marked) return;

            var size = 0f;
            foreach (var preset in plugin.Presets)
            {
                var text = preset.PresetButton ? preset.PresetButton!.GetComponent<PassiveButton>().buttonText : null;
                if (text && text != marked)
                {
                    text!.ForceMeshUpdate();
                    size = Mathf.Max(size, text.fontSize);
                }
            }

            if (size > 0f)
            {
                marked!.enableAutoSizing = false;
                marked.fontSize = size;
            }
        }

        private static void CreateButtons(GamePresetsTab tab)
        {
            var save = SaveButtonField.GetValue(null) as GameObject;
            var refresh = RefreshButtonField.GetValue(null) as GameObject;
            if (!save || !refresh) return;

            // "Save" asks before replacing a preset.
            var saveButton = save!.GetComponent<PassiveButton>();
            saveButton.OnClick = new Button.ButtonClickedEvent();
            saveButton.OnClick.AddListener((Action)(() => OnSave(tab)));

            if (!_loadButton)
            {
                _loadButton = CreateLargeButton(saveButton, tab, "BorLoadButton", "Load", "Load", () => OnLoad(tab));
                _exportButton = CreateLargeButton(saveButton, tab, "BorExportButton", "Export", "Export", () => OnExport(tab));
                _importButton = CreateLargeButton(saveButton, tab, "BorImportButton", "Import", "Import", () => OnImport(tab));
                _renameButton = CreateIconButton(refresh!.GetComponent<PassiveButton>(), tab, "BorRenameButton", BorAssets.PresetPencil.LoadAsset(), () => OnRename(tab));
                _deleteButton = CreateIconButton(refresh.GetComponent<PassiveButton>(), tab, "BorDeleteButton", BorAssets.PresetTrash.LoadAsset(), () => OnDelete(tab));
            }
        }

        // Load, Save, Export, Import in a column, then Refresh, Rename / Trash, Folder.
        private static void PlaceButtons()
        {
            var save = SaveButtonField.GetValue(null) as GameObject;
            var refresh = RefreshButtonField.GetValue(null) as GameObject;
            var folder = FolderButtonField.GetValue(null) as GameObject;
            if (!save || !refresh || !folder || !_loadButton) return;

            var z = save!.transform.localPosition.z;
            _loadButton!.transform.localPosition = new Vector3(ColumnX, ColumnTop, z);
            save.transform.localPosition = new Vector3(ColumnX, ColumnTop - RowSpacing, z);
            _exportButton!.transform.localPosition = new Vector3(ColumnX, ColumnTop - (2 * RowSpacing), z);
            _importButton!.transform.localPosition = new Vector3(ColumnX, ColumnTop - (3 * RowSpacing), z);

            var rowY = ColumnTop - (4 * RowSpacing);
            refresh!.transform.localPosition = new Vector3(LeftX, rowY, z);
            _renameButton!.transform.localPosition = new Vector3(RightX, rowY, z);
            _deleteButton!.transform.localPosition = new Vector3(LeftX, rowY - RowSpacing, z);
            folder!.transform.localPosition = new Vector3(RightX, rowY - RowSpacing, z);
        }

        private static PassiveButton CreateLargeButton(PassiveButton template, GamePresetsTab tab, string name, string key, string fallback, Action onClick)
        {
            var button = Object.Instantiate(template, tab.transform);
            button.name = name;
            button.buttonText.text = Get(key, fallback);
            button.OnClick = new Button.ButtonClickedEvent();
            button.OnClick.AddListener(onClick);
            return button;
        }

        // Small button like "Refresh", with an icon in the color of the refresh icon.
        private static PassiveButton CreateIconButton(PassiveButton refresh, GamePresetsTab tab, string name, Sprite iconSprite, Action onClick)
        {
            var button = Object.Instantiate(refresh, tab.transform);
            button.name = name;
            var icon = button.transform.Find("Sprite")?.GetComponent<SpriteRenderer>();
            if (icon)
            {
                icon!.sprite = iconSprite;
                icon.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
            }

            button.OnClick = new Button.ButtonClickedEvent();
            button.OnClick.AddListener(onClick);
            return button;
        }

        private static void SetEnabled(PassiveButton? button, bool enabled)
        {
            if (!button) return;

            button!.enabled = enabled;
            var alpha = enabled ? 1f : 0.4f;
            foreach (var renderer in button.GetComponentsInChildren<SpriteRenderer>(true))
            {
                var color = renderer.color;
                renderer.color = new Color(color.r, color.g, color.b, alpha);
            }

            foreach (var text in button.GetComponentsInChildren<TextMeshPro>(true))
            {
                text.alpha = alpha;
            }
        }

        private static void OnLoad(GamePresetsTab tab)
        {
            var name = _selected;
            var preset = BorPresets.Plugin?.Presets.FirstOrDefault(x => x.Name == name);
            if (preset == null) return;

            Popups.Confirm(Format("ConfirmLoad", "Load the preset {0}?", name), () =>
            {
                preset.LoadPreset();
                _message = string.Empty;
                Refresh(tab);
            });
        }

        // Filled with the selected preset (or the name refused by "No" on the replacement); a name already used asks to
        // replace that preset, "No" gives the name back to change it.
        private static void OnSave(GamePresetsTab tab, string? name = null)
        {
            name ??= _selected == BorPresets.DefaultName ? string.Empty : _selected;
            AskNameToReplace(string.Empty, name, value =>
            {
                Save(value);
                Popups.InformLater(Format("Saved", "The preset {0} has been saved.", value));
            }, value => OnSave(tab, value));
        }

        // Asks a valid name (in lower case); a name already used asks to replace that preset, "No" calls onRefused with
        // the name to change it. The name popup closes after its action: the next popup opens on the next frame.
        private static void AskNameToReplace(string message, string name, Action<string> onName, Action<string> onRefused)
        {
            Popups.AskValidName(message, name, BorPresets.IsValidName, value =>
            {
                var existing = BorPresets.FindPreset(value);
                if (existing == null)
                {
                    onName(value);
                    return;
                }

                Popups.ConfirmLater(Format("ConfirmReplace", "The preset {0} already exists. Replace it?", existing), () => onName(existing), () => onRefused(value));
            });
        }

        private static void Save(string name)
        {
            BorPresets.Save(name);
            _selected = name;
            RefreshMethod.Invoke(null, null);
        }

        private static void OnDelete(GamePresetsTab tab)
        {
            var name = _selected;
            if (name.Length == 0 || name == BorPresets.DefaultName) return;

            Popups.Confirm(Format("ConfirmDelete", "Do you really want to delete the preset {0}?", name), () =>
            {
                BorPresets.Delete(name);
                _selected = string.Empty;
                RefreshMethod.Invoke(null, null);
            });
        }

        private static void OnRename(GamePresetsTab tab)
        {
            var name = _selected;
            if (name.Length == 0 || name == BorPresets.DefaultName) return;

            Popups.AskValidName(Format("RenameTitle", "New name of the preset {0}:", name), name, BorPresets.IsFreeName, newName =>
            {
                BorPresets.Rename(name, newName);
                _selected = newName;
                RefreshMethod.Invoke(null, null);
            });
        }

        private static void OnExport(GamePresetsTab tab)
        {
            var name = BorPresets.TryGetActive(out var active, out var changed) && !changed ? active : BorPresets.ImportedName;
            GUIUtility.systemCopyBuffer = BorPresets.Export(name);
            ShowFadingMessage(tab, Get("Copied", "Code copied to the clipboard!"));
        }

        // Description of the active preset, then the message under it (with its transparency while it fades out).
        // The text is centered vertically: the message line is always there (an invisible character without message), so
        // the description stays in place when a message shows up or fades out.
        private static void SetDescription(GamePresetsTab tab)
        {
            var message = _message.Length > 0 ? _message : "-";
            var alpha = _message.Length > 0 ? Mathf.RoundToInt(Mathf.Clamp01(_messageAlpha) * 255f).ToString("X2", BetterOtherRolesPlugin.Culture) : "00";
            tab.PresetDescriptionText.text = $"{_description}\n<alpha=#{alpha}>{message}<alpha=#FF>";
        }

        private static void ShowFadingMessage(GamePresetsTab tab, string message)
        {
            _message = message;
            _messageAlpha = 1f;
            Refresh(tab);
            Coroutines.Start(CoFadeMessage(tab, message));
        }

        // Stops when another message replaced this one (or it was cleared), or when the tab is closed.
        private static IEnumerator CoFadeMessage(GamePresetsTab tab, string message)
        {
            var elapsed = 0f;
            while (elapsed < MessageDuration + MessageFade)
            {
                yield return null;
                if (_message != message || !tab || !tab.isActiveAndEnabled) yield break;

                elapsed += Time.deltaTime;
                if (elapsed > MessageDuration)
                {
                    _messageAlpha = 1f - ((elapsed - MessageDuration) / MessageFade);
                    SetDescription(tab);
                }
            }

            _message = string.Empty;
            _messageAlpha = 1f;
            SetDescription(tab);
        }

        private static void OnImport(GamePresetsTab tab)
        {
            if (!BorPresets.TryReadCode(GUIUtility.systemCopyBuffer, out var name, out var values))
            {
                Popups.Inform(Get("ClipboardInvalid", "The clipboard is empty or holds a code not compatible with the mod, please copy a valid code before importing."), Get("Cancel", "Cancel"));
                return;
            }

            AskImportName(name, values);
        }

        // A preset of the same name can be replaced, as when saving.
        private static void AskImportName(string name, string values)
        {
            AskNameToReplace(Get("ImportName", "Name of the imported preset:"), name, presetName =>
            {
                BorPresets.Import(values, presetName);
                _selected = presetName;
                _message = Get("Imported", "Code imported!");
                _messageAlpha = 1f;
                RefreshMethod.Invoke(null, null);
            }, refused => AskImportName(refused, values));
        }

        private static string Get(string key, string fallback) => MiraLocaleManager.Get("BetterOtherRoles.Presets." + key, fallback);

        private static string Format(string key, string fallback, string name) =>
            string.Format(BetterOtherRolesPlugin.Culture, Get(key, fallback), name);
    }

    /// <summary>
    ///     Popups of the presets tab, copies of the MiraAPI "save preset" popup (<see cref="SavePresetPopup"/>): the name to
    ///     save (filled with the selected preset), a question (name box and its icon replaced by the question, buttons
    ///     "Yes" / "No"), a message (button "Close" only), or a name under a message (buttons "Validate" / "Cancel", the
    ///     name box shakes when the name is refused).
    /// </summary>
    [HarmonyPatch]
    public static class Popups
    {
        // Key (Escape or Enter) that closed a popup: ignored by the settings menu until it is released.
        private static KeyCode _usedKey;
        private static int _keyFrame = -1;

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
            if (!popup) return;

            var box = popup!.GetComponentInChildren<TextBoxTMP>(true);
            var holder = popup.transform.Find("TextboxHolder");
            var save = popup.transform.Find("SaveButton")?.GetComponent<PassiveButton>();
            var close = popup.transform.Find("CloseButton")?.GetComponent<PassiveButton>();
            if (!box || !holder || !save || !close) return;

            if (name.Length > 0)
                box!.SetText(name.ToUpperInvariant(), string.Empty);

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
                        error!.gameObject.SetActive(true);

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
            if (!popup) return;

            SetButtonText(popup!, "SaveButton", Get("Yes", "Yes"));
            SetButtonText(popup!, "CloseButton", Get("No", "No"));
            ShowMessage(popup!, question);

            var save = popup!.transform.Find("SaveButton")?.GetComponent<PassiveButton>();
            var close = popup.transform.Find("CloseButton")?.GetComponent<PassiveButton>();
            if (onCancel == null || !save || !close) return;

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
                    Coroutines.Start(CoLater(onCancel));
            }));
        }

        /// <summary>
        ///     Shows a message with the "Close" button only (or another text for it).
        /// </summary>
        public static void Inform(string message, string? closeText = null)
        {
            var popup = Create(_ => { });
            if (!popup) return;

            var save = popup!.transform.Find("SaveButton");
            var close = popup.transform.Find("CloseButton");
            if (save && close)
            {
                save!.gameObject.SetActive(false);
                close!.localPosition = new Vector3(0f, close.localPosition.y, close.localPosition.z);
            }

            if (closeText != null)
                SetButtonText(popup, "CloseButton", closeText);

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
                holder.localPosition = origin;
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
            if (!model) return null;

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
                text!.text = FontStart + label + FontEnd;
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
            if (!box || !save) return;

            TextBoxPatches.AddCaret(box!);

            // Spaces and symbols ("-", "_", ".") allowed: MiraAPI removed the spaces from the shown text only.
            box.AllowSymbols = true;

            // Names shown in upper case, saved in lower case (BorPresets.ToPresetName).
            box.ForceUppercase = true;
            box.OnChange = new Button.ButtonClickedEvent();

            box.OnEnter = new Button.ButtonClickedEvent();
            box.OnEnter.AddListener((Action)(() =>
            {
                if (save)
                    save!.OnClick.Invoke();
            }));
            box.GiveFocus();
        }

        // The glass (light blue with reflections) and the background are at the same depth, so drawn in any order: the
        // glass was sometimes hidden behind the background (dark blue). Put in front of it, and everything else (buttons,
        // icon, name box, texts) in front of the glass.
        private static void RaiseGlass(SavePresetPopup popup)
        {
            var glass = popup.transform.Find("Glass");
            var background = popup.transform.Find("Background")?.GetComponent<SpriteRenderer>();
            var glassRenderer = glass ? glass!.GetComponent<SpriteRenderer>() : null;
            if (!glassRenderer || !background) return;

            var size = glassRenderer!.bounds.size;
            var scale = glass!.localScale;
            glass.localScale = new Vector3(scale.x * (1f - (GlassTrimX / size.x)), scale.y * (1f - ((GlassTrimTop + GlassTrimBottom) / size.y)), scale.z);
            glass.localPosition = new Vector3(glass.localPosition.x, glass.localPosition.y + ((GlassTrimBottom - GlassTrimTop) / 2f), GlassZ);
            var order = background!.sortingOrder;
            glassRenderer.sortingOrder = order + 1;
            foreach (var renderer in popup.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer != background && renderer != glassRenderer && renderer.sortingOrder < order + 2)
                    renderer.sortingOrder = order + 2;
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
                    pixels[index] = new Color32(outline.r, outline.g, outline.b, pixels[index].a);
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
                yield return index - 1;

            if (x < width - 1)
                yield return index + 1;

            if (y > 0)
                yield return index - width;

            if (y < height - 1)
                yield return index + width;
        }

        // The background sprite (from the "Download data" task) has a dark band above the frame: cut off. Its blue is
        // spread to the frame (grey ring in between).
        private static void CropBackground(SavePresetPopup popup)
        {
            var renderer = popup.transform.Find("Background")?.GetComponent<SpriteRenderer>();
            if (!renderer || !renderer!.sprite) return;

            if (!_background)
            {
                var copy = BorSpriteUtils.ToStandalone(renderer.sprite);
                if (copy == renderer.sprite) return;

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
                renderer.size = new Vector2(renderer.size.x, renderer.size.y * (1f - BackgroundTopCut));
        }

        private static string Get(string key, string fallback) => MiraLocaleManager.Get("BetterOtherRoles.Presets." + key, fallback);

        /// <summary>
        ///     Keys over a preset popup. The popup (a Minigame whose Close does nothing) let the settings menu take Escape,
        ///     through its own Update and through the game (native code calling CloseTab): its tab closed, then the menu
        ///     itself, the popup staying open. Now:
        ///     - Escape clicks the button closing the popup ("Cancel", "No" or "Close", with its action);
        ///     - Enter, when the name box does not have the focus (it has its own Enter), clicks the main button shown:
        ///       "Yes" / "Validate", else the only one ("Close" / "Cancel").
        ///     While a popup is open, and until the key that closed it is released, neither the menu nor the game menu
        ///     manager (ControllerManager, which clicks the back button of the top menu) run, and the menu closes neither
        ///     its tab nor itself.
        /// </summary>
        [HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.Update))]
        [HarmonyPatch(typeof(ControllerManager), nameof(ControllerManager.Update))]
        [HarmonyPrefix]
        public static bool MenuUpdatePrefix()
        {
            var popup = GetOpenPopup();
            if (popup && _keyFrame != Time.frameCount)
            {
                var save = GetShownButton(popup!, "SaveButton");
                var close = GetShownButton(popup!, "CloseButton");
                PassiveButton? button = null;
                KeyCode key = default;
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    button = close;
                    key = KeyCode.Escape;
                }
                else if (EnterDown(out var enter) && !HasFocusedBox(popup!))
                {
                    button = save ?? close;
                    key = enter;
                }

                // Once per frame, whichever patch sees the key first.
                if (button)
                {
                    _keyFrame = Time.frameCount;
                    _usedKey = key;
                    button!.OnClick.Invoke();
                }
            }

            return !IsMenuBlocked(popup);
        }

        [HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.CloseTab))]
        [HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.Close))]
        [HarmonyPrefix]
        public static bool GameSettingMenuClosePrefix()
        {
            return !IsMenuBlocked(GetOpenPopup());
        }

        private static SavePresetPopup? GetOpenPopup()
        {
            return Minigame.Instance ? Minigame.Instance.TryCast<SavePresetPopup>() : null;
        }

        private static PassiveButton? GetShownButton(SavePresetPopup popup, string name)
        {
            var button = popup.transform.Find(name);
            return button && button!.gameObject.activeInHierarchy ? button.GetComponent<PassiveButton>() : null;
        }

        private static bool EnterDown(out KeyCode key)
        {
            key = default;
            if (Input.GetKeyDown(KeyCode.Return))
            {
                key = KeyCode.Return;
            }
            else if (Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                key = KeyCode.KeypadEnter;
            }

            return key != default;
        }

        private static bool HasFocusedBox(SavePresetPopup popup)
        {
            var box = popup.GetComponentInChildren<TextBoxTMP>();
            return box && box!.hasFocus;
        }

        // Blocked while a popup is open, and while the key that closed one is held (or released this frame).
        private static bool IsMenuBlocked(SavePresetPopup? popup)
        {
            if (popup) return true;

            if (_usedKey != default && (Input.GetKey(_usedKey) || Input.GetKeyUp(_usedKey))) return true;

            _usedKey = default;
            return false;
        }
    }

    /// <summary>
    ///     Presets of MiraAPI, fixed and completed (<see cref="BorPresets"/>):
    ///     - loading a preset applies the option values (SetValue without RPC) but never wrote them to the plugin config:
    ///       after a restart, the options were back to their values before the preset. Every option value is written to the
    ///       plugin config (the role counts and chances already are), then the vanilla settings of the preset are applied,
    ///       and the preset becomes the active one;
    ///     - MiraAPI writes every preset file again each time the settings menu opens (PresetManager.LoadPresets): the files
    ///       are only written when saved. The values missing in a file still get their default value when loaded;
    ///     - the presets are sorted ("Default" first, then by name), and the buttons of the presets read again are taken out
    ///       of the grid at once (destroyed at the end of the frame only, they left holes in it after "Refresh").
    /// </summary>
    [HarmonyPatch]
    public static class LoadSave
    {
        private static readonly PropertyInfo InternalPresetsProperty = AccessTools.Property(typeof(MiraPluginInfo), "InternalPresets");
        private static readonly PropertyInfo PresetsProperty = AccessTools.Property(typeof(MiraPluginInfo), nameof(MiraPluginInfo.Presets));

        private static bool _readingPresets;

        [HarmonyPatch(typeof(OptionPreset), nameof(OptionPreset.LoadPreset))]
        [HarmonyPostfix]
        public static void LoadPresetPostfix(OptionPreset __instance)
        {
            var config = __instance.PluginConfig;
            foreach (var option in __instance.Plugin.Options)
            {
                var definition = option.ConfigDefinition;
                if (definition == null || !config.ContainsKey(definition)) continue;

                var value = AccessTools.Property(option.GetType(), "Value")?.GetValue(option);
                if (value != null)
                    config[definition].BoxedValue = value;
            }

            config.Save();

            if (__instance.Plugin.PluginId == BetterOtherRolesPlugin.Id)
            {
                BorPresets.ApplyVanilla(__instance.PresetConfig);
                if (__instance.Name.Length > 0)
                    BorPresets.SetActive(__instance.Name);
            }
        }

        [HarmonyPatch(typeof(PresetManager), nameof(PresetManager.LoadPresets))]
        [HarmonyPrefix]
        public static void LoadPresetsPrefix(MiraPluginInfo plugin)
        {
            _readingPresets = true;
            foreach (var preset in plugin.Presets)
            {
                if (preset.PresetButton)
                {
                    preset.PresetButton!.SetActive(false);
                    preset.PresetButton.transform.SetParent(null, false);
                }
            }
        }

        [HarmonyPatch(typeof(PresetManager), nameof(PresetManager.LoadPresets))]
        [HarmonyPostfix]
        public static void LoadPresetsPostfix(MiraPluginInfo plugin)
        {
            if (InternalPresetsProperty.GetValue(plugin) is not List<OptionPreset> presets) return;

            presets.Sort((a, b) => a.Name == BorPresets.DefaultName ? (b.Name == BorPresets.DefaultName ? 0 : -1)
                : b.Name == BorPresets.DefaultName ? 1 : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            PresetsProperty.SetValue(plugin, presets.ToArray());
        }

        [HarmonyPatch(typeof(PresetManager), nameof(PresetManager.LoadPresets))]
        [HarmonyFinalizer]
        public static void LoadPresetsFinalizer()
        {
            _readingPresets = false;
        }

        // While MiraAPI reads the preset files: not written again.
        [HarmonyPatch(typeof(ConfigFile), nameof(ConfigFile.Save))]
        [HarmonyPrefix]
        public static bool ConfigSavePrefix(ConfigFile __instance)
        {
            return !_readingPresets || !__instance.ConfigFilePath.StartsWith(PresetManager.PresetDirectory, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    ///     Fixes a MiraAPI 0.6.0 bug: the preset buttons of a mod are clones of the vanilla GameSettingsButton,
    ///     placed by a vanilla GridArrange. Their spacing depends on the screen ratio (and on resolution changes while the
    ///     menu is open): the second column ends up shifted, or on top of the save button.
    ///     The buttons are placed here in local space, which does not depend on the resolution.
    /// </summary>
    [HarmonyPatch(typeof(GamePresetsTab), nameof(GamePresetsTab.OnEnable))]
    [HarmonyAfter("mira.api")]
    public static class ButtonGrid
    {
        // 2 columns like MiraAPI. In the container space, a button is about 2.67 x 0.52: cells leave a small gap around it.
        private const int Columns = 2;
        private const float CellWidth = 2.8f;
        private const float CellHeight = 0.6f;

        [HarmonyPostfix]
        public static void OnEnablePostfix()
        {
            if (!GameSettingMenu.Instance || !MenuState.Instance || MenuState.Instance.CurrentModIdx == 0) return;

            var container = MenuState.Instance.CurrentContainer;
            if (!container) return;

            foreach (var aspectPosition in container.GetComponentsInChildren<AspectPosition>(true))
            {
                UnityEngine.Object.DestroyImmediate(aspectPosition);
            }

            var grid = container.GetComponent<GridArrange>();
            if (grid)
                grid.enabled = false;

            var index = 0;
            for (var i = 0; i < container.transform.childCount; i++)
            {
                var child = container.transform.GetChild(i);
                if (!child.gameObject.activeSelf) continue;

                child.localPosition = new Vector3(index % Columns * CellWidth, -(index / Columns) * CellHeight, child.localPosition.z);
                index++;
            }
        }
    }
}
