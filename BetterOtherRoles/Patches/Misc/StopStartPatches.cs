using BetterOtherRoles.Modules.Admin;
using BetterOtherRoles.Options;
using HarmonyLib;
using MiraAPI.GameOptions;
using MiraAPI.Translation;
using Reactor.Networking.Attributes;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Stopping the game start countdown, as BetterOtherRoles 1.7.1 (GameStartManagerPatch, RPC StopStart):
///     the host can always stop it with its start button ("Stop"); the other players get a stop button when
///     <see cref="LobbyOptions.AnyPlayerCanStopStart"/> is on, the admins always.
/// </summary>
[HarmonyPatch]
public static class StopStartPatches
{
    // Countdown seen by a client (the start state only changes on the host): seconds left, -1 when not counting.
    private static int _clientSeconds = -1;
    private static GameObject? _clientStopButton;

    // Host start button: its text before the countdown (put back after), and the button whose hover is followed.
    private static string? _hostStartText;
    private static PassiveButton? _hookedHostButton;
    private static bool _hostHovered;
    private static bool _clientHovered;
    private static readonly List<SpriteRenderer> _hiddenPanelSprites = [];

    private static bool CanStop =>
        AmongUsClient.Instance.AmHost || BorAdmin.IsAdmin || OptionGroupSingleton<LobbyOptions>.Instance.AnyPlayerCanStopStart.Value;

    private static string StopText => MiraLocaleManager.Get("BetterOtherRoles.Lobby.StopStart", "Stop");

    /// <summary>
    ///     Asks the host to stop the countdown. The admin flag comes from the sender, as the host cannot check its key.
    /// </summary>
    [MethodRpc((uint)BorRpc.StopStart)]
    public static void RpcStopStart(PlayerControl player, bool isAdmin)
    {
        if (!AmongUsClient.Instance.AmHost || !GameStartManager.InstanceExists ||
            GameStartManager.Instance.startState != GameStartManager.StartingStates.Countdown)
        {
            return;
        }

        if (!isAdmin && !OptionGroupSingleton<LobbyOptions>.Instance.AnyPlayerCanStopStart.Value)
        {
            return;
        }

        GameStartManager.Instance.ResetStartState();
        var message = string.Format(BetterOtherRolesPlugin.Culture,
            MiraLocaleManager.Get("BetterOtherRoles.Lobby.StartStopped", "{0} stopped the game start!"), player.Data.PlayerName);
        PlayerControl.LocalPlayer.RpcSendChat(message);
    }

    /// <summary>
    ///     The host's start button stops the countdown instead of starting the game again.
    /// </summary>
    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.BeginGame))]
    [HarmonyPrefix]
    public static bool BeginGamePrefix(GameStartManager __instance)
    {
        if (!AmongUsClient.Instance.AmHost || __instance.startState != GameStartManager.StartingStates.Countdown)
        {
            return true;
        }

        __instance.ResetStartState();
        return false;
    }

    /// <summary>
    ///     A new lobby (back from a game): the countdown ended with the game start, without a -1 counter.
    /// </summary>
    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Start))]
    [HarmonyPostfix]
    public static void StartPostfix()
    {
        _clientSeconds = -1;
        _clientStopButton = null;
        _clientHovered = false;
        _hostStartText = null;
        _hookedHostButton = null;
        _hostHovered = false;
        _hiddenPanelSprites.Clear();
    }

    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.SetStartCounter))]
    [HarmonyPostfix]
    public static void SetStartCounterPostfix(sbyte __0)
    {
        _clientSeconds = __0;
    }

    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Update))]
    [HarmonyPostfix]
    public static void UpdatePostfix(GameStartManager __instance)
    {
        if (AmongUsClient.Instance.AmHost)
        {
            UpdateHostButton(__instance);
        }
        else
        {
            UpdateClientButton(__instance);
        }
    }

    // During the countdown: the vanilla countdown text (drawn over the button), "Stop" under the mouse instead.
    // Its own text again afterwards.
    private static void UpdateHostButton(GameStartManager manager)
    {
        var label = GetLabel(manager.StartButton.gameObject);
        if (manager.startState != GameStartManager.StartingStates.Countdown)
        {
            if (_hostStartText != null)
            {
                if (label)
                {
                    label!.text = _hostStartText;
                }

                _hostStartText = null;
                ShowCountdown(manager, true);
                ShowCountdownPanel(manager, true);
            }

            return;
        }

        if (_hookedHostButton != manager.StartButton)
        {
            _hookedHostButton = manager.StartButton;
            _hostHovered = false;
            FollowHover(manager.StartButton, hovered => _hostHovered = hovered);
        }

        if (label)
        {
            _hostStartText ??= label!.text;
            SetText(label!, _hostHovered ? StopText : string.Empty);
        }

        // Not hovered: the vanilla grey countdown. Hovered: the start button look, with "Stop".
        ShowCountdown(manager, !_hostHovered);
        ShowCountdownPanel(manager, !_hostHovered);
        ShowAsEnabled(manager.StartButton, _hostHovered);
        manager.StartButton.gameObject.SetActive(true);
    }

    // The countdown panel has a grey background drawn over the start button (and the client copy): hidden while "Stop" is shown.
    private static void ShowCountdownPanel(GameStartManager manager, bool visible)
    {
        if (!manager.GameStartTextParent)
        {
            return;
        }

        if (!visible && _hiddenPanelSprites.Count == 0)
        {
            foreach (var sprite in manager.GameStartTextParent.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sprite.enabled && !sprite.transform.IsChildOf(manager.StartButton.transform) &&
                    !(_clientStopButton && sprite.transform.IsChildOf(_clientStopButton!.transform)))
                {
                    sprite.enabled = false;
                    _hiddenPanelSprites.Add(sprite);
                }
            }
        }
        else if (visible)
        {
            foreach (var sprite in _hiddenPanelSprites.Where(x => x))
            {
                sprite.enabled = true;
            }

            _hiddenPanelSprites.Clear();
        }
    }

    private static void ShowCountdown(GameStartManager manager, bool visible)
    {
        if (manager.GameStartText && manager.GameStartText.enabled != visible)
        {
            manager.GameStartText.enabled = visible;
        }

        if (manager.GameStartTextClient && manager.GameStartTextClient.enabled != visible)
        {
            manager.GameStartTextClient.enabled = visible;
        }
    }

    private static void UpdateClientButton(GameStartManager manager)
    {
        if (_clientSeconds < 0 || !CanStop)
        {
            if (_clientStopButton)
            {
                Object.Destroy(_clientStopButton);
                ShowCountdown(manager, true);
                ShowCountdownPanel(manager, true);
            }

            _clientStopButton = null;
            return;
        }

        if (!_clientStopButton)
        {
            CreateClientButton(manager);
        }

        var label = GetLabel(_clientStopButton!);
        if (label)
        {
            SetText(label!, _clientHovered ? StopText : _clientSeconds.ToString(BetterOtherRolesPlugin.Culture));
        }

        ShowCountdown(manager, !_clientHovered);
        ShowCountdownPanel(manager, !_clientHovered);
        ShowAsEnabled(_clientStopButton!.GetComponent<PassiveButton>(), _clientHovered);
    }

    // A copy of the host start button (inactive on the clients), at its place.
    private static void CreateClientButton(GameStartManager manager)
    {
        _clientStopButton = Object.Instantiate(manager.StartButton.gameObject, manager.StartButton.transform.parent);
        _clientStopButton.name = "BorStopStartButton";
        _clientStopButton.transform.localPosition = manager.StartButton.transform.localPosition;
        _clientStopButton.SetActive(true);
        foreach (var translator in _clientStopButton.GetComponentsInChildren<TextTranslatorTMP>(true))
        {
            Object.Destroy(translator);
        }

        var button = _clientStopButton.GetComponent<PassiveButton>();
        _clientHovered = false;
        FollowHover(button, hovered => _clientHovered = hovered);
        button.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        button.OnClick.AddListener((Action)(() =>
        {
            RpcStopStart(PlayerControl.LocalPlayer, BorAdmin.IsAdmin);
            _clientSeconds = -1;
        }));
    }

    // Added to the button's own mouse events (the vanilla ones are kept).
    private static void FollowHover(PassiveButton button, Action<bool> setHovered)
    {
        button.OnMouseOver ??= new UnityEngine.Events.UnityEvent();
        button.OnMouseOut ??= new UnityEngine.Events.UnityEvent();
        button.OnMouseOver.AddListener((Action)(() => setHovered(true)));
        button.OnMouseOut.AddListener((Action)(() => setHovered(false)));
    }

    // The game shows the start button disabled (grey) during the countdown: like the "Start" button instead.
    private static void ShowAsEnabled(PassiveButton button, bool hovered)
    {
        button.SetButtonEnableState(true);
        if (hovered)
        {
            button.SetPassiveButtonHoverStateActive();
        }
        else
        {
            button.SetPassiveButtonHoverStateInactive();
        }
    }

    private static TextMeshPro? GetLabel(GameObject button)
    {
        return button.GetComponentsInChildren<TextMeshPro>(true).FirstOrDefault();
    }

    private static void SetText(TextMeshPro label, string text)
    {
        if (label.text != text)
        {
            label.text = text;
        }
    }
}
