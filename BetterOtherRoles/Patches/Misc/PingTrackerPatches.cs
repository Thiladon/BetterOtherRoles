using BetterOtherRoles.LocalSettings;
using BetterOtherRoles.Modules.Display;
using HarmonyLib;
using MiraAPI.LocalSettings;
using Reactor.Utilities;
using TMPro;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Text of the ping, at the top of the screen in the lobby and in game, at the bottom in meetings
///     (BetterOtherRoles 1.7.1 CredentialsPatch):
///     the colored logo of BOR and its version above, then the ping of the game, followed by the FPS with
///     <see cref="BorLocalSettingsTab.ShowFps"/>.
/// </summary>
[HarmonyPatch]
public static class PingTrackerPatches
{
    private const string ColoredLogo = "<color=#fcba03>Better</color><color=#ff351f>OtherRoles</color>";
    private const string FpsColor = "#00FF00";

    private const string PingSize = "80%";

    // In game, the text keeps one size: TextMeshPro sized it itself to fit its box, more or less big from one game
    // to the other. In the lobby, the size of the game is given back.
    private const float GameFontSize = 2.1f;

    private static readonly string VersionLine = $"<size=130%>{ColoredLogo}</size> v{BetterOtherRolesPlugin.Version}";

    private static bool? _lobbyAutoSizing;
    private static float _lobbyFontSize;

    // In the lobby: centered at the top of the screen (BOR 1.7.1).
    private static readonly Vector3 LobbyDistance = new(0f, 0.1f, 0f);

    // In game: at the top left, between the task bar and the task list, which is lowered below it
    // (the game puts the ping at the bottom, above the name of the room). Distances from the edges, in world units.
    private const float GameLeft = 0.12f;
    private const float GameTop = 0.55f;
    private const float TaskListGap = 0.1f;

    // In meetings: centered at the bottom of the screen, the ping above the version (the top left is taken by the
    // meeting screen).
    private const float MeetingBottom = 0.1f;

    private enum Place
    {
        Lobby,
        Game,
        Meeting,
    }

    /// <summary>
    ///     Gets the distance from the top of the screen to the bottom of the texts in game (0 when not shown):
    ///     the task list stays below it (<see cref="TaskPanelUpdatePostfix"/>).
    /// </summary>
    public static float GameBottom { get; private set; }

    [HarmonyPatch(typeof(PingTracker), nameof(PingTracker.Update))]
    [HarmonyPostfix]
    public static void UpdatePostfix(PingTracker __instance)
    {
        var ping = __instance.text.text;
        var settings = LocalSettingsTabSingleton<BorLocalSettingsTab>.Instance;
        if (settings != null && settings.ShowFps.Value && BorFpsCounter.Fps > 0f)
            ping += $" - <color={FpsColor}><b>{BorFpsCounter.Fps:F0} FPS</b></color>";

        ping = $"<size={PingSize}>{ping}</size>";

        var place = GetPlace();
        switch (place)
        {
            case Place.Game:
                __instance.text.text = $"{VersionLine}\n{ping}";
                __instance.text.alignment = TextAlignmentOptions.TopLeft;
                break;
            case Place.Meeting:
                __instance.text.text = $"{ping}\n{VersionLine}";
                __instance.text.alignment = TextAlignmentOptions.Bottom;
                break;
            default:
                __instance.text.text = $"{VersionLine}\n{ping}";
                __instance.text.alignment = TextAlignmentOptions.Top;
                break;
        }

        UpdateSize(__instance.text, place != Place.Lobby);
        UpdatePosition(__instance, place);
    }

    private static Place GetPlace()
    {
        if (!AmongUsClient.Instance || !AmongUsClient.Instance.IsGameStarted) return Place.Lobby;

        return MeetingHud.Instance ? Place.Meeting : Place.Game;
    }

    private static void UpdateSize(TextMeshPro text, bool inGame)
    {
        if (_lobbyAutoSizing == null)
        {
            _lobbyAutoSizing = text.enableAutoSizing;
            _lobbyFontSize = text.fontSize;
        }

        if (inGame)
        {
            if (text.enableAutoSizing)
                text.enableAutoSizing = false;
            if (!Mathf.Approximately(text.fontSize, GameFontSize))
                text.fontSize = GameFontSize;
        }
        else if (text.enableAutoSizing != _lobbyAutoSizing.Value)
        {
            text.enableAutoSizing = _lobbyAutoSizing.Value;
            text.fontSize = _lobbyFontSize;
        }
    }

    // The game sets its own distance every frame, before this patch: placed again every frame, as BOR does.
    // The place is the one of the center of the text box: moved so its corner or its edge is at the place.
    // The task list keeps its place during meetings (GameBottom unchanged), where it is hidden.
    private static void UpdatePosition(PingTracker ping, Place place)
    {
        var position = ping.GetComponent<AspectPosition>();
        if (!position) return;

        var box = ping.text.rectTransform;
        var scale = box.localScale;
        var rect = box.rect;
        if (place == Place.Game)
        {
            position.Alignment = AspectPosition.EdgeAlignments.LeftTop;
            position.DistanceFromEdge = new Vector3(
                GameLeft + rect.width * box.pivot.x * scale.x,
                GameTop + rect.height * (1f - box.pivot.y) * scale.y,
                0f);
            GameBottom = GameTop + ping.text.renderer.bounds.size.y;
        }
        else if (place == Place.Meeting)
        {
            position.Alignment = AspectPosition.EdgeAlignments.Bottom;
            position.DistanceFromEdge = new Vector3(0f, MeetingBottom + rect.height * box.pivot.y * scale.y, 0f);
        }
        else
        {
            position.Alignment = AspectPosition.EdgeAlignments.Top;
            position.DistanceFromEdge = LobbyDistance;
            GameBottom = 0f;
        }

        position.AdjustPosition();
    }

    // The game places the task list at 0.6 from the top every frame: lowered below the texts. The tab of the role
    // (MiraAPI "RolePanel", a copy of the task list placed below it) is lowered by as much.
    [HarmonyPatch(typeof(TaskPanelBehaviour), nameof(TaskPanelBehaviour.Update))]
    [HarmonyPostfix]
    public static void TaskPanelUpdatePostfix(TaskPanelBehaviour __instance)
    {
        if (GameBottom <= 0f || !HudManager.InstanceExists || (GameManager.Instance && GameManager.Instance.IsHideAndSeek())) return;

        var taskPanel = HudManager.Instance.TaskPanel;
        if (!taskPanel) return;

        var shift = GameBottom + TaskListGap - taskPanel.openPosition.y;
        if (shift <= 0f) return;

        var place = __instance.transform.localPosition;
        place.y -= shift;
        __instance.transform.localPosition = place;
    }

    /// <summary>
    ///     Reactor adds the name and version of each mod under the ping (BOR and MiraAPI): only the line of BOR is kept,
    ///     above. They are still listed on the main menu.
    /// </summary>
    [HarmonyPatch(typeof(ReactorCredits), "GetText")]
    [HarmonyPrefix]
    public static bool GetTextPrefix(ReactorCredits.Location location, ref string? __result)
    {
        if (location != ReactorCredits.Location.PingTracker) return true;

        __result = null;
        return false;
    }
}
