using BetterOtherRoles.Modules.Debug;
using BetterOtherRoles.Options;
using BetterOtherRoles.Patches.Misc;
using BetterOtherRoles.Patches.Roles;
using BetterOtherRoles.Utilities;
using HarmonyLib;
using Hazel;
using MiraAPI.GameOptions;
using MiraAPI.Translation;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using PoolList = Il2CppSystem.Collections.Generic.List<PoolableBehavior>;

namespace BetterOtherRoles.Modules.Chat;

/// <summary>
///     Impostor chat (TOU-Mira 1.7.9 team chat, impostors only), option <see cref="GameplayOptions.ImpostorChat"/>: never,
///     in meetings, or during the whole game.
///     <list type="bullet">
///         <item>
///             a red chat button, left of the chat button of the game, opens the impostor chat (closes it when shown),
///             the button of the game the public chat; Tab switches while the chat is open; each chat has its own list
///             of messages, the impostor one with a tinted background and a text; every meeting starts on the public chat;
///         </item>
///         <item>
///             out of meetings (whole game), a living impostor has the chat for the impostor chat only (the game gives no
///             chat to the living there): the button of the game is hidden and nothing can be sent to the public chat;
///         </item>
///         <item>
///             the living impostors write and read, the dead ones only read; a message is only sent to the impostors
///             (<see cref="ImpostorChatRpc"/>, one RPC per impostor), so a crewmate never receives it; a dot on the red
///             button tells an impostor message was not read.
///         </item>
///     </list>
///     The two lists share the bubble pool of the game: the shown list is the scroller content and the active bubbles of
///     the pool, swapped when switching. With <see cref="GameplayOptions.ImpostorChatSeparate"/> off, there is no impostor
///     chat of its own (no red button): out of meetings, a living impostor writes in the chat of the game, sent to the
///     impostors only, their messages in the public list with a red bubble; in meetings, everyone has the public chat.
/// </summary>
[HarmonyPatch]
public static class BorImpostorChat
{
    // Chat background while the impostor chat is shown, its bubbles, its text, and the red button.
    private static readonly Color BackgroundColor = new(0.35f, 0.12f, 0.12f, 0.85f);
    private static readonly Color BubbleColor = new(1f, 0.82f, 0.82f, 1f);
    private static readonly Color TextColor = new(1f, 0.45f, 0.45f, 1f);
    private static readonly Color ButtonColor = new(1f, 0.35f, 0.35f, 1f);
    private static readonly Color ReadOnlyTint = new(0.6f, 0.6f, 0.6f, 1f);

    // The chat box of a dead impostor is greyed (RefreshInput), with the colors to give back.
    private static readonly Dictionary<IntPtr, (SpriteRenderer Renderer, Color Color)> GreyedColors = [];
    private static bool _inputReadOnly;

    // Rate limit of the game between two messages.
    private const float SendRate = 3f;

    // Space between the red button and the game button, beyond the width of the icon.
    private const float ButtonGap = 0f;

    private static Transform? _publicItems;
    private static Transform? _impostorItems;
    private static PoolList? _publicBubbles;
    private static PoolList? _impostorBubbles;
    private static PassiveButton? _button;
    private static SpriteRenderer? _unreadDot;
    private static TextMeshPro? _label;
    private static Color _backgroundColor = Color.white;

    // A bubble added to the impostor list (not moved to the public one).
    private static bool _addingImpostor;

    // The chat was made visible for a living impostor out of meetings.
    private static bool _shownForImpostor;

    // Children of the chat button of the game: its panel, its icons (one per state) and its unread dot.
    private const string PanelName = "Background";
    private const string DotName = "chatIconNotification";
    private static readonly string[] IconNames = ["Inactive", "Active", "Selected"];

    // Width of the game icon when it is missing.
    private const float DefaultIconWidth = 0.56f;

    // One button (red button to game button, in the space of the game button), the frame under the red button, the
    // game button in red.
    private static float _pitch;
    private static bool _chatButtonMoved;
    private static bool _panelGrown;
    private static bool _gameButtonRed;

    /// <summary>
    ///     Gets whether the impostor chat is shown.
    /// </summary>
    public static bool IsShown { get; private set; }

    private static ImpostorChatMode Mode => (ImpostorChatMode)OptionGroupSingleton<GameplayOptions>.Instance.ImpostorChat.Value;

    // Each chat has its own list; off: the impostor messages are in the public list. The option is only shown (and only
    // counts) for the whole game mode: in meetings only, the impostor chat is always a chat of its own.
    private static bool Separate =>
        Mode != ImpostorChatMode.WholeGame || OptionGroupSingleton<GameplayOptions>.Instance.ImpostorChatSeparate.Value;

    /// <summary>
    ///     Gets whether the local player can show the impostor chat: an impostor (dead or not), in a meeting, or in game
    ///     with the whole game mode.
    /// </summary>
    public static bool CanShow
    {
        get
        {
            if (Mode == ImpostorChatMode.Off || !IsImpostor(PlayerControl.LocalPlayer)) return false;
            if (MeetingHud.Instance) return true;

            return Mode == ImpostorChatMode.WholeGame && IsInGame;
        }
    }

    /// <summary>
    ///     Gets whether the local player can write in the impostor chat (a living impostor).
    /// </summary>
    public static bool CanWrite => CanShow && !PlayerControl.LocalPlayer.Data.IsDead;

    // The public chat, as the game gives it: in meetings, or dead; and to the host in game with the Debug Mode
    // (TestModePatches).
    private static bool CanShowPublic =>
        MeetingHud.Instance || (PlayerControl.LocalPlayer && PlayerControl.LocalPlayer.Data.IsDead) ||
        (BorDebugActions.CanUse && IntroStatePatches.IntroEnded);

    // Only the impostor chat: a living impostor out of meetings.
    private static bool IsImpostorOnly => CanShow && !CanShowPublic;

    // The red button, next to the game one: both chats can be shown, each with its own list.
    private static bool HasButton => Separate && CanShow && !IsImpostorOnly;

    // In game, out of the intro and the ejection.
    private static bool IsInGame =>
        AmongUsClient.Instance && AmongUsClient.Instance.IsGameStarted && ShipStatus.Instance && IntroStatePatches.IntroEnded &&
        !ExileController.Instance;

    /// <summary>
    ///     Sends the text of the chat box to the impostors when the impostor chat is shown. Called by
    ///     <see cref="Commands.BorCommandManager"/> for a message that is not a command.
    /// </summary>
    /// <param name="chat">The chat.</param>
    /// <param name="text">The text, trimmed.</param>
    /// <returns>True if the message was taken (sent, or refused with a message).</returns>
    public static bool TrySend(ChatController chat, string text)
    {
        if (!IsShown || text.Length == 0) return false;

        if (!CanWrite)
        {
            BorChat.AddSystemMessage(MiraLocaleManager.Get("BetterOtherRoles.ImpostorChat.CannotWrite", "Dead impostors can only read this chat."));
            return true;
        }

        // The game shows the rate limit and sends nothing.
        if (chat.timeSinceLastMessage < SendRate) return false;

        chat.timeSinceLastMessage = 0f;
        var sender = PlayerControl.LocalPlayer;
        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (player && !player.AmOwner && IsImpostor(player))
                Rpc<ImpostorChatRpc>.Instance.SendTo(sender, player.OwnerId, text);
        }

        AddMessage(sender, text);
        chat.freeChatField.Clear();
        chat.quickChatMenu.Clear();
        chat.quickChatField.Clear();
        chat.UpdateChatMode();
        return true;
    }

    /// <summary>
    ///     Shows the public chat or the impostor chat (only the impostor chat for a living impostor out of meetings).
    /// </summary>
    /// <param name="impostor">True for the impostor chat.</param>
    public static void Show(bool impostor)
    {
        var chat = GetChat();
        if (!chat || !EnsureLists(chat!)) return;

        if (impostor && !CanShow) impostor = false;
        if (!impostor && IsImpostorOnly) impostor = true;

        // One list: the impostor chat is only the chat of a living impostor out of meetings.
        if (!Separate) impostor = IsImpostorOnly;

        if (impostor != IsShown)
        {
            IsShown = impostor;
            if (Separate)
            {
                SwapLists(chat!, impostor);
                _publicItems!.gameObject.SetActive(!impostor);
                _impostorItems!.gameObject.SetActive(impostor);
                chat!.AlignAllBubbles();
            }
        }

        if (impostor && _unreadDot)
            _unreadDot!.enabled = false;

        RefreshLook(chat!);
    }

    // Red button: opens the impostor chat, shows it, or closes the chat when it is shown.
    private static void OnImpostorButton()
    {
        var chat = GetChat();
        if (!chat) return;

        if (!chat!.IsOpenOrOpening)
        {
            Show(true);
            chat.Toggle();
        }
        else if (!IsShown)
        {
            Show(true);
        }
        else
        {
            chat.Toggle();
        }
    }

    // Button of the game: the same for the public chat (for the impostor chat when it is the only one, the button red).
    private static void OnPublicButton()
    {
        var chat = GetChat();
        if (!chat) return;

        // Escape: the game clicks this button (back button of the open chat) to close the chat, whichever is shown.
        if (chat!.IsOpenOrOpening && Input.GetKeyDown(KeyCode.Escape))
        {
            chat.Toggle();
            return;
        }

        if (IsImpostorOnly)
        {
            OnImpostorButton();
            return;
        }

        if (chat!.IsOpenOrOpening && IsShown && CanShowPublic)
        {
            Show(false);
            return;
        }

        if (!chat.IsOpenOrOpening)
            Show(false);

        chat.Toggle();
    }

    private static void AddMessage(PlayerControl sender, string text)
    {
        var chat = GetChat();
        if (!chat || !EnsureLists(chat!)) return;

        // The game lights its unread dot for any message: kept for its button only when it shows the message (one list,
        // or the impostor chat only).
        var separate = Separate;
        var gameDot = chat!.chatNotifyDot;
        var gameDotWasOn = gameDot && gameDot.enabled;

        _addingImpostor = true;
        try
        {
            if (separate)
                SwapLists(chat, true);

            chat.AddChat(sender, text, true);
            if (separate && gameDot && !gameDotWasOn && !IsImpostorOnly)
                gameDot.enabled = false;

            var bubbles = chat.chatBubblePool.activeChildren;
            var bubble = bubbles.Count > 0 ? bubbles[bubbles.Count - 1].TryCast<ChatBubble>() : null;
            if (bubble)
                bubble!.Background.color = BubbleColor;
        }
        finally
        {
            if (separate)
            {
                SwapLists(chat!, IsShown);
                chat!.AlignAllBubbles();
            }

            _addingImpostor = false;
        }

        // One list: the unread dot of the game tells it.
        if (separate && (!IsShown || !chat.IsOpenOrOpening) && _unreadDot)
            _unreadDot!.enabled = true;
    }

    // A dead player has a ghost role, not always of the impostor team for the roles of the mod: its role while alive
    // (AliveRolePatches) tells it.
    private static bool IsImpostor(PlayerControl? player)
    {
        if (!player || !player!.Data || !player.Data.Role) return false;
        if (player.Data.Role.IsImpostor) return true;
        if (!player.Data.IsDead) return false;

        var aliveRole = AliveRolePatches.Get(player);
        if (aliveRole == null || !RoleManager.InstanceExists) return false;

        var role = RoleManager.Instance.GetRole(aliveRole.Value);
        return role && role.IsImpostor;
    }

    private static ChatController? GetChat() => HudManager.InstanceExists ? HudManager.Instance.Chat : null;

    // The impostor list: an empty copy of the scroller content, next to it, and its own active bubbles.
    private static bool EnsureLists(ChatController chat)
    {
        if (_impostorItems && _publicItems) return true;

        var inner = chat.scroller.Inner;
        if (!inner) return false;

        _publicItems = inner;
        _publicBubbles = chat.chatBubblePool.activeChildren;
        _impostorBubbles = new PoolList();

        var items = new GameObject("BorImpostorChatItems") { layer = inner.gameObject.layer };
        items.transform.SetParent(inner.parent, false);
        items.transform.localPosition = inner.localPosition;
        items.transform.localRotation = inner.localRotation;
        items.transform.localScale = inner.localScale;
        items.SetActive(false);
        _impostorItems = items.transform;
        return true;
    }

    private static void SwapLists(ChatController chat, bool impostor)
    {
        chat.scroller.Inner = impostor ? _impostorItems : _publicItems;
        chat.chatBubblePool.activeChildren = impostor ? _impostorBubbles : _publicBubbles;
    }

    // Buttons, background and text of the impostor chat.
    private static void RefreshLook(ChatController chat)
    {
        CreateButton(chat);
        RefreshButtons(chat);
        var open = chat.IsOpenOrOpening;
        if (_button)
            _button!.SelectButton(open && IsShown);

        if (open)
            chat.chatButton.SelectButton(IsImpostorOnly || !IsShown);

        var background = chat.backgroundImage;
        if (background)
            background.color = IsShown ? BackgroundColor : _backgroundColor;

        if (!_label)
        {
            _label = Object.Instantiate(chat.sendRateMessageText, chat.sendRateMessageText.transform.parent);
            _label.name = "BorImpostorChatLabel";
        }

        _label!.gameObject.SetActive(IsShown);
        if (!IsShown) return;

        _label.color = TextColor;
        if (IsImpostorOnly)
            _label.text = MiraLocaleManager.Get("BetterOtherRoles.ImpostorChat.Only", "Impostor chat");
        else if (CanWrite)
            _label.text = MiraLocaleManager.Get("BetterOtherRoles.ImpostorChat.Active", "Impostor chat (Tab: public chat)");
        else
            _label.text = MiraLocaleManager.Get("BetterOtherRoles.ImpostorChat.ReadOnly", "Impostor chat, read only (Tab: public chat)");
    }

    // Copy of the chat button of the game, in red, on its left; the click of the game button goes through
    // OnPublicButton.
    private static void CreateButton(ChatController chat)
    {
        if (_button) return;

        var gameButton = chat.chatButton;
        if (!gameButton) return;

        gameButton.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        gameButton.OnClick.AddListener((Action)OnPublicButton);

        _button = Object.Instantiate(gameButton, gameButton.transform.parent);
        _button.name = "BorImpostorChatButton";
        _button.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        _button.OnClick.AddListener((Action)OnImpostorButton);
        _button.gameObject.SetActive(false);

        // Placed by the mod next to the game button (PlaceButton), not on the screen edge; in the panel of the game,
        // which grows for it (GrowPanel): its own panel is not shown.
        var aspect = _button.GetComponent<AspectPosition>();
        if (aspect)
            aspect.enabled = false;

        _button.transform.Find(PanelName)?.gameObject.SetActive(false);
        foreach (var name in IconNames)
        {
            var icon = _button.transform.Find(name)?.GetComponent<SpriteRenderer>();
            if (icon)
                icon!.color *= ButtonColor;
        }

        _unreadDot = _button.transform.Find(DotName)?.GetComponent<SpriteRenderer>();
        if (_unreadDot)
            _unreadDot!.enabled = false;

        // One button to the left: the width of the icon and a gap, in the space of the game button.
        var gameIcon = gameButton.transform.Find(IconNames[0])?.GetComponent<SpriteRenderer>();
        var width = gameIcon ? IconMax(gameIcon!) - IconMin(gameIcon!) : DefaultIconWidth;
        _pitch = (width + ButtonGap) / Mathf.Max(gameButton.transform.lossyScale.x, 0.0001f);
    }

    // The red button one button left of the game button, at its scale, wherever the game puts it (lobby, game,
    // meeting).
    // Child of the game button: it follows its place and scale. Its icons and dot take the place the game gives to its
    // own (moved in meetings).
    private static void PlaceButton(ChatController chat)
    {
        var gameButton = chat.chatButton.transform;
        var button = _button!.transform;
        if (button.parent != gameButton)
            button.SetParent(gameButton, false);

        button.localPosition = new Vector3(-_pitch, 0f, 0f);
        button.localRotation = Quaternion.identity;
        button.localScale = Vector3.one;

        // Its panel (hidden) too: the frame of the button group is placed from it (the game moves the one of the game
        // button between the lobby and the game).
        foreach (var name in IconNames.Append(DotName).Append(PanelName))
        {
            var gameChild = gameButton.Find(name);
            var child = button.Find(name);
            if (!gameChild || !child) continue;

            child!.localPosition = gameChild!.localPosition;
            child.localScale = gameChild.localScale;
        }
    }

    // The frame of the button group (ButtonGroupPatches) under the red button too, with the margins it has for the
    // other buttons: one frame for the chat buttons and the settings button. Given back when the red button is hidden.
    private static void GrowPanel(ChatController chat, bool grow)
    {
        if (!grow)
        {
            if (!_panelGrown) return;

            _panelGrown = false;
            ButtonGroupPatches.SetExtraPanels([]);
            return;
        }

        var panel = chat.chatButton ? chat.chatButton.transform.Find(PanelName) : null;
        var redPanel = _button ? _button!.transform.Find(PanelName) : null;
        if (!panel || !redPanel) return;

        _panelGrown = true;
        ButtonGroupPatches.SetExtraPanels([panel!, redPanel!]);
    }

    // Span of an icon from its sprite and transform: an inactive icon (the Inactive one while the button is hovered or
    // selected) has empty bounds.
    private static float IconMin(SpriteRenderer icon) => Mathf.Min(IconEdge(icon, true), IconEdge(icon, false));

    private static float IconMax(SpriteRenderer icon) => Mathf.Max(IconEdge(icon, true), IconEdge(icon, false));

    private static float IconCenter(SpriteRenderer icon) => (IconMin(icon) + IconMax(icon)) / 2f;

    private static float IconEdge(SpriteRenderer icon, bool min)
    {
        if (!icon.sprite) return icon.transform.position.x;

        var bounds = icon.sprite.bounds;
        return icon.transform.TransformPoint(min ? bounds.min : bounds.max).x;
    }

    // The red button next to the game one, in its panel, when both chats can be shown; the game button in red when
    // only the impostor chat can be.
    private static void RefreshButtons(ChatController chat)
    {
        var both = HasButton && _button;
        if (_button && _button!.gameObject.activeSelf != both)
            _button.gameObject.SetActive(both);

        if (both)
        {
            MoveChatButton(chat);
            PlaceButton(chat);
        }
        else if (_chatButtonMoved)
        {
            // Back where the game puts it.
            _chatButtonMoved = false;
            chat.chatButtonAspectPosition.AdjustPosition();
        }

        GrowPanel(chat, both);
        TintGameButton(chat, IsImpostorOnly && Separate);
    }

    // The chat button of the game one button left of the settings button (as the red one is left of it): the three
    // icons evenly spaced. Measured on the icons every frame, so a move of the game is followed.
    private static void MoveChatButton(ChatController chat)
    {
        var settings = HudManager.InstanceExists ? HudManager.Instance.SettingsButton : null;
        var settingsIcon = settings ? settings!.transform.Find(IconNames[0])?.GetComponent<SpriteRenderer>() : null;
        var chatIcon = chat.chatButton.transform.Find(IconNames[0])?.GetComponent<SpriteRenderer>();
        if (!settingsIcon || !chatIcon) return;

        var pitch = _pitch * chat.chatButton.transform.lossyScale.x;
        var shift = IconCenter(settingsIcon!) - pitch - IconCenter(chatIcon!);
        if (Mathf.Abs(shift) < 0.001f) return;

        chat.chatButton.transform.position += new Vector3(shift, 0f, 0f);
        _chatButtonMoved = true;
    }

    private static void TintGameButton(ChatController chat, bool red)
    {
        if (red == _gameButtonRed || !chat.chatButton) return;

        _gameButtonRed = red;
        foreach (var name in IconNames)
        {
            var icon = chat.chatButton.transform.Find(name)?.GetComponent<SpriteRenderer>();
            if (!icon) continue;

            var color = icon!.color;
            icon.color = red
                ? color * ButtonColor
                : new Color(color.r / ButtonColor.r, color.g / ButtonColor.g, color.b / ButtonColor.b, color.a);
        }
    }

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.Awake))]
    [HarmonyPostfix]
    public static void ChatAwakePostfix(ChatController __instance)
    {
        _publicItems = null;
        _impostorItems = null;
        _publicBubbles = null;
        _impostorBubbles = null;
        _button = null;
        _unreadDot = null;
        _label = null;
        IsShown = false;
        _shownForImpostor = false;
        _gameButtonRed = false;
        _panelGrown = false;
        ButtonGroupPatches.SetExtraPanels([]);
        _chatButtonMoved = false;
        _inputReadOnly = false;
        GreyedColors.Clear();
        if (__instance.backgroundImage)
            _backgroundColor = __instance.backgroundImage.color;
    }

    // The chat of a living impostor out of meetings (whole game): shown by the mod, with the impostor chat only.
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    [HarmonyPostfix]
    public static void HudUpdatePostfix(HudManager __instance)
    {
        var chat = __instance.Chat;
        if (!chat) return;

        var impostorOnly = IsImpostorOnly && !Minigame.Instance;
        if (impostorOnly && !chat.gameObject.activeSelf)
        {
            chat.SetVisible(true);
            _shownForImpostor = true;
        }
        else if (!impostorOnly && _shownForImpostor && !CanShowPublic)
        {
            _shownForImpostor = false;
            chat.SetVisible(false);
        }
        else if (CanShowPublic)
        {
            _shownForImpostor = false;
        }

        // Hidden chat (end of a meeting for a living player): the panel of the settings button, made transparent while
        // the panel of the chat button was grown under it, is shown again.
        if (chat.gameObject.activeSelf)
            RefreshButtons(chat);
        else
            GrowPanel(chat, false);
    }

    // A dead impostor reads the impostor chat only: its chat box is greyed and cannot be written in.
    private static void RefreshInput(ChatController chat)
    {
        var field = chat.freeChatField;
        var box = field ? field.textArea : null;
        if (!field || box == null) return;

        var readOnly = IsShown && !CanWrite;

        // The game enables the box again when it shows it (UpdateChatMode).
        if (readOnly && box.enabled)
        {
            box.LoseFocus();
            box.Clear();
            box.enabled = false;
        }

        if (readOnly == _inputReadOnly) return;

        // Its button would give the box its colors back on hover, and the focus on click.
        _inputReadOnly = readOnly;
        if (field.fieldButton)
            field.fieldButton.enabled = !readOnly;

        if (readOnly)
        {
            foreach (var renderer in field.GetComponentsInChildren<SpriteRenderer>(true))
            {
                GreyedColors[renderer.Pointer] = (renderer, renderer.color);
                renderer.color *= ReadOnlyTint;
            }

            return;
        }

        foreach (var (renderer, color) in GreyedColors.Values)
        {
            if (renderer)
                renderer.color = color;
        }

        GreyedColors.Clear();
        box.enabled = field.Visible;
    }

    // Tab switches while the chat is open; the shown chat follows what can be shown (meeting started or ended, death).
    [HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
    [HarmonyPostfix]
    public static void ChatUpdatePostfix(ChatController __instance)
    {
        RefreshInput(__instance);
        if ((IsShown && !CanShow) || (!IsShown && IsImpostorOnly))
        {
            Show(IsImpostorOnly);
            return;
        }

        if (__instance.IsOpenOrOpening && HasButton && Input.GetKeyDown(KeyCode.Tab))
        {
            Show(!IsShown);
            return;
        }

        if (!_button || _button!.gameObject.activeSelf != HasButton)
            RefreshLook(__instance);
    }

    // The chat window points at the chat button (placed by the game in LateUpdate): at the red one while the impostor
    // chat is shown next to it.
    [HarmonyPatch(typeof(ChatController), nameof(ChatController.LateUpdate))]
    [HarmonyPostfix]
    public static void LateUpdatePostfix(ChatController __instance)
    {
        if (!IsShown || !_button || !_button!.gameObject.activeInHierarchy || !__instance.chatScreen) return;

        var shift = _button.transform.position.x - __instance.chatButton.transform.position.x;
        __instance.chatScreen.transform.position += new Vector3(shift, 0f, 0f);
    }

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.Toggle))]
    [HarmonyPostfix]
    public static void TogglePostfix(ChatController __instance)
    {
        if (CanShow || IsShown)
            RefreshLook(__instance);
    }

    // Every meeting starts on the public chat.
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    [HarmonyPostfix]
    public static void MeetingStartPostfix()
    {
        Show(false);
    }

    // A player message (or vote note) goes to the public list, even while the impostor chat is shown.
    [HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
    [HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChatNote))]
    [HarmonyPrefix]
    public static void AddPublicPrefix(ChatController __instance, out bool __state)
    {
        __state = IsShown && !_addingImpostor && _publicItems && Separate;
        if (__state)
            SwapLists(__instance, false);
    }

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
    [HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChatNote))]
    [HarmonyFinalizer]
    public static void AddPublicFinalizer(ChatController __instance, bool __state)
    {
        if (!__state) return;

        SwapLists(__instance, true);
        __instance.AlignAllBubbles();
    }

    /// <summary>
    ///     Clears the impostor messages of the last game (called when a game starts, with the public chat).
    /// </summary>
    public static void Clear()
    {
        var chat = GetChat();
        Show(false);
        if (_unreadDot)
            _unreadDot!.enabled = false;

        if (!chat || _impostorBubbles == null || !_impostorItems) return;

        SwapLists(chat!, true);
        chat!.chatBubblePool.ReclaimAll();
        SwapLists(chat, IsShown);
    }

    /// <summary>
    ///     Impostor chat message, sent to one impostor (SendTo): its sender is the player object of the RPC.
    /// </summary>
    [RegisterCustomRpc((uint)BorRpc.ImpostorChat)]
    public sealed class ImpostorChatRpc(BetterOtherRolesPlugin plugin, uint id) : PlayerCustomRpc<BetterOtherRolesPlugin, string>(plugin, id)
    {
        public override RpcLocalHandling LocalHandling => RpcLocalHandling.None;

        public override void Write(MessageWriter writer, string? data) => writer.Write(data ?? string.Empty);

        public override string Read(MessageReader reader) => reader.ReadString();

        // Shown only by an impostor, from an impostor, when the impostor chat can be shown.
        public override void Handle(PlayerControl innerNetObject, string? data)
        {
            if (string.IsNullOrEmpty(data) || !CanShow || !IsImpostor(innerNetObject)) return;

            AddMessage(innerNetObject, data!);
        }
    }
}
