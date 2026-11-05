using AmongUs.Data;
using BetterOtherRoles.LocalSettings;
using InnerNet;
using MiraAPI.LocalSettings;
using MiraAPI.Translation;
using Reactor.Utilities.Attributes;
using UnityEngine;

namespace BetterOtherRoles.Modules.AutoRejoin;

/// <summary>
///     Added to the end game screen when <see cref="BorLocalSettingsTab.AutoRejoin"/> is on (TOU-Mira 1.7.9 AutoRejoin,
///     from Maxi0fc): once the "Play Again" button shows, a countdown, then "Play Again". A local setting of each player.
///     It is destroyed with the end game screen, so leaving the game cancels it.
/// </summary>
[RegisterInIl2Cpp]
public sealed class AutoRejoinBehaviour(nint cppPtr) : MonoBehaviour(cppPtr)
{
    private float _timer;
    private bool _counting;
    private string _gameCode = string.Empty;
    private EndGameNavigation? _navigation;
    private GUIStyle? _style;
    private GUIStyle? _shadow;

    private void Start()
    {
        _navigation = GetComponent<EndGameManager>().Navigation;
        if (!_navigation)
        {
            Warning("Auto rejoin: end game navigation not found.");
            enabled = false;
            return;
        }

        // The game code stays hidden in streamer mode (and there is none in local games).
        _gameCode = DataManager.Settings.Gameplay.StreamerMode
            ? string.Empty
            : GameCode.IntToGameName(AmongUsClient.Instance.GameId) ?? string.Empty;
    }

    private void Update()
    {
        // The countdown starts with the "Quit" and "Play Again" buttons, not at the end of the game.
        if (!_counting)
        {
            if (!_navigation!.PlayAgainButton || !_navigation.PlayAgainButton.gameObject.activeInHierarchy) return;

            _counting = true;
            _timer = LocalSettingsTabSingleton<BorLocalSettingsTab>.Instance.AutoRejoinDelay.Value;
            Info($"Auto rejoin in {_timer}s");
        }

        _timer -= Time.unscaledDeltaTime;
        if (_timer > 0f) return;

        enabled = false;

        // What the "Play Again" button does.
        _navigation!.NextGame();
    }

    private void OnGUI()
    {
        if (!enabled || !_counting) return;

        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _style.normal.textColor = new Color(0f, 0.85f, 1f, 1f);
            _shadow = new GUIStyle(_style);
            _shadow.normal.textColor = new Color(0f, 0f, 0f, 0.65f);
        }

        var seconds = Mathf.Max(0, Mathf.CeilToInt(_timer));
        var text = string.Format(BetterOtherRolesPlugin.Culture,
            MiraLocaleManager.Get("BetterOtherRoles.Lobby.AutoRejoinCountdown", "Rejoining in {0}s"),
            seconds);
        if (_gameCode.Length > 0)
            text += $" ({_gameCode})";

        var width = Screen.width * 0.7f;
        var rect = new Rect((Screen.width - width) / 2f, Screen.height - 80f, width, 44f);
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, _shadow);
        GUI.Label(rect, text, _style);
    }
}
