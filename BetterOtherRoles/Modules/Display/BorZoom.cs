using BetterOtherRoles.Assets;
using BetterOtherRoles.Options;
using BetterOtherRoles.Patches.Misc;
using HarmonyLib;
using InnerNet;
using MiraAPI.GameOptions;
using UnityEngine;

namespace BetterOtherRoles.Modules.Display;

/// <summary>
///     Zoom out of a dead player (BetterOtherRoles 1.7.1 zoomOutButton, TOU-Mira 1.7.9 ZoomButton): a button under the
///     role guide button switches between the normal view and a view 4 times wider; the mouse wheel and two keybinds
///     zoom step by step (<see cref="MinSize"/> to <see cref="MaxSize"/>). With <see cref="GameplayOptions.FinishTasksBeforeZoom"/>,
///     only once the tasks are done. Back to normal in meetings, the zoom of before given back once the ejection is
///     over; the button is hidden from the start of the meeting to the end of the ejection. Back to
///     normal at the end of the game.
///     Only the view of the map zooms: the interface (buttons, task list, tasks, map) keeps its size. BOR and TOU zoom
///     the interface camera too, the game placing the interface and the clicks with the main camera; here the main
///     camera alone zooms, the clicks are placed with the interface camera (Controller.mainCam) and the interface with
///     its size (CameraSafeArea.GetSafeOrthographicSize).
/// </summary>
[HarmonyPatch]
public static class BorZoom
{
    private const float MinSize = 3f;
    private const float ButtonSize = 12f;
    private const float MaxSize = 15f;
    private const float StepFactor = 1.25f;

    private static GameObject? _button;
    private static bool _shownZoomedOut;

    // Size of the view when a meeting started, given back after the ejection.
    private static float? _sizeBeforeMeeting;

    /// <summary>
    ///     Gets whether the view is zoomed out.
    /// </summary>
    public static bool IsZoomed => _zoomedCamera && _zoomedCamera == Camera.main;

    // The camera zoomed out here: the game may change the size of the camera itself (meeting animation), it is not a
    // zoom of the player and is left alone.
    private static Camera? _zoomedCamera;

    // Dead in a game, meetings included.
    private static bool IsDeadInGame
    {
        get
        {
            var player = PlayerControl.LocalPlayer;
            return player && player.Data && player.Data.IsDead && ShipStatus.Instance &&
                   AmongUsClient.Instance && AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Started;
        }
    }

    // Dead in a game, out of meetings and ejections.
    private static bool IsAllowedNow => IsDeadInGame && !MeetingHud.Instance && !ExileController.Instance;

    // With the option, a dead player zooms out once its tasks are done (none for an impostor).
    private static bool MayZoom =>
        IsDeadInGame && (!OptionGroupSingleton<GameplayOptions>.Instance.FinishTasksBeforeZoom.Value || TasksDone(PlayerControl.LocalPlayer));

    private static bool CanZoomOut => IsAllowedNow && MayZoom;

    // From the start of a meeting to the end of the ejection.
    private static bool IsMeeting => MeetingHud.Instance || ExileController.Instance;

    // A menu over the game: the wheel and the keys are for it.
    private static bool IsMenuOpen
    {
        get
        {
            var hud = HudManager.Instance;
            return Minigame.Instance || (MapBehaviour.Instance && MapBehaviour.Instance.IsOpen) ||
                   (hud.Chat && hud.Chat.IsOpenOrOpening) || (hud.GameMenu && hud.GameMenu.IsOpen) ||
                   (MatchInfoGuide.Instance && MatchInfoGuide.Instance.IsActive);
        }
    }

    /// <summary>
    ///     Switches between the normal view and the wide view (the button).
    /// </summary>
    public static void Toggle()
    {
        if (IsZoomed)
            SetSize(MinSize);
        else if (CanZoomOut)
            SetSize(ButtonSize);
    }

    /// <summary>
    ///     Zooms one step (the wheel and the keybinds).
    /// </summary>
    /// <param name="zoomOut">True to see wider.</param>
    public static void Step(bool zoomOut)
    {
        if (!HudManager.InstanceExists || IsMenuOpen || !Camera.main) return;
        if (zoomOut && !CanZoomOut) return;
        if (!zoomOut && !IsZoomed) return;

        var size = IsZoomed ? Camera.main.orthographicSize : MinSize;
        SetSize(Mathf.Clamp(zoomOut ? size * StepFactor : size / StepFactor, MinSize, MaxSize));
    }

    /// <summary>
    ///     Back to the normal view.
    /// </summary>
    public static void Reset()
    {
        if (IsZoomed)
            SetSize(MinSize);
    }

    /// <summary>
    ///     Shows the button when the player can zoom, with the icon of what a click does. Called by
    ///     <see cref="VerticalButtonGroupPatches"/>, which places it.
    /// </summary>
    /// <param name="guide">The role guide button.</param>
    public static void RefreshButton(Transform guide)
    {
        if (!_button)
        {
            _button = VerticalButtonGroupPatches.CreateButton(guide, "BorZoomButton", BorAssets.ZoomOutButton, BorAssets.ZoomOutButtonActive, Toggle);
            _shownZoomedOut = false;
        }

        var visible = IsZoomed || (MayZoom && !IsMeeting);
        if (_button!.activeSelf != visible)
            _button.SetActive(visible);

        if (_shownZoomedOut == IsZoomed) return;

        _shownZoomedOut = IsZoomed;
        if (_shownZoomedOut)
            VerticalButtonGroupPatches.SetIcons(_button.transform, BorAssets.ZoomInButton, BorAssets.ZoomInButtonActive);
        else
            VerticalButtonGroupPatches.SetIcons(_button.transform, BorAssets.ZoomOutButton, BorAssets.ZoomOutButtonActive);
    }

    private static bool TasksDone(PlayerControl player)
    {
        var tasks = player.Data.Tasks;
        if (tasks == null) return true;

        foreach (var task in tasks)
        {
            if (!task.Complete) return false;
        }

        return true;
    }

    private static void SetSize(float size)
    {
        var camera = Camera.main;
        if (!camera) return;

        if (!Mathf.Approximately(camera.orthographicSize, size))
            camera.orthographicSize = size;

        _zoomedCamera = size > MinSize + 0.01f ? camera : null;
    }

    // The interface camera, for the clicks and the places of the interface, while zoomed out.
    private static Camera? InterfaceCamera => HudManager.InstanceExists && HudManager.Instance.UICamera && HudManager.Instance.UICamera != Camera.main ? HudManager.Instance.UICamera : null;

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    [HarmonyPostfix]
    public static void HudUpdatePostfix()
    {
        if (IsZoomed && !IsAllowedNow)
        {
            ResetForMeeting();
            return;
        }

        if (!IsDeadInGame)
            _sizeBeforeMeeting = null;

        if (!IsAllowedNow) return;

        // The ejection is over: the zoom of before the meeting.
        if (_sizeBeforeMeeting is { } size)
        {
            _sizeBeforeMeeting = null;
            if (CanZoomOut)
                SetSize(size);
        }

        if (IsMenuOpen) return;

        var wheel = Input.GetAxis("Mouse ScrollWheel");
        if (wheel > 0f)
            Step(false);
        else if (wheel < 0f)
            Step(true);
    }

    // Each frame, before the touches are read: the clicks on the interface while zoomed out.
    [HarmonyPatch(typeof(Controller), nameof(Controller.Update))]
    [HarmonyPrefix]
    public static void ControllerUpdatePrefix(Controller __instance)
    {
        var interfaceCamera = InterfaceCamera;
        if (!interfaceCamera) return;

        if (IsZoomed)
        {
            if (__instance.mainCam != interfaceCamera)
                __instance.mainCam = interfaceCamera;
        }
        else if (__instance.mainCam == interfaceCamera)
        {
            __instance.mainCam = Camera.main;
        }
    }

    // The interface is placed with the size of the normal view (AspectPosition, task list, chat...).
    [HarmonyPatch(typeof(CameraSafeArea), nameof(CameraSafeArea.GetSafeOrthographicSize))]
    [HarmonyPrefix]
    public static bool GetSafeOrthographicSizePrefix(Camera cam, ref float __result)
    {
        if (!cam || cam != Camera.main || !IsZoomed || !InterfaceCamera) return true;

        __result = InterfaceCamera!.orthographicSize;
        return false;
    }

    // GetSafeOrthographicSize is one line, copied into its callers by IL2CPP: the patch above is not always called.
    // The places of the interface are given here too. Task list, sliding panels, player list: placed each frame.
    [HarmonyPatch(typeof(AspectPosition), nameof(AspectPosition.ComputePosition), typeof(AspectPosition.EdgeAlignments), typeof(Vector3))]
    [HarmonyPrefix]
    public static bool ComputePositionPrefix(AspectPosition.EdgeAlignments alignment, Vector3 relativePos, ref Vector3 __result)
    {
        var interfaceCamera = InterfaceCamera;
        if (!IsZoomed || !interfaceCamera) return true;

        var safeArea = Screen.safeArea;
        var aspect = Mathf.Min(interfaceCamera!.aspect, safeArea.width / safeArea.height);
        __result = AspectPosition.ComputePosition(alignment, relativePos, interfaceCamera.orthographicSize, aspect);
        return false;
    }

    // Buttons at the screen edges, placed when shown: placed with the interface camera, lent for the time of the call.
    [HarmonyPatch(typeof(AspectPosition), nameof(AspectPosition.AdjustPosition), typeof(float))]
    [HarmonyPrefix]
    public static void AdjustPositionPrefix(AspectPosition __instance, out bool __state)
    {
        __state = false;
        var interfaceCamera = InterfaceCamera;
        if (!IsZoomed || !interfaceCamera || (__instance.parentCam && __instance.parentCam != Camera.main)) return;

        _lentFrom = __instance.parentCam;
        __instance.parentCam = interfaceCamera;
        __state = true;
    }

    [HarmonyPatch(typeof(AspectPosition), nameof(AspectPosition.AdjustPosition), typeof(float))]
    [HarmonyPostfix]
    public static void AdjustPositionPostfix(AspectPosition __instance, bool __state)
    {
        if (!__state) return;

        __instance.parentCam = _lentFrom;
        _lentFrom = null;
    }

    // Camera of an AspectPosition while the interface camera is lent to it (AdjustPositionPrefix).
    private static Camera? _lentFrom;

    // Menus fitted to the screen (AspectSize shrinks them when the screen is too small, Debug menu...): fitted to the
    // interface camera, the screen would look 4 times bigger.
    [HarmonyPatch(typeof(AspectSize), "GetScale")]
    [HarmonyPrefix]
    public static void AspectSizeGetScalePrefix(ref float orthographicSize)
    {
        if (IsZoomed && InterfaceCamera && Mathf.Approximately(orthographicSize, Camera.main.orthographicSize))
            orthographicSize = InterfaceCamera!.orthographicSize;
    }

    // Before the meeting animation and the meeting screen, placed with the main camera (BOR: MeetingPatch).
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.StartMeeting))]
    [HarmonyPrefix]
    public static void StartMeetingPrefix() => ResetForMeeting();

    // The host makes the meeting screen before StartMeeting (PlayerControl.ReportDeadBody, Debug menu): it would be
    // placed with the camera still zoomed out.
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.OpenMeetingRoom))]
    [HarmonyPrefix]
    public static void OpenMeetingRoomPrefix() => ResetForMeeting();

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
    [HarmonyPostfix]
    public static void GameEndPostfix()
    {
        _sizeBeforeMeeting = null;
        Reset();
    }

    // Back to normal, the zoom kept for after the ejection (outside a game, forgotten by HudUpdatePostfix).
    private static void ResetForMeeting()
    {
        if (!IsZoomed) return;

        _sizeBeforeMeeting = Camera.main.orthographicSize;
        Reset();
    }
}
