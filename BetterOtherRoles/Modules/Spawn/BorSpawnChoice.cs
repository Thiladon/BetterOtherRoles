using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using BetterOtherRoles.Options.Maps;
using BetterOtherRoles.Utilities;
using HarmonyLib;
using MiraAPI.GameOptions;
using MiraAPI.Translation;
using Reactor.Networking.Attributes;
using Reactor.Utilities;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace BetterOtherRoles.Modules.Spawn;

/// <summary>
///     Spawn room chosen in the window of the Airship (<see cref="SpawnChoiceOptions"/>), on the maps whose box is
///     ticked, at the start of the game (PrespawnStep, empty on these maps) and after each meeting (end of the ejection),
///     with the rooms of <see cref="BorSpawnRooms"/>: everyone who picks a room arrives on its point. The window is loaded
///     from the prefab of the Airship while the intro plays.
///     Rooms offered (TOU-Mira 1.7.9 AirshipSpawnPatch): drawn for each player by the window, the same three for everyone
///     (drawn by the host and sent before the window opens), or none: everyone is put in the room set by the host.
///     Airship: its own window and locations; with "start of the game", its choice after a meeting is left out (the
///     players stay where they were, as the game does there). Not in free play.
/// </summary>
[HarmonyPatch]
public static class BorSpawnChoice
{
    // Rooms offered with "the same ones for everyone" (buttons of the window).
    private const int SameCount = 3;

    // Time waited for the window loaded from the Airship, before leaving the players where they are.
    private const float LoadTimeout = 10f;

    // Prefab of the Airship (holds the window), released with the map.
    private static AsyncOperationHandle<GameObject>? _airship;

    private static SpawnInMinigame? _window;

    // Rooms drawn by the host for the next window (indices in BorSpawnRooms.Of), used once.
    private static int[]? _sameRooms;

    // Picture of a room without one (until the pictures of the rooms are made).
    private static Sprite? _placeholder;

    // Time the host waits for every choice from its window opening, before placing everyone anyway.
    private const float HostWaitTimeout = 14f;

    // Time a player (not the host) waits for the host from its window opening, before going to its room alone.
    private const float PlayerWaitTimeout = 17f;

    // Players who chose their room (sent to everyone, counted by the host), for this window.
    private static readonly HashSet<byte> Chosen = [];

    // Window open on this player, and the room it chose (placed once everyone chose).
    private static SpawnInMinigame? _open;

    private static SpawnInMinigame.SpawnLocation? _choice;

    // Everyone chose (sent by the host): the player goes to its room as soon as it chose.
    private static bool _placeAll;

    // The original SpawnAt called to place the player.
    private static bool _placing;

    private static SpawnChoiceOptions Options => OptionGroupSingleton<SpawnChoiceOptions>.Instance;

    private static MapNames Map => (MapNames)GameOptionsManager.Instance.CurrentGameOptions.MapId;

    /// <summary>
    ///     Start of the intro: the same rooms drawn by the host, the window loaded from the Airship on another map.
    /// </summary>
    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin))]
    [HarmonyPrefix]
    public static void IntroBeginPrefix()
    {
        ResetRound();
        if (!Applies(false)) return;

        DrawSameRooms();
        if (Map != MapNames.Airship && Options.OfferedRooms != BorSpawnLocations.Host && _airship == null)
            Coroutines.Start(CoLoadWindow());
    }

    /// <summary>
    ///     Start of a meeting: the same rooms drawn by the host for the window after the meeting.
    /// </summary>
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    [HarmonyPostfix]
    public static void MeetingStartPostfix()
    {
        ResetRound();
        if (Applies(true)) DrawSameRooms();
    }

    /// <summary>
    ///     Start of the game on the maps other than the Airship (the game does nothing there): window or room of the
    ///     host, while the screen is still black.
    /// </summary>
    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.PrespawnStep))]
    [HarmonyPrefix]
    public static bool ShipPrespawnPrefix(ShipStatus __instance, ref Il2CppSystem.Collections.IEnumerator __result)
    {
        if (__instance.TryCast<AirshipStatus>() || !Applies(false)) return true;

        __result = CoSpawn().WrapToIl2Cpp();
        return false;
    }

    // Airship, after a meeting with "start of the game": the players in two rows around the table of the meeting room,
    // as on Polus (points noted by the user on 2026-11-25: center of the table, and the gap to stand beside it).
    private static readonly Vector2 AirshipTableCenter = new(11.05f, 15.33f);

    // Gap from the center of the table to the top row and to the bottom row (the body stands above its point).
    private const float AirshipRowGap = 0.67f;

    // Space between two players of a row (0.6 on Polus, too narrow around the table).
    private const float RowSpacing = 0.9f;

    /// <summary>
    ///     Airship, at the start and after each meeting: no choice after a meeting with "start of the game" (the players
    ///     go around the table of the meeting room, as the other maps put them in their meeting room), room of the host
    ///     without the window.
    /// </summary>
    [HarmonyPatch(typeof(AirshipStatus), nameof(AirshipStatus.PrespawnStep))]
    [HarmonyPrefix]
    public static bool AirshipPrespawnPrefix(ref Il2CppSystem.Collections.IEnumerator __result)
    {
        if (TutorialManager.InstanceExists || !SpawnChoiceOptions.IsOn()) return true;

        var afterMeeting = (bool)ExileController.Instance;
        if (afterMeeting && Options.ChoiceMode != BorSpawnChoiceMode.StartAndMeetings)
        {
            __result = CoMeetingRows().WrapToIl2Cpp();
            return false;
        }

        if (Options.OfferedRooms != BorSpawnLocations.Host) return true;

        __result = CoSpawn().WrapToIl2Cpp();
        return false;
    }

    /// <summary>
    ///     End of the ejection on the maps other than the Airship: as AirshipExileController.WrapUpAndSpawn, the window
    ///     (or the room of the host) comes while the screen is still black, before the game is back (ReEnableGameplay:
    ///     fade, HUD, cooldowns, round start of MiraAPI), unless the game ends.
    /// </summary>
    [HarmonyPatch(typeof(ExileController), nameof(ExileController.WrapUp))]
    [HarmonyPrefix]
    public static bool WrapUpPrefix(ExileController __instance)
    {
        if (!ShipStatus.Instance || ShipStatus.Instance.TryCast<AirshipStatus>() || !Applies(true)) return true;

        Coroutines.Start(CoWrapUp(__instance));
        return false;
    }

    /// <summary>
    ///     Window of the Airship: only the rooms drawn by the host with "the same ones for everyone".
    /// </summary>
    [HarmonyPatch(typeof(SpawnInMinigame), nameof(SpawnInMinigame.Begin))]
    [HarmonyPrefix]
    public static void WindowBeginPrefix(SpawnInMinigame __instance)
    {
        if (Map != MapNames.Airship || TakeSameRooms() is not { } rooms) return;

        var names = rooms.Select(x => Enum.TryParse<StringNames>(x.Key, out var name) ? name : StringNames.None).ToList();
        var locations = __instance.Locations.Where(x => names.Contains(x.Name)).ToArray();
        if (locations.Length < __instance.LocationButtons.Length)
        {
            Warning($"[SpawnChoice] Airship: {locations.Length} of the rooms drawn found, all the rooms offered.");
            return;
        }

        __instance.Locations = new(locations);
    }

    /// <summary>
    ///     A window opened while the choice is on: the players are placed once everyone chose (the host counts them).
    /// </summary>
    [HarmonyPatch(typeof(SpawnInMinigame), nameof(SpawnInMinigame.Begin))]
    [HarmonyPostfix]
    public static void WindowBeginPostfix(SpawnInMinigame __instance)
    {
        if (!WaitsForEveryone()) return;

        _open = __instance;
        _choice = null;
        Coroutines.Start(AmongUsClient.Instance.AmHost ? CoHostWait() : CoPlayerWait(__instance));
    }

    /// <summary>
    ///     A room chosen (button or end of the timer): kept and sent, the window waits for the other players.
    /// </summary>
    [HarmonyPatch(typeof(SpawnInMinigame), nameof(SpawnInMinigame.SpawnAt))]
    [HarmonyPrefix]
    public static bool SpawnAtPrefix(SpawnInMinigame __instance, SpawnInMinigame.SpawnLocation spawnPoint)
    {
        if (_placing || !_open || __instance.Pointer != _open!.Pointer) return true;
        if (__instance.amClosing != Minigame.CloseState.None || _choice != null) return false;

        _choice = spawnPoint;
        __instance.gotButton = true;
        __instance.StopAllCoroutines();
        __instance.Text.text = MiraLocaleManager.Get("BetterOtherRoles.SpawnChoice.Waiting", "Waiting for the other players...");        RpcChosen(PlayerControl.LocalPlayer);
        if (_placeAll) Place();

        return false;
    }

    /// <summary>
    ///     The map destroyed (end of the game): prefab of the Airship released, rooms drawn forgotten.
    /// </summary>
    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.OnDestroy))]
    [HarmonyPostfix]
    public static void ShipDestroyPostfix()
    {
        if (_airship is { } handle) Addressables.Release(handle);

        _airship = null;
        _window = null;
        _sameRooms = null;
        ResetRound();
    }

    // Start of the intro or of a meeting: nothing chosen yet. The rooms drawn by the host are kept: they can come
    // before the intro or the meeting starts here (forgotten once used).
    private static void ResetRound()
    {
        Chosen.Clear();
        _open = null;
        _choice = null;
        _placeAll = false;
    }

    // Whether the players wait for everyone in the window (not in free play, nor with the room of the host).
    private static bool WaitsForEveryone() =>
        !TutorialManager.InstanceExists && SpawnChoiceOptions.IsOn() && Options.AppliesTo(Map) &&
        Options.OfferedRooms != BorSpawnLocations.Host;

    [MethodRpc((uint)BorRpc.SpawnChosen)]
    private static void RpcChosen(PlayerControl player)
    {
        if (player) Chosen.Add(player.PlayerId);
    }

    [MethodRpc((uint)BorRpc.SpawnPlaceAll)]
    private static void RpcPlaceAll(PlayerControl host)
    {
        if (!host || host.OwnerId != AmongUsClient.Instance.HostId) return;
        _placeAll = true;
        if (_choice != null)
            Place();
        else if (_open && _open!.amClosing == Minigame.CloseState.None)
            _open.LocationButtons[Random.Range(0, _open.LocationButtons.Length)].ReceiveClickUp();
    }

    // Host: everyone connected chose, or the time is over.
    private static IEnumerator CoHostWait()
    {
        for (var time = 0f; time < HostWaitTimeout && !EveryoneChose(); time += Time.deltaTime)
            yield return null;
        RpcPlaceAll(PlayerControl.LocalPlayer);
    }

    private static bool EveryoneChose() =>
        GameData.Instance.AllPlayers.ToArray()
            .Where(x => x && !x.Disconnected && x.Object && !x.Object.isDummy)
            .All(x => Chosen.Contains(x.PlayerId));

    // No word from the host (left the game...): the player goes to its room alone (one drawn if it did not choose).
    private static IEnumerator CoPlayerWait(SpawnInMinigame window)
    {
        for (var time = 0f; time < PlayerWaitTimeout; time += Time.deltaTime)
        {
            if (!window || _placeAll) yield break;

            yield return null;
        }

        Warning("[SpawnChoice] no word from the host, placed alone.");
        _placeAll = true;
        if (_choice != null)
            Place();
        else if (_open && _open!.amClosing == Minigame.CloseState.None)
            _open.LocationButtons[Random.Range(0, _open.LocationButtons.Length)].ReceiveClickUp();
    }

    // The player goes to the room it chose (original SpawnAt: snapped, then the window closes).
    private static void Place()
    {
        var window = _open;
        var choice = _choice;
        _open = null;
        if (!window || choice == null || window!.amClosing != Minigame.CloseState.None) return;

        _placing = true;
        try
        {
            window.SpawnAt(choice);
        }
        finally
        {
            _placing = false;
        }
    }

    // Whether the spawn room is chosen on this map, at the start or after a meeting.
    private static bool Applies(bool afterMeeting)
    {
        if (TutorialManager.InstanceExists || !SpawnChoiceOptions.IsOn() || !Options.AppliesTo(Map)) return false;

        return !afterMeeting || Options.ChoiceMode == BorSpawnChoiceMode.StartAndMeetings;
    }

    // Host, "the same ones for everyone": three rooms of the map sent to everyone.
    private static void DrawSameRooms()
    {
        if (!AmongUsClient.Instance.AmHost || Options.OfferedRooms != BorSpawnLocations.Same) return;

        var count = BorSpawnRooms.Of(Map).Length;
        if (count < SameCount) return;

        var picks = Enumerable.Range(0, count).OrderBy(_ => Random.value).Take(SameCount).ToArray();        RpcSameRooms(PlayerControl.LocalPlayer, picks[0], picks[1], picks[2]);
    }

    [MethodRpc((uint)BorRpc.SpawnRooms)]
    private static void RpcSameRooms(PlayerControl host, int first, int second, int third)
    {
        if (!host || host.OwnerId != AmongUsClient.Instance.HostId) return;

        _sameRooms = [first, second, third];    }

    // Rooms drawn by the host for this window (forgotten once used), or null when none were received.
    private static BorSpawnRoom[]? TakeSameRooms()
    {
        var picks = _sameRooms;
        _sameRooms = null;
        if (Options.OfferedRooms != BorSpawnLocations.Same || picks == null) return null;

        var rooms = BorSpawnRooms.Of(Map);
        if (picks.Any(x => x < 0 || x >= rooms.Length)) return null;

        return picks.Select(x => rooms[x]).ToArray();
    }

    private static IEnumerator CoLoadWindow()
    {
        var handle = Addressables.LoadAssetAsync<GameObject>(AmongUsClient.Instance.ShipPrefabs[(int)MapNames.Airship].RuntimeKey);
        _airship = handle;
        while (!handle.IsDone) yield return null;

        if (_airship == null || handle.Status != AsyncOperationStatus.Succeeded) yield break;

        _window = handle.Result.GetComponent<AirshipStatus>()?.SpawnInGame;    }

    // The player in its place around the table of the Airship: the top row holds the first half (rounded up) of the
    // players here sorted by id, the bottom row the others, each row centered on the table. Everyone gets the same order.
    private static IEnumerator CoMeetingRows()
    {
        var player = PlayerControl.LocalPlayer;
        var players = GameData.Instance.AllPlayers.ToArray()
            .Where(x => x && !x.Disconnected && x.Object && !x.Object.isDummy)
            .Select(x => x.PlayerId)
            .OrderBy(x => x)
            .ToList();
        var rank = players.IndexOf(player.PlayerId);
        if (rank < 0) yield break;

        var position = AirshipTablePlace(rank, players.Count);        player.NetTransform.RpcSnapTo(position);
        HudManager.Instance.PlayerCam.SnapToTarget();
    }

    // Place of a player around the table of the meeting room of the Airship (rank among the players here, sorted by id).
    private static Vector2 AirshipTablePlace(int rank, int count)
    {
        var top = (count + 1) / 2;
        var onTop = rank < top;
        var index = onTop ? rank : rank - top;
        var inRow = onTop ? top : count - top;
        return new Vector2(
            AirshipTableCenter.x + ((index - ((inRow - 1) / 2f)) * RowSpacing) - 0.2f,
            AirshipTableCenter.y + (onTop ? AirshipRowGap : -AirshipRowGap - 0.2f));
    }

    // ExileController.WrapUp with the spawn step of the Airship before the game is back.
    private static IEnumerator CoWrapUp(ExileController exile)
    {
        var exiled = exile.initData?.networkedPlayer;
        if (exiled)
        {
            if (exiled!.Object) exiled.Object.Exiled();

            exiled.IsDead = true;
        }

        if (GameManager.Instance && !GameManager.Instance.LogicFlow.IsGameOverDueToDeath())
        {
            yield return CoSpawn();
            if (exile) exile.ReEnableGameplay();
        }

        if (exile) Object.Destroy(exile.gameObject);
    }

    // Window of the Airship with the rooms of the map, or the room of the host.
    private static IEnumerator CoSpawn()
    {
        var map = Map;
        var rooms = BorSpawnRooms.Of(map);
        if (rooms.Length == 0) yield break;

        if (Options.OfferedRooms == BorSpawnLocations.Host)
        {
            var room = rooms[Mathf.Clamp(Options.HostRoom(map), 0, rooms.Length - 1)];            PlayerControl.LocalPlayer.NetTransform.RpcSnapTo(room.Position);
            HudManager.Instance.PlayerCam.SnapToTarget();
            yield break;
        }

        for (var time = 0f; !_window && time < LoadTimeout; time += Time.deltaTime)
            yield return null;

        if (!_window)
        {
            Warning($"[SpawnChoice] {map}: window of the Airship not loaded, no choice.");
            yield break;
        }

        var window = Object.Instantiate(_window!, Camera.main.transform, false);
        window.transform.localPosition = new Vector3(0f, 0f, -600f);
        var offered = TakeSameRooms() ?? rooms;
        window.Locations = new(Locations(offered, window.LocationButtons.Length));        window.Begin(null);

        // No picture nor animation of the room yet: the buttons stay still when hovered.
        foreach (var button in window.LocationButtons)
        {
            var rollover = button.GetComponent<ButtonAnimRolloverHandler>();
            if (rollover) rollover.enabled = false;
        }

        yield return window.WaitForFinish();
    }

    // Locations of the window (as many as its buttons at least: the rooms repeated when there are fewer).
    private static SpawnInMinigame.SpawnLocation[] Locations(BorSpawnRoom[] rooms, int buttons)
    {
        var image = Placeholder();
        var count = Math.Max(rooms.Length, buttons);
        var locations = new SpawnInMinigame.SpawnLocation[count];
        for (var i = 0; i < count; i++)
        {
            var room = rooms[i % rooms.Length];
            locations[i] = new SpawnInMinigame.SpawnLocation
            {
                Name = room.Name,
                Image = image,
                Location = room.Position,
            };
        }

        return locations;
    }

    // Same size as the pictures of the Airship: a dark frame with a light border.
    private static Sprite? Placeholder()
    {
        if (_placeholder) return _placeholder;

        var model = _window?.Locations.FirstOrDefault()?.Image;
        if (!model) return null;

        var width = (int)model!.rect.width;
        var height = (int)model.rect.height;
        const int border = 4;
        var fill = new Color(0.10f, 0.12f, 0.18f, 0.92f);
        var edge = new Color(0.85f, 0.85f, 0.9f, 1f);
        var pixels = new Color[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var onEdge = x < border || y < border || x >= width - border || y >= height - border;
                pixels[(y * width) + x] = onEdge ? edge : fill;
            }
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels(pixels);
        texture.Apply();
        var pivot = new Vector2(model.pivot.x / width, model.pivot.y / height);
        _placeholder = Sprite.Create(texture, new Rect(0f, 0f, width, height), pivot, model.pixelsPerUnit);
        BorSpriteUtils.KeepLoaded(_placeholder);
        return _placeholder;
    }
}
