using AmongUs.Data;
using HarmonyLib;
using InnerNet;
using MiraAPI.Translation;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Modules.Lobby;

/// <summary>
///     Previous lobby joined again with Tab (TOU-Mira 1.7.9 QuickJoinPatch, without its region display): the last lobby
///     joined (host included) is kept for the session, and the "Enter Code" screen shows it on the right, Tab trying to
///     join it once per opening of the screen. Hidden code in streamer mode.
/// </summary>
[HarmonyPatch]
public static class BorQuickJoin
{
    private const string PanelName = "BorPreviousLobby";

    // Last lobby joined in this session, 0 for none.
    private static int _gameId;
    private static GameObject? _panel;
    private static TextMeshPro? _text;
    private static bool _joinAttempted;

    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.JoinGame))]
    [HarmonyPostfix]
    public static void JoinGamePostfix(InnerNetClient __instance)
    {
        if (__instance.GameId != 0)
            _gameId = __instance.GameId;
    }

    [HarmonyPatch(typeof(EnterCodeManager), nameof(EnterCodeManager.OnEnable))]
    [HarmonyPostfix]
    public static void OnEnablePostfix(EnterCodeManager __instance)
    {
        _joinAttempted = false;
        if (!_panel)
            CreatePanel(__instance);

        if (_panel)
            _panel!.SetActive(_gameId != 0);
    }

    [HarmonyPatch(typeof(EnterCodeManager), nameof(EnterCodeManager.OnDisable))]
    [HarmonyPostfix]
    public static void OnDisablePostfix()
    {
        if (_panel)
            _panel!.SetActive(false);
    }

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.LateUpdate))]
    [HarmonyPostfix]
    public static void LateUpdatePostfix()
    {
        if (_gameId == 0 || !_panel || !_panel!.activeInHierarchy || !_text) return;

        if (Input.GetKeyDown(KeyCode.Tab) && !_joinAttempted)
        {
            _joinAttempted = true;
            AmongUsClient.Instance.StartCoroutine(AmongUsClient.Instance.CoFindGameInfoFromCodeAndJoin(_gameId));
        }

        var code = DataManager.Settings.Gameplay.StreamerMode ? "******" : GameCode.IntToGameName(_gameId);
        var text = $"<size=110%>{MiraLocaleManager.Get("BetterOtherRoles.QuickJoin.Previous", "Previous lobby:")}</size>\n" +
                   $"<size=4.6>{code}</size>\n" +
                   $"<size=2.6>{MiraLocaleManager.Get(_joinAttempted ? "BetterOtherRoles.QuickJoin.Joining" : "BetterOtherRoles.QuickJoin.PressTab", "Press Tab to join it")}</size>";
        if (_text!.text != text)
            _text.text = text;
    }

    // Copy of the screen header, on the right of the code field (place and sizes of TOU-Mira 1.7.9).
    private static void CreatePanel(EnterCodeManager manager)
    {
        var header = manager.transform.Find("Header");
        if (!header || header.childCount < 2)
        {
            Warning("Quick join: no Header on the Enter Code screen, previous lobby not shown.");
            return;
        }

        _panel = Object.Instantiate(header.gameObject, manager.transform);
        _panel.name = PanelName;
        _panel.transform.localPosition = new Vector3(1f, 0f, 0f);
        Object.Destroy(_panel.transform.GetChild(0).gameObject);

        _text = _panel.transform.GetChild(1).GetComponent<TextMeshPro>();
        if (!_text) return;

        // The header text is translated by the game: it would write its title back.
        var translator = _text!.GetComponent<TextTranslatorTMP>();
        if (translator)
            Object.Destroy(translator);

        _text.fontSizeMin = 3.35f;
        _text.fontSizeMax = 3.35f;
        _text.fontSize = 3.35f;
        _text.alignment = TextAlignmentOptions.Center;
        _text.text = string.Empty;
    }
}
