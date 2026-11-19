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
///     Durations of the sabotages (<see cref="SabotageOptions"/>), in a "Sabotages" category before the ones of the
///     game in the vanilla game settings tab, in the settings view of the lobby and of the settings of the guide. Only the maps that
///     can be played are shown: the map of the lobby, or the maps the random map can draw; in a game, the map played.
///     The host sets the countdown when a sabotage starts (then sent to everyone by the game).
/// </summary>
[HarmonyPatch]
public static class SabotageSettingsPatches
{
    private const string HeaderName = "BorSabotagesHeader";
    private const string RowPrefix = "BorSabotageOption";

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

    // Countdown of the crash of the Airship set by the game (HeliSabotageSystem.UpdateSystem).
    private const float GameCrashCountdown = 90f;

    // Control of a MiraAPI option (protected setter).
    private static readonly PropertyInfo OptionBehaviourProperty =
        AccessTools.Property(typeof(ModdedNumberOption), nameof(ModdedNumberOption.OptionBehaviour));

    // Mod shown in the settings view of the lobby (0: the game), private in MiraAPI.
    private static readonly PropertyInfo SelectedModIdxProperty =
        AccessTools.Property(typeof(LobbyViewPanePatches), "SelectedModIdx");

    // Rows of the vanilla tab, in the order of SabotageOptions.All.
    private static readonly List<NumberOption?> Rows = [];
    private static readonly Dictionary<ModdedNumberOption, FloatGameSetting> ViewData = [];

    private static GameOptionsMenu? _menu;
    private static CategoryHeaderMasked? _header;

    // Header of the first category of the game (Impostors), above which the category goes.
    private static Transform? _anchor;

    // Rows shown (bits of their index), to place them again when it changes.
    private static int _shownKey = -1;
    private static float _room;

    // Room by which the categories of the game are moved down (kept with the tab, MiraAPI keeps it).
    private static float _applied;

    private static SabotageOptions Options => OptionGroupSingleton<SabotageOptions>.Instance;

    /// <summary>
    ///     Gets the room taken by the category in the scrolled height (<see cref="SettingsScrollPatches"/>).
    /// </summary>
    public static float Room => _header && _header!.gameObject.activeInHierarchy ? _room : 0f;

    /// <summary>
    ///     Category before the ones of the game in the vanilla tab: made once with the tab (MiraAPI keeps it), found again after.
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

        var existing = container.Find(HeaderName);
        if (existing)
        {
            _header = existing.GetComponent<CategoryHeaderMasked>();
        }
        else
        {
            _header = CreateHeader(__instance, container);
            _applied = 0f;
        }

        Rows.Clear();
        var options = Options.All.ToList();
        for (var i = 0; i < options.Count; i++)
        {
            var row = container.Find(RowPrefix + i);
            if (row)
            {
                var option = row.GetComponent<NumberOption>();

                // MiraAPI takes back every option of the tab as a vanilla one: these are MiraAPI options.
                __instance.Children?.Remove(option);
                Rows.Add(option);
            }
            else
            {
                Rows.Add(CreateRow(__instance, container, options[i].Option, i));
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
    ///     Shows the values set by the host (MiraAPI only updates the control it knows, not these rows).
    /// </summary>
    public static void RefreshValues()
    {
        if (Rows.Count == 0) return;

        var options = Options.All.ToList();
        for (var i = 0; i < Rows.Count && i < options.Count; i++)
        {
            var row = Rows[i];
            var value = options[i].Option.Value;
            if (row && !Mathf.Approximately(row!.Value, value)) row.Value = value;
        }
    }

    /// <summary>
    ///     Settings view of the lobby: the category before the ones of the game (they are moved down to make room).
    /// </summary>
    [HarmonyPatch(typeof(LobbyViewSettingsPane), nameof(LobbyViewSettingsPane.DrawNormalTab))]
    [HarmonyPostfix]
    public static void DrawNormalTabPostfix(LobbyViewSettingsPane __instance)
    {
        // Drawn by MiraAPI instead of the game for the tab of a mod.
        if (SelectedModIdxProperty?.GetValue(null) is int index && index != 0) return;

        var shown = ShownOptions();
        var infos = __instance.settingsInfo;
        if (shown.Count == 0 || infos == null || infos.Count == 0) return;

        // Height of the category: header, lines of 2 panels, then the gap before the next category (as the game does).
        var y = infos[0].transform.localPosition.y;
        var room = ViewHeaderHeight + ViewRowHeight * ((shown.Count + 1) / 2);
        foreach (var info in infos)
        {
            if (info) info.transform.localPosition += Vector3.down * room;
        }

        var header = Object.Instantiate(__instance.categoryHeaderOrigin);
        header.SetHeader(HeaderTitle(), 61);
        header.transform.SetParent(__instance.settingsContainer);
        header.transform.localScale = Vector3.one;
        header.transform.localPosition = new Vector3(ViewHeaderX, y, -2f);
        infos.Add(header.gameObject);
        y -= ViewHeaderHeight;

        for (var i = 0; i < shown.Count; i++)
        {
            if (i % 2 == 0 && i > 0) y -= ViewRowHeight;

            var data = Data(shown[i]);
            var panel = Object.Instantiate(__instance.infoPanelOrigin);
            panel.transform.SetParent(__instance.settingsContainer);
            panel.transform.localScale = Vector3.one;
            panel.transform.localPosition = new Vector3(i % 2 == 0 ? ViewLeftX : ViewRightX, y, -2f);
            panel.SetInfo(data.Title, data.GetValueString(shown[i].Value), 61);
            infos.Add(panel.gameObject);
        }

        __instance.scrollBar.CalculateAndSetYBounds(infos.Count + 10, 2f, 6f, ViewRowHeight);
    }

    /// <summary>
    ///     Title and value of each duration shown, for the role guide (list of the settings of the game).
    /// </summary>
    /// <returns>The lines.</returns>
    public static List<(string Title, string Value)> GuideLines()
    {
        var lines = new List<(string Title, string Value)>();
        if (!TranslationController.Instance) return lines;

        foreach (var option in ShownOptions())
        {
            var data = Data(option);
            lines.Add((TranslationController.Instance.GetString(data.Title), data.GetValueString(option.Value)));
        }

        return lines;
    }

    // Reactor (Skeld, dlekS, MIRA HQ, Polus, The Fungle): run by the host when a player sabotages or repairs.
    [HarmonyPatch(typeof(ReactorSystemType), nameof(ReactorSystemType.UpdateSystem))]
    [HarmonyPrefix]
    public static void ReactorUpdatePrefix(ReactorSystemType __instance, out bool __state) => __state = __instance.IsActive;

    [HarmonyPatch(typeof(ReactorSystemType), nameof(ReactorSystemType.UpdateSystem))]
    [HarmonyPostfix]
    public static void ReactorUpdatePostfix(ReactorSystemType __instance, bool __state)
    {
        if (__state || !__instance.IsActive || Options.Reactor(CurrentMap()) is not { } duration) return;

        __instance.Countdown = duration;
    }

    // Oxygen (Skeld, dlekS, MIRA HQ).
    [HarmonyPatch(typeof(LifeSuppSystemType), nameof(LifeSuppSystemType.UpdateSystem))]
    [HarmonyPrefix]
    public static void OxygenUpdatePrefix(LifeSuppSystemType __instance, out bool __state) => __state = __instance.IsActive;

    [HarmonyPatch(typeof(LifeSuppSystemType), nameof(LifeSuppSystemType.UpdateSystem))]
    [HarmonyPostfix]
    public static void OxygenUpdatePostfix(LifeSuppSystemType __instance, bool __state)
    {
        if (__state || !__instance.IsActive || Options.Oxygen(CurrentMap()) is not { } duration) return;

        __instance.Countdown = duration;
    }

    // Crash of the Airship: the game sets 90 s each time it is sabotaged (even while it runs).
    [HarmonyPatch(typeof(HeliSabotageSystem), nameof(HeliSabotageSystem.UpdateSystem))]
    [HarmonyPrefix]
    public static void CrashUpdatePrefix(HeliSabotageSystem __instance, out float __state) => __state = __instance.Countdown;

    [HarmonyPatch(typeof(HeliSabotageSystem), nameof(HeliSabotageSystem.UpdateSystem))]
    [HarmonyPostfix]
    public static void CrashUpdatePostfix(HeliSabotageSystem __instance, float __state)
    {
        if (Mathf.Approximately(__instance.Countdown, __state) || !Mathf.Approximately(__instance.Countdown, GameCrashCountdown)) return;

        __instance.Countdown = Options.AirshipCrash.Value;
    }

    // Mushroom mixup of The Fungle: time until everyone gets their look back.
    [HarmonyPatch(typeof(MushroomMixupSabotageSystem), nameof(MushroomMixupSabotageSystem.UpdateSystem))]
    [HarmonyPrefix]
    public static void MixupUpdatePrefix(MushroomMixupSabotageSystem __instance, out bool __state) => __state = __instance.IsActive;

    [HarmonyPatch(typeof(MushroomMixupSabotageSystem), nameof(MushroomMixupSabotageSystem.UpdateSystem))]
    [HarmonyPostfix]
    public static void MixupUpdatePostfix(MushroomMixupSabotageSystem __instance, bool __state)
    {
        if (__state || !__instance.IsActive) return;

        __instance.currentSecondsUntilHeal = Options.FungleMixup.Value;
    }

    private static MapNames CurrentMap() => (MapNames)GameOptionsManager.Instance.CurrentGameOptions.MapId;

    /// <summary>
    ///     Maps whose sabotages are shown: in a game, the map played; in the lobby, the map of the lobby, or the maps the
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

    private static List<ModdedNumberOption> ShownOptions()
    {
        if (GameOptionsManager.Instance?.CurrentGameOptions == null) return [];

        var maps = ShownMaps();
        return Options.All.Where(x => x.Maps.Any(maps.Contains)).Select(x => x.Option).ToList();
    }

    private static int ShownKey()
    {
        var maps = ShownMaps();
        var key = 0;
        var i = 0;
        foreach (var (optionMaps, _) in Options.All)
        {
            if (optionMaps.Any(maps.Contains)) key |= 1 << i;
            i++;
        }

        return key;
    }

    // Header at the place of the first category of the game, then the rows shown; the categories of the game moved
    // down by the room taken.
    private static void Layout()
    {
        if (!_anchor || !_header) return;

        _shownKey = ShownKey();
        var y = _anchor!.localPosition.y + _applied;
        _header!.transform.localPosition = new Vector3(HeaderX, y, -2f);
        y -= HeaderHeight;

        var shown = 0;
        for (var i = 0; i < Rows.Count; i++)
        {
            var row = Rows[i];
            if (!row) continue;

            var active = (_shownKey & (1 << i)) != 0;
            row!.gameObject.SetActive(active);
            if (!active) continue;

            row.transform.localPosition = new Vector3(RowX, y, -2f);
            y -= RowHeight;
            shown++;
        }

        _header.gameObject.SetActive(shown > 0);
        _room = shown > 0 ? HeaderHeight + RowHeight * shown : 0f;
        MoveGameCategories(_room - _applied);
        _applied = _room;
        UpdateController();
    }

    // Everything from the first category of the game down (not the rows above it nor this category) moved down.
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
        transform.name == HeaderName || transform.name.StartsWith(RowPrefix, StringComparison.Ordinal);

    private static CategoryHeaderMasked CreateHeader(GameOptionsMenu menu, Transform container)
    {
        var header = Object.Instantiate(menu.categoryHeaderOrigin, container);
        header.SetHeader(HeaderTitle(), 20);
        header.transform.localScale = Vector3.one * 0.63f;
        header.name = HeaderName;
        return header;
    }

    private static NumberOption? CreateRow(GameOptionsMenu menu, Transform container, ModdedNumberOption option, int index)
    {
        var previous = option.OptionBehaviour;
        var behaviour = option.CreateOption(menu.checkboxOrigin, menu.numberOptionOrigin, menu.stringOptionOrigin, menu.playerOptionOrigin, container);

        // CreateOption makes the row the control of the option: MiraAPI would hide it with its group, hidden in the
        // tab of BetterOtherRoles (UpdateGroup). Its control given back: MiraAPI leaves the row alone.
        OptionBehaviourProperty.SetValue(option, previous);
        behaviour.name = RowPrefix + index;
        behaviour.SetClickMask(menu.ButtonClickMask);
        if (AmongUsClient.Instance && !AmongUsClient.Instance.AmHost)
            behaviour.SetAsPlayer();

        return behaviour.TryCast<NumberOption>();
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

    private static StringNames HeaderTitle() => MiraLocaleManager.GetOrCreateLocaleString("BetterOtherRoles.Setting.Sabotages");

    // Setting of the game for the settings view (format "30s"): the options of MiraAPI only have one with a control.
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
