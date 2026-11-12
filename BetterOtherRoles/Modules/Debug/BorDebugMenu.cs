using System.Collections;
using System.Reflection;
using BetterOtherRoles.Assets;
using BetterOtherRoles.Patches.Misc;
using HarmonyLib;
using MiraAPI.Hud;
using MiraAPI.Roles;
using MiraAPI.Translation;
using MiraAPI.Utilities.Assets;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Modules.Debug;

/// <summary>
///     Debug menu of the host (<see cref="BorDebugActions"/>), opened by the Debug button under the role guide: a single
///     phone (<see cref="BorPhoneMenu"/>) showing the actions, then the players and, to change a role, the roles.
/// </summary>
public static class BorDebugMenu
{
    private static readonly Color ActionColor = Color.white;
    private static readonly Color DangerColor = new Color32(255, 80, 80, 255);

    /// <summary>
    ///     Opens the list of actions, or closes the menu when it is open (keybind <see cref="Keybinds.BorKeybinds.DebugMenu"/>).
    /// </summary>
    public static void Toggle()
    {
        if (BorPhoneMenu.Current is { } menu)
        {
            menu.ForceClose();
            return;
        }

        Open();
    }

    /// <summary>
    ///     Opens the list of actions.
    /// </summary>
    public static void Open()
    {
        if (!BorDebugActions.CanUse || !PlayerControl.LocalPlayer) return;

        // The haunt menu of a dead player gives way to the debug menu, any other minigame keeps it closed.
        if (HauntMenuButtonsPatches.IsHauntMenuOpen)
            Minigame.Instance.Close();
        else if (Minigame.Instance)
            return;

        PlayerControl.LocalPlayer.NetTransform.Halt();
        BorPhoneMenu.Open(
        [
            Opens(Text("SetRole", "Change role"), ActionColor, m => m.Show(Players(_ => true, ShowRoles, opens: true))),
            Opens(Text("ResetCooldowns", "Reset cooldowns"), ActionColor, m => m.Show(Players(IsAlive, (_, x) => x.RpcResetCooldowns()))),
            Opens(Text("Revive", "Revive"), ActionColor, m => m.Show(Players(x => !IsAlive(x), (_, x) => x.RpcRevive()))),
            Opens(Text("Kill", "Kill"), DangerColor, m => m.Show(Players(IsAlive, (_, x) => BorDebugActions.Kill(x)))),
            Does(Text("Meeting", "Call a meeting"), ActionColor, BorDebugActions.StartMeeting),
            Opens(Text("TeleportTo", "Go to a player"), ActionColor, m => m.Show(Players(IsOtherAlive, (_, x) => BorDebugActions.Teleport(PlayerControl.LocalPlayer, x)))),
            Opens(Text("Bring", "Bring a player"), ActionColor, m => m.Show(Players(IsOtherAlive, (_, x) => BorDebugActions.Teleport(x, PlayerControl.LocalPlayer)))),
            Does(Text("EndCrewmates", "End: crewmates win"), Palette.CrewmateBlue, () => BorDebugActions.EndGame(GameOverReason.CrewmatesByVote)),
            Does(Text("EndImpostors", "End: impostors win"), Palette.ImpostorRed, () => BorDebugActions.EndGame(GameOverReason.ImpostorsByKill)),
        ]);
    }

    private static BorPhoneMenu.Entry Opens(string text, Color color, Action<BorPhoneMenu> show) => new(text, color, show, Opens: true);

    private static BorPhoneMenu.Entry Does(string text, Color color, Action action) => new(text, color, _ => action());

    private static bool IsAlive(PlayerControl player) => player.Data != null && !player.Data.IsDead;

    private static bool IsOtherAlive(PlayerControl player) => IsAlive(player) && !player.AmOwner;

    // The players matching, then the action on the one picked (done once the menu is closed, unless it opens a list).
    private static List<BorPhoneMenu.Entry> Players(Func<PlayerControl, bool> match, Action<BorPhoneMenu, PlayerControl> onPick, bool opens = false)
    {
        return PlayerControl.AllPlayerControls.ToArray()
            .Where(x => x.Data != null && !x.Data.Disconnected && match(x))
            .Select(x => new BorPhoneMenu.Entry(x.Data.PlayerName, Color.white, m =>
            {
                if (x && BorDebugActions.CanUse)
                    onPick(m, x);
            }, opens, x))
            .ToList();
    }

    private static void ShowRoles(BorPhoneMenu menu, PlayerControl player)
    {
        menu.Show(BorDebugActions.GetRoles()
            .Select(role => new BorPhoneMenu.Entry(GetRoleName(role), GetRoleColor(role), _ => BorDebugActions.SetRole(player, role)))
            .OrderBy(x => x.Text, StringComparer.CurrentCultureIgnoreCase));
    }

    private static string GetRoleName(RoleBehaviour role)
    {
        return role is ICustomRole custom ? custom.RoleName : TranslationController.Instance.GetString(role.StringName);
    }

    private static Color GetRoleColor(RoleBehaviour role)
    {
        return role is ICustomRole custom ? custom.RoleColor : role.TeamColor;
    }

    private static string Text(string key, string english)
    {
        return MiraLocaleManager.Get("BetterOtherRoles.Debug.Menu." + key, english);
    }
}

/// <summary>
///     MiraAPI phone menu whose page is replaced by the next list (<see cref="Show"/>) instead of opening a new phone, with
///     a back arrow under the close button to the previous list. A panel shows a player, or a text alone (centered).
/// </summary>
public sealed class BorPhoneMenu : CustomPaginableMenu
{
    // The panels start lower than in MiraAPI, and the search (placed by MiraAPI from the panels) a little lower: a gap
    // between them, the "Search" label inside the phone screen.
    private const float PanelsDown = 0.35f;
    private const float SearchDown = 0.05f;

    // The back arrow is under the close button.
    private const float BackArrowDown = 0.6f;

    private static readonly FieldInfo SearchTextField = AccessTools.Field(typeof(CustomPaginableMenu), "searchText");
    private static readonly FieldInfo SearchBoxField = AccessTools.Field(typeof(CustomPaginableMenu), "searchTextbox");
    private static readonly MethodInfo FilteredEntriesMethod = AccessTools.Method(typeof(CustomPaginableMenu), "GetFilteredEntries");

    // Entries per page in MiraAPI (3 columns, 5 rows).
    private const int PageSize = 15;

    private static BorPhoneMenu? _current;

    private readonly Stack<List<Entry>> _history = new();
    private List<Entry> _entries = [];
    private GameObject? _backArrow;

    /// <summary>
    ///     Gets the menu open on screen, or null.
    /// </summary>
    public static BorPhoneMenu? Current =>
        _current != null && _current.Component && Minigame.Instance == _current.Component ? _current : null;

    protected override string Name => "BorDebug";

    // Search box: the one of the MiraAPI save preset popup (the chat one could not be typed in).
    protected override TextBoxTMP PrefabTextbox => MiraAssets.PresetSavePopup.LoadAsset().GetComponentInChildren<TextBoxTMP>(true);

    /// <summary>
    ///     Opens a phone with a first list.
    /// </summary>
    /// <param name="entries">The entries, in their order.</param>
    /// <returns>The menu.</returns>
    public static BorPhoneMenu Open(IEnumerable<Entry> entries)
    {
        var menu = Create<BorPhoneMenu>();
        menu._entries = entries.ToList();
        menu.yStart -= PanelsDown;
        menu.Begin(menu.Register);
        menu.RaiseSearch();
        menu.CreateBackArrow();
        _current = menu;
        return menu;
    }

    /// <summary>
    ///     Shows another list in the same phone, the current one being kept for the back arrow.
    /// </summary>
    /// <param name="entries">The entries, in their order.</param>
    public void Show(IEnumerable<Entry> entries)
    {
        _history.Push(_entries);
        Replace(entries.ToList());
    }

    private void Back()
    {
        if (_history.Count > 0)
            Replace(_history.Pop());
    }

    private void Replace(List<Entry> entries)
    {
        foreach (var panel in EntryPanels)
        {
            if (panel)
                Object.Destroy(panel.gameObject);
        }

        // MenuEntries and EntryPanels of MiraAPI give copies of the list: replaced, not cleared.
        MenuEntries = [];
        _entries = entries;

        // A search typed for the previous list is cleared.
        SearchTextField?.SetValue(this, string.Empty);
        if (SearchBoxField?.GetValue(this) is TextBoxTMP box && box)
            box.SetText(string.Empty, string.Empty);

        Register();
        currentPage = 0;
        ShowPage();
        RefreshBackArrow();
    }

    private void Register()
    {
        RegisterPanels(_entries, OnEntryClick, SetupPanel, (panel, entry) => new MenuEntry(panel, entry.Text));

        // No shapeshift icon on hover.
        foreach (var panel in EntryPanels)
        {
            var highlight = panel.transform.Find("Nameplate/Highlight");
            if (highlight && highlight!.childCount > 0)
                highlight.GetChild(0).gameObject.SetActive(false);
        }
    }

    // Search bar, its label and its clear button, created by MiraAPI. The search box gets a caret, and the label
    // ("Search") in it as a placeholder.
    private void RaiseSearch()
    {
        var label = transform.Find(Name + "SearchLabel");
        if (SearchBoxField?.GetValue(this) is TextBoxTMP box && box)
        {
            TextBoxPatches.AddCaret(box);
            if (label && label!.GetComponent<TextMeshPro>() is { } labelText)
            {
                TextBoxPatches.AddPlaceholder(box, labelText.text, Color.black);
                label.gameObject.SetActive(false);
            }
        }

        foreach (var name in new[] { Name + "SearchBar", Name + "SearchLabel", "ClearSearchButton" })
        {
            var element = transform.Find(name);
            if (element)
                element!.localPosition += Vector3.up * (PanelsDown - SearchDown);
        }
    }

    private void OnEntryClick(Entry? entry)
    {
        if (entry == null) return;

        if (!entry.Opens)
            ForceClose();

        entry.OnClick(this);
    }

    // Copy of the close button, with an arrow, under it; shown when there is a list to go back to.
    private void CreateBackArrow()
    {
        if (!backButton) return;

        var close = backButton.gameObject;
        var arrow = Object.Instantiate(close, close.transform.parent);
        _backArrow = arrow;
        arrow.name = "BorBackArrow";
        arrow.transform.localPosition = close.transform.localPosition + (Vector3.down * BackArrowDown);

        // The rollover would put the cross back.
        foreach (var rollover in arrow.GetComponentsInChildren<ButtonRolloverHandler>(true))
        {
            Object.Destroy(rollover);
        }

        var renderer = arrow.GetComponentInChildren<SpriteRenderer>(true);
        if (renderer && renderer!.sprite)
        {
            // At the size of the close button.
            var closeSprite = renderer.sprite;
            var texture = BorAssets.BackButton.LoadAsset().texture;
            var pixelsPerUnit = closeSprite.pixelsPerUnit * texture.width / closeSprite.rect.width;
            renderer.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        var button = arrow.GetComponent<PassiveButton>();
        if (button)
        {
            button.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            button.OnClick.AddListener((Action)Back);
        }

        RefreshBackArrow();
    }

    private void RefreshBackArrow()
    {
        if (_backArrow)
            _backArrow!.SetActive(_history.Count > 0);
    }

    /// <summary>
    ///     Page arrows of MiraAPI (at the bottom of the phone, the pages loop): only shown when there is a page before
    ///     (left) or after (right). Called after each page shown (<see cref="BorPhoneMenuPagePatch"/>).
    /// </summary>
    internal void RefreshPageArrows()
    {
        var count = ((ICollection)FilteredEntriesMethod.Invoke(this, null)!).Count;
        var pages = Math.Max(1, (count + PageSize - 1) / PageSize);
        SetActive(transform.Find("LeftArrowButton"), currentPage > 0);
        SetActive(transform.Find("RightArrowButton"), currentPage < pages - 1);
    }

    private static void SetActive(Transform? arrow, bool visible)
    {
        if (arrow && arrow!.gameObject.activeSelf != visible)
            arrow.gameObject.SetActive(visible);
    }

    private static void SetupPanel(ShapeshifterPanel panel, int index, Entry entry, Action onClick)
    {
        if (entry.Player && entry.Player!.Data != null)
        {
            panel.SetPlayer(index, entry.Player.Data, onClick);

            // No level badge.
            panel.LevelNumberText.transform.parent.gameObject.SetActive(false);
            return;
        }

        // Like a role panel of TOU, without the player nor an icon.
        panel.shapeshift = onClick;
        panel.PlayerIcon.gameObject.SetActive(false);
        foreach (var renderer in panel.GetComponentsInChildren<SpriteRenderer>())
        {
            renderer.material.SetInt(PlayerMaterial.MaskLayer, index + 2);
        }

        panel.LevelNumberText.transform.parent.gameObject.SetActive(false);

        // Centered on the panel: the name box is on the right of the player icon.
        var text = panel.NameText;
        text.color = entry.Color;
        text.text = entry.Text;
        text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.pivot = new Vector2(0.5f, text.rectTransform.pivot.y);
        var position = text.transform.position;
        text.transform.position = new Vector3(panel.Background.bounds.center.x, position.y, position.z);
    }

    /// <summary>
    ///     An entry of the menu.
    /// </summary>
    /// <param name="Text">The text shown (the player name for a player).</param>
    /// <param name="Color">The text color.</param>
    /// <param name="OnClick">The action; after the menu is closed, unless it opens another list.</param>
    /// <param name="Opens">Whether the action shows another list in the menu (<see cref="Show"/>).</param>
    /// <param name="Player">The player shown, or null for a text alone.</param>
    public sealed record Entry(string Text, Color Color, Action<BorPhoneMenu> OnClick, bool Opens = false, PlayerControl? Player = null);
}

/// <summary>
///     After each page shown by MiraAPI (page change, new list, search), the page arrows of the Debug menu are refreshed.
/// </summary>
[HarmonyPatch(typeof(CustomPaginableMenu), nameof(CustomPaginableMenu.ShowPage))]
public static class BorPhoneMenuPagePatch
{
    [HarmonyPostfix]
    public static void ShowPagePostfix(CustomPaginableMenu __instance)
    {
        if (__instance is BorPhoneMenu menu)
            menu.RefreshPageArrows();
    }
}
