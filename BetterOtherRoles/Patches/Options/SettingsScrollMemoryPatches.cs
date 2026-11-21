using System.Collections;
using System.Reflection;
using HarmonyLib;
using MiraAPI.GameOptions;
using MiraAPI.Patches.Options;
using MiraAPI.Translation;
using Reactor.Utilities;
using TMPro;
using UnityEngine;

namespace BetterOtherRoles.Patches.Options;

/// <summary>
///     Scroll position of the lobby settings, kept when they are closed and opened again (both reopen at the top):
///     - game settings menu (edition): created again at each opening, on the last page (<see cref="SettingsLastPagePatches"/>);
///       the page of the role opened (roles of a mod) is opened again, then the position of the page is put back, before
///       the menu slides in;
///     - settings view of the lobby: the same object shown again, back on its first tab (the mod page of MiraAPI is kept);
///       the tab and the position are put back.
///     Kept while the game runs, forgotten when leaving the lobby (as the last page).
/// </summary>
[HarmonyPatch]
public static class SettingsScrollMemoryPatches
{
    // Private or internal members of MiraAPI: mod page of the settings view, role opened, closed groups.
    private static readonly PropertyInfo? ViewModIndex = AccessTools.Property(typeof(LobbyViewPanePatches), "SelectedModIdx");
    private static readonly PropertyInfo? CurrentRole = AccessTools.Property(typeof(RoleSettingMenuPatches), "CurrentRole");
    private static readonly MethodInfo? OpenRole = AccessTools.Method(typeof(RoleSettingMenuPatches), "ChangeTab", [typeof(RoleBehaviour), typeof(RolesSettingsMenu)]);
    private static readonly PropertyInfo? GroupHidden = AccessTools.Property(typeof(AbstractOptionGroup), "AllOptionsHidden");
    private static readonly PropertyInfo? GroupHeader = AccessTools.Property(typeof(AbstractOptionGroup), "Header");

    // Frames without any text change before a page is taken as built, and the most frames waited.
    private const int StableFrames = 3;
    private const int MaxFrames = 120;

    private static (int Mod, MenuCategory Page, RoleBehaviour? Role, float Y)? _edit;
    private static (int Mod, StringNames Tab, float Y)? _view;

    // Page of a role left for another tab of the menu (MiraAPI goes back to the list of the roles).
    private static (int Mod, RoleBehaviour Role, float Y)? _leftRole;

    /// <summary>
    ///     Tab changes of the game settings menu (all done by MiraAPI): none while Escape closes the menu (the key sets
    ///     the controller mode, whose default button "Game Presets" shows its tab before the menu closes); the page of
    ///     the role kept when the roles tab is left.
    /// </summary>
    [HarmonyPatch(typeof(MenuState), "ChangeTabPatch")]
    [HarmonyPrefix]
    public static bool ChangeTabPrefix(MenuState __instance, GameSettingMenu menu, int tabNum, bool previewOnly)
    {
        if (Input.GetKey(KeyCode.Escape)) return false;

        if (previewOnly || SettingsLastPagePatches.IsOpening || __instance.CurrentMenu != MenuCategory.Roles || tabNum == (int)MenuCategory.Roles) return true;

        var role = RolePage(menu, __instance);
        _leftRole = role ? (__instance.CurrentModIdx, role!, menu.RoleSettingsTab.scrollBar.Inner.localPosition.y) : null;
        return true;
    }

    /// <summary>
    ///     Back on the roles tab: the page of the role left opened again, once the list of the roles is built.
    /// </summary>
    [HarmonyPatch(typeof(MenuState), "ChangeTabPatch")]
    [HarmonyPostfix]
    public static void ChangeTabPostfix(MenuState __instance, GameSettingMenu menu, int tabNum, bool previewOnly)
    {
        if (previewOnly || SettingsLastPagePatches.IsOpening || tabNum != (int)MenuCategory.Roles) return;
        if (_leftRole is not { } left || left.Mod != __instance.CurrentModIdx || !left.Role) return;

        _leftRole = null;
        Coroutines.Start(CoOpenRolePage(menu, left.Role, left.Y));
    }

    private static IEnumerator CoOpenRolePage(GameSettingMenu menu, RoleBehaviour role, float y)
    {
        yield return CoWaitBuilt(menu);
        if (!menu || !menu.RoleSettingsTab.gameObject.activeSelf || OpenRole == null) yield break;

        OpenRole.Invoke(null, [role, menu.RoleSettingsTab]);
        yield return CoWaitBuilt(menu);
        if (menu && menu.RoleSettingsTab.AdvancedRolesSettings.activeSelf) ScrollTo(menu.RoleSettingsTab.scrollBar, y);
    }

    private static IEnumerator CoWaitBuilt(GameSettingMenu menu)
    {
        var layout = 0;
        var stable = 0;
        for (var frame = 0; frame < MaxFrames && stable < StableFrames && menu; frame++)
        {
            yield return null;
            if (!menu) yield break;

            var current = SettingsMenuHider.GetLayout(menu.transform);
            stable = current == layout ? stable + 1 : 0;
            layout = current;
        }
    }

    // Role of a mod whose page is opened (MiraAPI), or none: list of the roles, or roles of the game.
    private static RoleBehaviour? RolePage(GameSettingMenu menu, MenuState state) =>
        state.CurrentMenu == MenuCategory.Roles && state.CurrentModIdx != 0 && menu.RoleSettingsTab.AdvancedRolesSettings.activeSelf
            ? CurrentRole?.GetValue(null) as RoleBehaviour
            : null;

    /// <summary>
    ///     Game settings menu closed: page, role opened and position.
    /// </summary>
    [HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.OnDisable))]
    [HarmonyPrefix]
    public static void EditDisablePrefix(GameSettingMenu __instance)
    {
        // Instantiation by ClickEdit (never shown), or closed while opening: the last position is kept.
        var hider = __instance.GetComponent<SettingsMenuHider>();
        var state = __instance.GetComponent<MenuState>();
        if (!hider || hider!.IsHiding || !state) return;

        var page = state.CurrentMenu;
        var role = RolePage(__instance, state);

        // Page of a role of the game: not kept.
        if (!role && page == MenuCategory.Roles && __instance.RoleSettingsTab.AdvancedRolesSettings.activeSelf)
        {
            _edit = null;
            return;
        }

        var scroller = EditScroller(__instance, page);
        _edit = scroller ? (state.CurrentModIdx, page, role, scroller!.Inner.localPosition.y) : null;    }

    /// <summary>
    ///     Opens again the page of the role opened when the menu was closed (the page of the menu built).
    /// </summary>
    /// <param name="menu">The menu.</param>
    /// <returns>Whether the page is opened (built over the next frames).</returns>
    internal static bool OpenRolePage(GameSettingMenu menu)
    {
        var state = menu.GetComponent<MenuState>();
        if (_edit is not { Role: { } role } saved || !role || !state || OpenRole == null) return false;
        if (state.CurrentModIdx != saved.Mod || state.CurrentMenu != MenuCategory.Roles) return false;

        OpenRole.Invoke(null, [role, menu.RoleSettingsTab]);
        return true;
    }

    /// <summary>
    ///     Puts back the position of the page the game settings menu opened on (built, still hidden).
    /// </summary>
    /// <param name="menu">The menu.</param>
    internal static void RestoreEdit(GameSettingMenu menu)
    {
        var state = menu.GetComponent<MenuState>();
        if (!state) return;

        SyncHeaders(state);
        if (_edit is not { } saved || state.CurrentModIdx != saved.Mod || state.CurrentMenu != saved.Page) return;
        if (saved.Role && !menu.RoleSettingsTab.AdvancedRolesSettings.activeSelf) return;

        var scroller = EditScroller(menu, saved.Page);
        if (scroller) ScrollTo(scroller!, saved.Y);
    }

    /// <summary>
    ///     Settings view closed: tab and position.
    /// </summary>
    [HarmonyPatch(typeof(LobbyViewSettingsPane), nameof(LobbyViewSettingsPane.OnDisable))]
    [HarmonyPrefix]
    public static void ViewDisablePrefix(LobbyViewSettingsPane __instance)
    {
        if (ViewModIndex?.GetValue(null) is not int mod || !__instance.scrollBar) return;

        _view = (mod, __instance.currentTab, __instance.scrollBar.Inner.localPosition.y);
    }

    /// <summary>
    ///     Settings view shown again: after the game (first tab) and MiraAPI (mod page drawn, scrolled to the top).
    /// </summary>
    [HarmonyPatch(typeof(LobbyViewSettingsPane), nameof(LobbyViewSettingsPane.OnEnable))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    public static void ViewEnablePostfix(LobbyViewSettingsPane __instance)
    {
        if (_view is not { } saved || ViewModIndex?.GetValue(null) is not int mod || mod != saved.Mod || !__instance.scrollBar) return;

        if (__instance.currentTab != saved.Tab) __instance.ChangeTab(saved.Tab);
        ScrollTo(__instance.scrollBar, saved.Y);
    }

    /// <summary>
    ///     Leaving the lobby (or being disconnected) forgets the positions, as the last page.
    /// </summary>
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.ExitGame))]
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnDisconnected))]
    [HarmonyPostfix]
    public static void LeaveLobbyPostfix()
    {
        _edit = null;
        _view = null;
        _leftRole = null;
    }

    // Scroller of a page of the game settings menu (none for the presets).
    private static Scroller? EditScroller(GameSettingMenu menu, MenuCategory page) => page switch
    {
        MenuCategory.Preset => null,
        MenuCategory.Roles => menu.RoleSettingsTab.scrollBar,
        _ => menu.GameSettingsTab.scrollBar,
    };

    // MiraAPI keeps the closed groups between two openings, but writes "Click to close" on their new header.
    private static void SyncHeaders(MenuState state)
    {
        if (state.CurrentModIdx == 0 || GroupHidden == null || GroupHeader == null) return;

        foreach (var group in state.CurrentMod.OptionGroups)
        {
            var hidden = GroupHidden.GetValue(group) is true;
            var header = GroupHeader.GetValue(group) as CategoryHeaderMasked;
            if (!hidden || !header) continue;

            foreach (var text in header!.GetComponentsInChildren<TextMeshPro>(true))
            {
                if (text != header.Title)
                    text.text = $"<size=70%>({MiraLocaleManager.Get("MiraApi.GameSetting.Global.ClickToOpen", "Click to open")})</size>";
            }
        }
    }

    private static void ScrollTo(Scroller scroller, float y)
    {
        var bounds = scroller.ContentYBounds;
        var position = scroller.Inner.localPosition;
        position.y = Mathf.Clamp(y, bounds.min, Mathf.Max(bounds.min, bounds.max));
        scroller.Inner.localPosition = position;
        scroller.velocity = Vector2.zero;
        scroller.UpdateScrollBars();
    }
}
