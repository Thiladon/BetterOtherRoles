using HarmonyLib;
using MiraAPI.GameOptions;
using MiraAPI.Patches.Options;
using Reactor.Utilities.Attributes;
using UnityEngine;

namespace BetterOtherRoles.Patches.Options;

/// <summary>
///     Lobby game settings menu: opened again on the last page seen (mod and tab: Main / BetterOtherRoles, Presets,
///     Game, Roles...) instead of "Main > Game Presets". Kept while the game runs, also when coming back from a game,
///     but forgotten when leaving the lobby: a new one (local ones included) starts again on "Main > Game Presets".
///     The menu is created again at each opening: GameStartManager.ClickEdit instantiates it (first OnEnable, no parent
///     yet), then the game shows it a few frames later (second OnEnable, the real opening), selects the presets tab and
///     builds the tabs in their Start. The menu is kept hidden (<see cref="SettingsMenuHider"/>, moved out of the screen)
///     until the last page is applied and built, so it never shows the presets first, then slides in from the right.
/// </summary>
[HarmonyPatch]
public static class SettingsLastPagePatches
{
    private static int _modIndex;
    private static MenuCategory _menu = MenuCategory.Preset;

    /// <summary>
    ///     Gets a value indicating whether the menu is being opened on the last page (tab changes not recorded).
    /// </summary>
    internal static bool IsOpening { get; set; }

    /// <summary>
    ///     Records the page chosen by the player (MiraAPI handles every tab change, the mod changes included).
    /// </summary>
    [HarmonyPatch(typeof(MenuState), "ChangeTabPatch")]
    [HarmonyPostfix]
    public static void ChangeTabPostfix(MenuState __instance, int tabNum, bool previewOnly)
    {
        // Default tab of the instantiation by ClickEdit (no parent yet) or of the opening: not chosen by the player.
        if (previewOnly || IsOpening || !AmongUsClient.Instance || !__instance.transform.parent)
        {
            return;
        }

        _modIndex = __instance.CurrentModIdx;
        _menu = (MenuCategory)tabNum;
    }

    /// <summary>
    ///     Leaving the lobby (or being disconnected) forgets the last page: the next lobby starts again on
    ///     "Main > Game Presets". "Play Again" joins the same lobby again without leaving it, the page is kept.
    /// </summary>
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.ExitGame))]
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnDisconnected))]
    [HarmonyPostfix]
    public static void LeaveLobbyPostfix()
    {
        _modIndex = 0;
        _menu = MenuCategory.Preset;
    }

    /// <summary>
    ///     Real opening of the menu, before the game selects its default tab: hidden until built, on the last page.
    /// </summary>
    [HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.OnEnable))]
    [HarmonyPrefix]
    public static void OnEnablePrefix(GameSettingMenu __instance)
    {
        // Instantiation by ClickEdit: not shown yet.
        if (IsOpening || !AmongUsClient.Instance || !__instance.transform.parent)
        {
            return;
        }

        var restore = _modIndex != 0 || _menu != MenuCategory.Preset;
        var hider = __instance.gameObject.GetComponent<SettingsMenuHider>() ?? __instance.gameObject.AddComponent<SettingsMenuHider>();
        hider.Begin(restore, _modIndex, _menu);
    }

    /// <summary>
    ///     Applies the last page and records it as the current one.
    /// </summary>
    internal static void Restore(GameSettingMenu menu, int modIndex, MenuCategory page)
    {
        var state = MenuState.Instance;
        if (!state)
        {
            return;
        }

        for (var i = 0; i <= MenuState.ModCount && state.CurrentModIdx != modIndex; i++)
        {
            state.NextMod();
        }

        menu.ChangeTab((int)page, false);
        _modIndex = state.CurrentModIdx;
        _menu = state.CurrentMenu;
    }
}

/// <summary>
///     Keeps the game settings menu hidden while it opens, out of the screen: moved aside in LateUpdate (after the game,
///     which may place the menu meanwhile, before it is drawn). Not scaled to 0: the headers are laid out with world
///     positions (<see cref="GameOptionsHeaderPatches"/>), wrong in a menu of scale 0.
///     The last page is applied once the game selected its default tab and built the tabs, then the menu stays hidden
///     while the page builds its content (options added and headers laid out over several frames): once its texts stop
///     changing, the window parts listed in <see cref="SlidingNames"/> slide in from the right of the screen (ease out);
///     the starry background ("Background") shows at once, without sliding.
/// </summary>
[RegisterInIl2Cpp]
public sealed class SettingsMenuHider(nint cppPtr) : MonoBehaviour(cppPtr)
{
    // Frame of the opening when the last page is applied (the tabs start on the opening frame).
    private const int RestoreFrame = 2;

    // Frames without any text change before the menu is shown.
    private const int StableFrames = 3;

    // Shown anyway after this frame (about a second).
    private const int MaxFrame = 120;

    // Out of the screen, on the side.
    private static readonly Vector3 HiddenOffset = new(1000f, 0f, 0f);

    // Slide in: from the right of the screen, in seconds.
    private const float SlideDuration = 0.3f;
    private const float SlideDistance = 10f;

    // Starry background of the menu, covering the screen: shown at once, without sliding.
    private const string BackgroundName = "Background";

    // Parts of the window (direct children of the menu), sliding in. A part missing here stays still: a warning is logged.
    private static readonly HashSet<string> SlidingNames =
    [
        "PanelSprite", "GameSettingsLabel", "What Is This?", "LeftPanel", "MainArea", "CloseButton", "SelectedMod",
        "RightArrowButton", "LeftArrowButton",
    ];

    private static readonly HashSet<string> WarnedNames = [];

    private int _frame;
    private int _modIndex;
    private MenuCategory _page;
    private Vector3 _position;
    private bool _hiding;
    private int _layout;
    private int _stable;
    private bool _restore;
    private float _slideTime = -1f;
    private readonly List<(Transform Part, Vector3 Position)> _sliding = [];

    public void Begin(bool restore, int modIndex, MenuCategory page)
    {
        _restore = restore;
        _slideTime = -1f;
        _modIndex = modIndex;
        _page = page;
        _frame = 0;
        _layout = 0;
        _stable = 0;
        _position = transform.localPosition;
        transform.localPosition = _position + HiddenOffset;
        _hiding = true;
        SettingsLastPagePatches.IsOpening = restore;
        enabled = true;
    }

    private void LateUpdate()
    {
        if (_slideTime >= 0f)
        {
            Slide();
            return;
        }

        if (!_hiding)
        {
            enabled = false;
            return;
        }

        // The game may place the menu while it opens: keep its place.
        if (transform.localPosition != _position + HiddenOffset)
        {
            _position = transform.localPosition;
        }

        _frame++;
        if (_frame == RestoreFrame && _restore)
        {
            try
            {
                SettingsLastPagePatches.Restore(GetComponent<GameSettingMenu>(), _modIndex, _page);
            }
            finally
            {
                SettingsLastPagePatches.IsOpening = false;
            }
        }

        // Built once the texts (count and places) stop changing.
        var layout = GetLayout();
        _stable = layout == _layout ? _stable + 1 : 0;
        _layout = layout;
        if (_frame <= RestoreFrame || (_stable < StableFrames && _frame < MaxFrame))
        {
            transform.localPosition = _position + HiddenOffset;
            return;
        }

        StartSlide();
    }

    private void StartSlide()
    {
        _hiding = false;
        SettingsLastPagePatches.IsOpening = false;
        transform.localPosition = _position;

        // The window parts listed slide, the starry background stays.
        _sliding.Clear();
        for (var i = 0; i < transform.childCount; i++)
        {
            var part = transform.GetChild(i);
            if (SlidingNames.Contains(part.name))
            {
                _sliding.Add((part, part.localPosition));
            }
            else if (part.name != BackgroundName && WarnedNames.Add(part.name))
            {
                Warning($"Settings menu: unknown part '{part.name}', it does not slide (add it to SlidingNames).");
            }
        }

        _slideTime = 0f;
        Slide();
    }

    private void Slide()
    {
        _slideTime += Time.deltaTime;
        var progress = Mathf.Clamp01(_slideTime / SlideDuration);
        var eased = 1f - Mathf.Pow(1f - progress, 3f);
        var offset = (1f - eased) * SlideDistance / Mathf.Max(transform.lossyScale.x, 0.0001f);
        foreach (var (part, position) in _sliding)
        {
            if (part)
            {
                part.localPosition = position + new Vector3(offset, 0f, 0f);
            }
        }

        if (progress >= 1f)
        {
            Show();
        }
    }

    // Places relative to the menu, the same once it is moved back.
    private int GetLayout()
    {
        var hash = 17;
        foreach (var text in GetComponentsInChildren<TMPro.TextMeshPro>())
        {
            var position = transform.InverseTransformPoint(text.transform.position);
            hash = (hash * 31) + Mathf.RoundToInt(position.x * 100f);
            hash = (hash * 31) + Mathf.RoundToInt(position.y * 100f);
            hash = (hash * 31) + (text.text?.Length ?? 0);
        }

        return hash;
    }

    private void OnDisable()
    {
        if (_hiding || _slideTime >= 0f)
        {
            Show();
        }
    }

    private void Show()
    {
        _hiding = false;
        _slideTime = -1f;
        SettingsLastPagePatches.IsOpening = false;
        transform.localPosition = _position;
        foreach (var (part, position) in _sliding)
        {
            if (part)
            {
                part.localPosition = position;
            }
        }

        _sliding.Clear();
        enabled = false;
    }
}
