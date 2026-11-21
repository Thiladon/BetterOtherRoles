using System.Reflection;
using BetterOtherRoles.Options.Maps;
using HarmonyLib;
using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Patches.Options;
using MiraAPI.Translation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.Options;

/// <summary>
///     Settings of each map, in categories before the ones of the game in the vanilla game settings tab, in the settings
///     view of the lobby and in the settings of the guide: "Sabotages" (<see cref="SabotageOptions"/>), then "Doors"
///     (<see cref="DoorOptions"/>). Only the maps that can be played are shown: the map of the lobby, or the maps the
///     random map can draw; in a game, the map played.
/// </summary>
[HarmonyPatch]
public static class MapSettingsPatches
{
    private const string HeaderPrefix = "BorMapSettingsHeader";
    private const string RowPrefix = "BorMapSettingsOption";

    // Name of the game mode banner made by MiraAPI (vanilla tab).
    private const string BannerName = "GamemodeInfo";

    // Places of the vanilla tab in MiraAPI (GameOptionsMenuPatch.CreateSettings).
    private const float HeaderX = -0.903f;
    private const float HeaderHeight = 0.63f;
    private const float RowX = 0.952f;
    private const float RowHeight = KillCooldownPatches.RowHeight;

    // Places of the settings view of the lobby (LobbyViewSettingsPane.DrawNormalTab).
    private const float ViewHeaderX = -9.77f;
    private const float ViewHeaderHeight = 1.05f;
    private const float ViewLeftX = -8.95f;
    private const float ViewRightX = -3f;
    private const float ViewRowHeight = 0.85f;

    // Categories, in the order shown: locale key and English text.
    private static readonly (string Key, string Text)[] Sections =
    [
        ("BetterOtherRoles.Setting.Sabotages", "Sabotages"),
        ("BetterOtherRoles.Setting.Doors", "Doors"),
    ];

    // Mod shown in the settings view of the lobby (0: the game), private in MiraAPI.
    private static readonly PropertyInfo SelectedModIdxProperty =
        AccessTools.Property(typeof(LobbyViewPanePatches), "SelectedModIdx");

    // Rows of the vanilla tab, in the order of Entries, and the header of each category.
    private static readonly List<OptionBehaviour?> Rows = [];
    private static readonly List<CategoryHeaderMasked?> Headers = [];
    private static readonly Dictionary<ModdedNumberOption, FloatGameSetting> ViewData = [];

    private static GameOptionsMenu? _menu;

    // Header of the first category of the game (Impostors), above which the categories go.
    private static Transform? _anchor;

    // Rows shown (bits of their index), to place them again when it changes.
    private static int _shownKey = -1;
    private static float _room;

    // Room by which the categories of the game are moved down (kept with the tab, MiraAPI keeps it).
    private static float _applied;

    /// <summary>
    ///     Gets the room taken by the categories in the scrolled height (<see cref="SettingsScrollPatches"/>).
    /// </summary>
    public static float Room => Headers.Exists(x => x && x!.gameObject.activeInHierarchy) ? _room : 0f;

    /// <summary>
    ///     Categories before the ones of the game in the vanilla tab: made once with the tab (MiraAPI keeps them), found
    ///     again after.
    /// </summary>
    [HarmonyPatch(typeof(GameOptionsMenu), nameof(GameOptionsMenu.Initialize))]
    [HarmonyPostfix]
    public static void MenuInitializePostfix(GameOptionsMenu __instance)
    {
        var banner = FindBanner(__instance);
        if (!banner) return;

        var container = banner!.parent;
        _menu = __instance;
        _anchor = FindFirstVanillaHeader(banner);
        if (!_anchor) return;

        Headers.Clear();
        for (var s = 0; s < Sections.Length; s++)
        {
            var existing = container.Find(HeaderPrefix + s);
            if (existing)
            {
                Headers.Add(existing.GetComponent<CategoryHeaderMasked>());
                continue;
            }

            Headers.Add(CreateHeader(__instance, container, s));
            _applied = 0f;
        }

        Rows.Clear();
        var entries = Entries();
        for (var i = 0; i < entries.Count; i++)
        {
            var row = container.Find(RowPrefix + i);
            if (row)
            {
                var option = row.GetComponent<OptionBehaviour>();

                // MiraAPI takes back every option of the tab as a vanilla one: these are MiraAPI options.
                __instance.Children?.Remove(option);
                Rows.Add(option);
            }
            else
            {
                Rows.Add(CreateRow(__instance, container, entries[i].Option, i));
            }
        }

        _shownKey = -1;
        Layout();
    }

    /// <summary>
    ///     Rows placed again when the maps shown change (map of the lobby, random map, weights).
    /// </summary>
    [HarmonyPatch(typeof(GameOptionsMenu), nameof(GameOptionsMenu.Update))]
    [HarmonyPostfix]
    public static void MenuUpdatePostfix(GameOptionsMenu __instance)
    {
        if (__instance != _menu || !_anchor) return;

        if (ShownKey() != _shownKey) Layout();
    }

    /// <summary>
    ///     MiraAPI does not know these rows (their option keeps its own control): the game would show the value of its
    ///     own settings at the opening (index 0 for a list), and stop at the last choice. Value of the option shown.
    /// </summary>
    [HarmonyPatch(typeof(StringOption), nameof(StringOption.Initialize))]
    [HarmonyPrefix]
    public static bool StringInitializePrefix(StringOption __instance)
    {
        if (OptionOf(__instance) is not { } option) return true;

        __instance.Value = (int)option.GetFloatData();
        __instance.TitleText.text = TranslationController.Instance.GetString(__instance.Title);
        __instance.ValueText.text = TranslationController.Instance.GetString(__instance.Values[__instance.Value]);
        __instance.MinusBtn.SetInteractable(true);
        __instance.PlusBtn.SetInteractable(true);
        return false;
    }

    [HarmonyPatch(typeof(NumberOption), nameof(NumberOption.Initialize))]
    [HarmonyPrefix]
    public static bool NumberInitializePrefix(NumberOption __instance)
    {
        if (OptionOf(__instance) is not { } option) return true;

        __instance.TitleText.text = TranslationController.Instance.GetString(__instance.Title);
        __instance.Value = option.GetFloatData();
        __instance.AdjustButtonsActiveState();
        return false;
    }

    /// <summary>
    ///     A list goes back to its first choice after the last one (and the other way), as the options of MiraAPI.
    /// </summary>
    [HarmonyPatch(typeof(StringOption), nameof(StringOption.Increase))]
    [HarmonyPrefix]
    public static bool StringIncreasePrefix(StringOption __instance) => Cycle(__instance, 1);

    [HarmonyPatch(typeof(StringOption), nameof(StringOption.Decrease))]
    [HarmonyPrefix]
    public static bool StringDecreasePrefix(StringOption __instance) => Cycle(__instance, -1);

    [HarmonyPatch(typeof(StringOption), nameof(StringOption.AdjustButtonsActiveState))]
    [HarmonyPrefix]
    public static bool StringAdjustButtonsPrefix(StringOption __instance)
    {
        if (OptionOf(__instance) == null) return true;

        __instance.MinusBtn.SetInteractable(true);
        __instance.PlusBtn.SetInteractable(true);
        return false;
    }

    /// <summary>
    ///     The game would write the value to its own settings (no setting there: error in the log).
    /// </summary>
    [HarmonyPatch(typeof(StringOption), nameof(StringOption.UpdateValue))]
    [HarmonyPatch(typeof(NumberOption), nameof(NumberOption.UpdateValue))]
    [HarmonyPrefix]
    public static bool UpdateValuePrefix(OptionBehaviour __instance) => OptionOf(__instance) == null;

    /// <summary>
    ///     Shows the values set by the host (MiraAPI only updates the control it knows, not these rows).
    /// </summary>
    public static void RefreshValues()
    {
        if (Rows.Count == 0) return;

        var entries = Entries();
        for (var i = 0; i < Rows.Count && i < entries.Count; i++)
        {
            var row = Rows[i];
            if (!row) continue;

            var value = entries[i].Option.GetFloatData();
            var number = row!.TryCast<NumberOption>();
            if (number && !Mathf.Approximately(number!.Value, value))
            {
                number.Value = value;
                continue;
            }

            var choice = row.TryCast<StringOption>();
            if (choice && choice!.Value != (int)value) choice.Value = (int)value;
        }
    }

    /// <summary>
    ///     Settings view of the lobby: the categories before the ones of the game (they are moved down to make room).
    /// </summary>
    [HarmonyPatch(typeof(LobbyViewSettingsPane), nameof(LobbyViewSettingsPane.DrawNormalTab))]
    [HarmonyPostfix]
    public static void DrawNormalTabPostfix(LobbyViewSettingsPane __instance)
    {
        // Drawn by MiraAPI instead of the game for the tab of a mod.
        if (SelectedModIdxProperty?.GetValue(null) is int index && index != 0) return;

        var infos = __instance.settingsInfo;
        if (infos == null || infos.Count == 0) return;

        var sections = ShownBySection();
        if (sections.All(x => x.Count == 0)) return;

        // Height of each category: header, lines of 2 panels, then the gap before the next category (as the game does).
        var y = infos[0].transform.localPosition.y;
        var room = sections.Where(x => x.Count > 0).Sum(x => ViewHeaderHeight + ViewRowHeight * ((x.Count + 1) / 2));
        foreach (var info in infos)
        {
            if (info) info.transform.localPosition += Vector3.down * room;
        }

        for (var s = 0; s < sections.Count; s++)
        {
            var shown = sections[s];
            if (shown.Count == 0) continue;

            var header = Object.Instantiate(__instance.categoryHeaderOrigin);
            header.SetHeader(SectionTitle(s), 61);
            header.transform.SetParent(__instance.settingsContainer);
            header.transform.localScale = Vector3.one;
            header.transform.localPosition = new Vector3(ViewHeaderX, y, -2f);
            infos.Add(header.gameObject);
            y -= ViewHeaderHeight;

            for (var i = 0; i < shown.Count; i++)
            {
                if (i % 2 == 0 && i > 0) y -= ViewRowHeight;

                var panel = Object.Instantiate(__instance.infoPanelOrigin);
                panel.transform.SetParent(__instance.settingsContainer);
                panel.transform.localScale = Vector3.one;
                panel.transform.localPosition = new Vector3(i % 2 == 0 ? ViewLeftX : ViewRightX, y, -2f);
                panel.SetInfo(shown[i].StringName, ValueString(shown[i]), 61);
                infos.Add(panel.gameObject);
            }

            y -= ViewRowHeight;
        }

        __instance.scrollBar.CalculateAndSetYBounds(infos.Count + 10, 2f, 6f, ViewRowHeight);
    }

    /// <summary>
    ///     Title and value of each setting shown, for the role guide (list of the settings of the game).
    /// </summary>
    /// <returns>The lines.</returns>
    public static List<(string Title, string Value)> GuideLines()
    {
        var lines = new List<(string Title, string Value)>();
        if (!TranslationController.Instance || GameOptionsManager.Instance?.CurrentGameOptions == null) return lines;

        foreach (var option in ShownBySection().SelectMany(x => x))
            lines.Add((TranslationController.Instance.GetString(option.StringName), ValueString(option)));

        return lines;
    }

    // Every setting with its category and the maps it is used on, in the order shown.
    private static List<(int Section, MapNames[] Maps, IModdedOption Option)> Entries()
    {
        var entries = new List<(int Section, MapNames[] Maps, IModdedOption Option)>();
        foreach (var (maps, option) in OptionGroupSingleton<SabotageOptions>.Instance.All)
            entries.Add((0, maps, option));

        foreach (var (maps, option) in OptionGroupSingleton<DoorOptions>.Instance.All)
            entries.Add((1, maps, option));

        return entries;
    }

    // Option of a row of these categories (its name holds the index in Entries), or null for another row.
    private static IModdedOption? OptionOf(OptionBehaviour behaviour)
    {
        var name = behaviour.name;
        if (!name.StartsWith(RowPrefix, StringComparison.Ordinal) ||
            !int.TryParse(name.AsSpan(RowPrefix.Length), System.Globalization.NumberStyles.None, BetterOtherRolesPlugin.Culture, out var index)) return null;

        var entries = Entries();
        return index < entries.Count ? entries[index].Option : null;
    }

    // As the "+" and "-" buttons of the game, around the choices. Always false for these rows: the game's method skipped.
    private static bool Cycle(StringOption row, int step)
    {
        if (OptionOf(row) == null) return true;

        var count = row.Values.Length;
        row.Value = ((row.Value + step) % count + count) % count;
        row.OnValueChanged.Invoke(row);
        return false;
    }

    private static MapNames CurrentMap() => (MapNames)GameOptionsManager.Instance.CurrentGameOptions.MapId;

    /// <summary>
    ///     Maps whose settings are shown: in a game, the map played; in the lobby, the map of the lobby, or the maps the
    ///     random map can draw (only the ones at 100 % when there are some, as the draw does).
    /// </summary>
    private static HashSet<MapNames> ShownMaps()
    {
        var current = CurrentMap();
        if (ShipStatus.Instance || !RandomMapOptions.IsOn()) return [current];

        var maps = OptionGroupSingleton<RandomMapOptions>.Instance.Maps.Where(x => x.Chance.Value > 0).ToList();
        if (maps.Count == 0) return [current];

        if (maps.Exists(x => x.Chance.Value >= 100))
            maps = maps.Where(x => x.Chance.Value >= 100).ToList();

        return maps.Select(x => x.Map).ToHashSet();
    }

    // Settings shown, by category.
    private static List<List<IModdedOption>> ShownBySection()
    {
        var sections = Sections.Select(_ => new List<IModdedOption>()).ToList();
        if (GameOptionsManager.Instance?.CurrentGameOptions == null) return sections;

        var maps = ShownMaps();
        foreach (var (section, optionMaps, option) in Entries())
        {
            if (optionMaps.Any(maps.Contains)) sections[section].Add(option);
        }

        return sections;
    }

    private static int ShownKey()
    {
        var maps = ShownMaps();
        var key = 0;
        var entries = Entries();
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].Maps.Any(maps.Contains)) key |= 1 << i;
        }

        return key;
    }

    // Each category with rows shown at the place of the first category of the game, its header then its rows; the
    // categories of the game moved down by the room taken.
    private static void Layout()
    {
        if (!_anchor) return;

        _shownKey = ShownKey();
        var entries = Entries();
        var y = _anchor!.localPosition.y + _applied;
        var room = 0f;
        for (var s = 0; s < Headers.Count; s++)
        {
            var header = Headers[s];
            var shown = 0;
            for (var i = 0; i < Rows.Count && i < entries.Count; i++)
            {
                var row = Rows[i];
                if (entries[i].Section != s || !row) continue;

                var active = (_shownKey & (1 << i)) != 0;
                row!.gameObject.SetActive(active);
                if (!active) continue;

                if (shown == 0)
                {
                    if (header) header!.transform.localPosition = new Vector3(HeaderX, y, -2f);
                    y -= HeaderHeight;
                    room += HeaderHeight;
                }

                row.transform.localPosition = new Vector3(RowX, y, -2f);
                y -= RowHeight;
                room += RowHeight;
                shown++;
            }

            if (header) header!.gameObject.SetActive(shown > 0);
        }

        _room = room;
        MoveGameCategories(_room - _applied);
        _applied = _room;
        UpdateController();
    }

    // Everything from the first category of the game down (not the rows above it nor these categories) moved down.
    private static void MoveGameCategories(float shift)
    {
        if (Mathf.Approximately(shift, 0f)) return;

        var container = _anchor!.parent;
        var top = _anchor.localPosition.y + 0.1f;
        for (var i = 0; i < container.childCount; i++)
        {
            var child = container.GetChild(i);
            if (IsOwn(child)) continue;

            var position = child.localPosition;
            if (position.y < top) child.localPosition = new Vector3(position.x, position.y - shift, position.z);
        }
    }

    private static bool IsOwn(Transform transform) =>
        transform.name.StartsWith(HeaderPrefix, StringComparison.Ordinal) || transform.name.StartsWith(RowPrefix, StringComparison.Ordinal);

    private static CategoryHeaderMasked CreateHeader(GameOptionsMenu menu, Transform container, int section)
    {
        var header = Object.Instantiate(menu.categoryHeaderOrigin, container);
        header.SetHeader(SectionTitle(section), 20);
        header.transform.localScale = Vector3.one * 0.63f;
        header.name = HeaderPrefix + section;
        return header;
    }

    private static OptionBehaviour CreateRow(GameOptionsMenu menu, Transform container, IModdedOption option, int index)
    {
        var previous = option.OptionBehaviour;
        var behaviour = option.CreateOption(menu.checkboxOrigin, menu.numberOptionOrigin, menu.stringOptionOrigin, menu.playerOptionOrigin, container);

        // CreateOption makes the row the control of the option: MiraAPI would hide it with its group, hidden in the
        // tab of BetterOtherRoles (UpdateGroup). Its control given back (protected setter): MiraAPI leaves the row alone.
        AccessTools.Property(option.GetType(), nameof(IModdedOption.OptionBehaviour))?.SetValue(option, previous);
        behaviour.name = RowPrefix + index;
        behaviour.SetClickMask(menu.ButtonClickMask);
        if (AmongUsClient.Instance && !AmongUsClient.Instance.AmHost)
            behaviour.SetAsPlayer();

        return behaviour;
    }

    // Rows shown reachable with a controller, the hidden ones not.
    private static void UpdateController()
    {
        var selectable = _menu ? _menu!.ControllerSelectable : null;
        if (selectable == null) return;

        foreach (var row in Rows)
        {
            if (!row) continue;

            foreach (var element in row!.GetComponentsInChildren<UiElement>(true))
            {
                var wanted = row.gameObject.activeSelf && element.gameObject.activeSelf;
                if (wanted && !selectable.Contains(element)) selectable.Add(element);
                else if (!wanted) selectable.Remove(element);
            }
        }
    }

    private static Transform? FindBanner(GameOptionsMenu menu)
    {
        foreach (var transform in menu.GetComponentsInChildren<Transform>(true))
        {
            if (transform.name == BannerName && transform.gameObject.activeInHierarchy) return transform;
        }

        return null;
    }

    // Highest category header of the game under the banner (Impostors, under the lobby size); the game mode
    // category of MiraAPI is above the banner.
    private static Transform? FindFirstVanillaHeader(Transform banner)
    {
        var container = banner.parent;
        Transform? first = null;
        for (var i = 0; i < container.childCount; i++)
        {
            var child = container.GetChild(i);
            if (IsOwn(child) || child.localPosition.y >= banner.localPosition.y || !child.GetComponent<CategoryHeaderMasked>()) continue;

            if (!first || child.localPosition.y > first!.localPosition.y) first = child;
        }

        return first;
    }

    private static StringNames SectionTitle(int section) => MiraLocaleManager.GetOrCreateLocaleString(Sections[section].Key);

    // Value as the game shows it: "30s" for a duration, the name of the choice for the doors.
    private static string ValueString(IModdedOption option)
    {
        if (option is ModdedNumberOption number) return Data(number).GetValueString(number.Value);

        return option.Data.GetValueString(option.GetFloatData());
    }

    // Setting of the game for the durations (format "30s"): the options of MiraAPI only have one with a control.
    private static FloatGameSetting Data(ModdedNumberOption option)
    {
        if (ViewData.TryGetValue(option, out var data) && data) return data;

        data = ScriptableObject.CreateInstance<FloatGameSetting>();
        data.name = $"{RowPrefix}View";
        data.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        data.Type = OptionTypes.Float;
        data.Title = option.StringName;
        data.Increment = option.Increment;
        data.ValidRange = new FloatRange(option.Min, option.Max);
        data.SuffixType = NumberSuffixes.Seconds;
        data.FormatString = "0";
        ViewData[option] = data;
        return data;
    }
}
