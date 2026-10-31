using System.Reflection;
using BetterOtherRoles.Modules.Presets;
using BetterOtherRoles.Utilities;
using HarmonyLib;
using MiraAPI.Patches.Options;
using MiraAPI.Presets;
using MiraAPI.Translation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.Options;

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
public static class PresetsTabPatches
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
        if (!GameSettingMenu.Instance)
        {
            return;
        }

        CreateButtons(__instance);
        var shown = IsBorPage;
        foreach (var button in new[] { _loadButton, _exportButton, _importButton, _deleteButton, _renameButton })
        {
            if (button)
            {
                button!.gameObject.SetActive(shown);
            }
        }

        if (!shown)
        {
            return;
        }

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
            {
                button!.gameObject.SetActive(false);
            }
        }
    }

    // Preset buttons (select only), their labels, the description, and the greyed buttons.
    private static void Refresh(GamePresetsTab tab)
    {
        var plugin = BorPresets.Plugin;
        if (plugin == null)
        {
            return;
        }

        if (!BorPresets.GetPresetNames().Contains(_selected))
        {
            _selected = string.Empty;
        }

        var hasActive = BorPresets.TryGetActive(out var active, out var changed);
        TextMeshPro? marked = null;
        foreach (var preset in plugin.Presets)
        {
            if (!preset.PresetButton)
            {
                continue;
            }

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
            {
                marked = button.buttonText;
            }
        }

        KeepFontSize(plugin, marked);

        var description = !hasActive
            ? Get("Custom", "Custom settings")
            : string.Format(BetterOtherRolesPlugin.Culture, Get(changed ? "ActiveChanged" : "Active", changed ? "Active preset: {0} (changed)" : "Active preset: {0}"), active);
        tab.PresetDescriptionText.text = _message.Length > 0 ? $"{description}\n{_message}" : description;

        SetEnabled(_loadButton, _selected.Length > 0);
        SetEnabled(_deleteButton, _selected.Length > 0 && _selected != BorPresets.DefaultName);
        SetEnabled(_renameButton, _selected.Length > 0 && _selected != BorPresets.DefaultName);
    }

    // The marks make the label longer, so the font smaller: the marked label keeps the size of the others.
    private static void KeepFontSize(MiraPluginInfo plugin, TextMeshPro? marked)
    {
        if (!marked)
        {
            return;
        }

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
        if (!save || !refresh)
        {
            return;
        }

        // "Save" asks before replacing a preset.
        var saveButton = save!.GetComponent<PassiveButton>();
        saveButton.OnClick = new Button.ButtonClickedEvent();
        saveButton.OnClick.AddListener((Action)(() => OnSave(tab)));

        if (!_loadButton)
        {
            _loadButton = CreateLargeButton(saveButton, tab, "BorLoadButton", "Load", "Load", () => OnLoad(tab));
            _exportButton = CreateLargeButton(saveButton, tab, "BorExportButton", "Export", "Export", () => OnExport(tab));
            _importButton = CreateLargeButton(saveButton, tab, "BorImportButton", "Import", "Import", () => OnImport(tab));
            _renameButton = CreateIconButton(refresh!.GetComponent<PassiveButton>(), tab, "BorRenameButton", BorSpriteUtils.CreatePencilSprite, () => OnRename(tab));
            _deleteButton = CreateIconButton(refresh.GetComponent<PassiveButton>(), tab, "BorDeleteButton", BorSpriteUtils.CreateTrashSprite, () => OnDelete(tab));
        }
    }

    // Load, Save, Export, Import in a column, then Refresh, Rename / Trash, Folder.
    private static void PlaceButtons()
    {
        var save = SaveButtonField.GetValue(null) as GameObject;
        var refresh = RefreshButtonField.GetValue(null) as GameObject;
        var folder = FolderButtonField.GetValue(null) as GameObject;
        if (!save || !refresh || !folder || !_loadButton)
        {
            return;
        }

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

    // Small button like "Refresh", with an icon drawn in the color of the refresh icon.
    private static PassiveButton CreateIconButton(PassiveButton refresh, GamePresetsTab tab, string name, Func<Color, Sprite> createIcon, Action onClick)
    {
        var button = Object.Instantiate(refresh, tab.transform);
        button.name = name;
        var icon = button.transform.Find("Sprite")?.GetComponent<SpriteRenderer>();
        if (icon)
        {
            icon!.sprite = createIcon(BorSpriteUtils.GetMainColor(icon.sprite));
            icon.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
        }

        button.OnClick = new Button.ButtonClickedEvent();
        button.OnClick.AddListener(onClick);
        return button;
    }

    private static void SetEnabled(PassiveButton? button, bool enabled)
    {
        if (!button)
        {
            return;
        }

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
        if (preset == null)
        {
            return;
        }

        PresetPopups.Confirm(Format("ConfirmLoad", "Load the preset {0}?", name), () =>
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
            PresetPopups.InformLater(Format("Saved", "The preset {0} has been saved.", value));
        }, value => OnSave(tab, value));
    }

    // Asks a valid name (in lower case); a name already used asks to replace that preset, "No" calls onRefused with
    // the name to change it. The name popup closes after its action: the next popup opens on the next frame.
    private static void AskNameToReplace(string message, string name, Action<string> onName, Action<string> onRefused)
    {
        PresetPopups.AskValidName(message, name, BorPresets.IsValidName, value =>
        {
            var existing = BorPresets.FindPreset(value);
            if (existing == null)
            {
                onName(value);
                return;
            }

            PresetPopups.ConfirmLater(Format("ConfirmReplace", "The preset {0} already exists. Replace it?", existing), () => onName(existing), () => onRefused(value));
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
        if (name.Length == 0 || name == BorPresets.DefaultName)
        {
            return;
        }

        PresetPopups.Confirm(Format("ConfirmDelete", "Do you really want to delete the preset {0}?", name), () =>
        {
            BorPresets.Delete(name);
            _selected = string.Empty;
            RefreshMethod.Invoke(null, null);
        });
    }

    private static void OnRename(GamePresetsTab tab)
    {
        var name = _selected;
        if (name.Length == 0 || name == BorPresets.DefaultName)
        {
            return;
        }

        PresetPopups.AskValidName(Format("RenameTitle", "New name of the preset {0}:", name), name, BorPresets.IsFreeName, newName =>
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
        _message = Get("Copied", "Code copied to the clipboard!");
        Refresh(tab);
    }

    private static void OnImport(GamePresetsTab tab)
    {
        if (!BorPresets.TryReadCode(GUIUtility.systemCopyBuffer, out var name, out var values))
        {
            PresetPopups.Inform(Get("ClipboardInvalid", "The clipboard is empty or holds a code not compatible with the mod, please copy a valid code before importing."), Get("Cancel", "Cancel"));
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
            RefreshMethod.Invoke(null, null);
        }, refused => AskImportName(refused, values));
    }

    private static string Get(string key, string fallback) => MiraLocaleManager.Get("BetterOtherRoles.Presets." + key, fallback);

    private static string Format(string key, string fallback, string name) =>
        string.Format(BetterOtherRolesPlugin.Culture, Get(key, fallback), name);
}
