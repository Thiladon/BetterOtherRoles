using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using MiraAPI.Translation;
using MiraAPI.Utilities;
using MiraAPI.Utilities.Assets;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     The reversed Skeld (dlekS ehT) as a map of its own, all year, as TOU-Mira 1.7.9 does
///     (Patches/AprilFools/DleksMapOptionPickerPatches.cs, DleksClampPatch.cs, CoStartGameHost.cs), with its images.
///     The game has the map (MapNames.Dleks) but only offers it during its events.
/// </summary>
[HarmonyPatch]
public static class DleksMapPatches
{
    private const string MenusPath = "BetterOtherRoles.Resources.Menus.";

    private static readonly LoadableAsset<Sprite> Banner = new LoadableResourceAsset(MenusPath + "DleksBanner.png");
    private static readonly LoadableAsset<Sprite> Icon = new LoadableResourceAsset(MenusPath + "DleksIcon.png");
    private static readonly LoadableAsset<Sprite> Text = new LoadableResourceAsset(MenusPath + "DleksText.png");
    private static readonly LoadableAsset<Sprite> TextSmall = new LoadableResourceAsset(MenusPath + "DleksText.png", 155f);

    // Place of dlekS in the map picker of the lobby settings: second, after The Skeld.
    private const int PickerIndex = 1;

    private static readonly HashSet<int> MirroredMaps = [];

    private static FreeplayPopover? _lastPopover;

    /// <summary>
    ///     The minimap of dlekS is the one of The Skeld: mirrored once (left / right) by hand, without negative scales
    ///     (a mirrored container drew it upside down, its texts faded and the task icons behind it): the drawing is
    ///     flipped, the room names, sabotage buttons and counters moved to the other side, the task icons mirrored (at
    ///     each opening).
    ///     The player icon is mirrored inside its parent (<see cref="MapFixedUpdatePostfix"/>).
    /// </summary>
    [HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.Show))]
    [HarmonyPostfix]
    public static void MapShowPostfix(MapBehaviour __instance)
    {
        var map = __instance.transform;
        if (!IsDleks) return;

        UnflipTasks(__instance);
        if (!MirroredMaps.Add(__instance.GetInstanceID()))
        {
            PlaceHere(__instance);
            return;
        }

        var background = map.Find("Background")?.GetComponent<SpriteRenderer>();
        if (background)
        {
            background!.flipX = !background.flipX;
            MirrorPosition(background.transform);
        }

        foreach (var name in new[] { "RoomNames (1)", "InfectedOverlay", "CountOverlay" })
        {
            var group = map.Find(name);
            for (var i = 0; group && i < group!.childCount; i++)
            {
                var item = group.GetChild(i);
                MirrorPosition(item);
                for (var j = 0; j < item.childCount; j++)
                {
                    MirrorPosition(item.GetChild(j));
                }
            }
        }

        // Fixed offset of the player icon and the task icons, for the drawing of The Skeld.
        if (__instance.HerePoint)
            MirrorPosition(__instance.HerePoint.transform.parent);

        PlaceHere(__instance);
    }

    // The game puts the player icon at the player position divided by the map scale, x flipped on dlekS (ship scale
    // x negative) to fit the drawing of The Skeld: put back without the flip, for the mirrored drawing.
    [HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.FixedUpdate))]
    [HarmonyPostfix]
    public static void MapFixedUpdatePostfix(MapBehaviour __instance)
    {
        if (MirroredMaps.Contains(__instance.GetInstanceID()))
        {
            UnflipTasks(__instance);
            PlaceHere(__instance);
        }
    }

    // The game flips the task icons for the drawing of The Skeld (negative x scale), again at each opening.
    private static void UnflipTasks(MapBehaviour map)
    {
        var tasks = map.taskOverlay ? map.taskOverlay.transform : null;
        if (tasks && tasks!.localScale.x < 0f)
            tasks.localScale = new Vector3(-tasks.localScale.x, tasks.localScale.y, tasks.localScale.z);
    }

    private static void PlaceHere(MapBehaviour map)
    {
        if (!map.HerePoint || !ShipStatus.Instance || !PlayerControl.LocalPlayer) return;

        var icon = map.HerePoint.transform;
        var x = PlayerControl.LocalPlayer.transform.position.x / ShipStatus.Instance.MapScale;
        icon.localPosition = new Vector3(x, icon.localPosition.y, icon.localPosition.z);
    }

    private static void MirrorPosition(Transform transform)
    {
        var position = transform.localPosition;
        transform.localPosition = new Vector3(-position.x, position.y, position.z);
    }

    private static bool IsDleks => TutorialManager.InstanceExists
        ? AmongUsClient.Instance.TutorialMapId == (int)MapNames.Dleks
        : GameOptionsManager.Instance.CurrentGameOptions.MapId == (byte)MapNames.Dleks;

    private static StringNames MapName { get; } = MiraLocaleManager.GetOrCreateLocaleString("BetterOtherRoles.Maps.Dleks");

    private static StringNames MapTooltip { get; } = MiraLocaleManager.GetOrCreateLocaleString("BetterOtherRoles.Maps.DleksTooltip");

    // Map picker of the lobby settings.
    [HarmonyPatch(typeof(GameOptionsMapPicker), nameof(GameOptionsMapPicker.SetupMapButtons))]
    [HarmonyPrefix]
    public static void SetupMapButtonsPrefix(GameOptionsMapPicker __instance)
    {
        if (__instance.AllMapIcons.ToArray().Any(x => x.Name == MapNames.Dleks)) return;

        // Shown right after The Skeld (the buttons follow the list order, each one keeps its map id).
        __instance.AllMapIcons.Insert(PickerIndex, new MapIconByName
        {
            Name = MapNames.Dleks,
            MapImage = Banner.LoadAsset(),
            MapIcon = Icon.LoadAsset(),
            NameImage = Text.LoadAsset(),
        });
    }

    // Map logo of the lobby.
    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Start))]
    [HarmonyPriority(Priority.First)]
    [HarmonyPrefix]
    public static void GameStartManagerStartPrefix(GameStartManager __instance)
    {
        try
        {
            if (__instance.AllMapIcons.ToArray().Any(x => x.Name == MapNames.Dleks)) return;
        }
        catch (Exception)
        {
            // TOU-Mira: seems to throw on Android.
            return;
        }

        __instance.AllMapIcons.Insert((int)MapNames.Dleks, new MapIconByName
        {
            Name = MapNames.Dleks,
            MapIcon = TextSmall.LoadAsset(),
        });
    }

    // Map name in the settings summary.
    [HarmonyPatch(typeof(MapSelectionGameSetting), nameof(MapSelectionGameSetting.GetValueString))]
    [HarmonyPriority(Priority.VeryLow)]
    [HarmonyPrefix]
    public static void GetValueStringPrefix(MapSelectionGameSetting __instance)
    {
        if (__instance.Values.All(x => (int)x != (int)MapName))
        {
            var list = __instance.Values.ToList();
            list.Insert((int)MapNames.Dleks, MapName);
            __instance.Values = list.ToArray();
        }
    }

    // The game skips dlekS when it picks the shown map (outside of its events).
    [HarmonyPatch(typeof(StringOption), nameof(StringOption.Start))]
    [HarmonyPostfix]
    public static void StringOptionStartPostfix(StringOption __instance)
    {
        if (__instance.Title == StringNames.GameMapName)
            __instance.Value = GameOptionsManager.Instance.CurrentGameOptions.MapId;
    }

    // Game creation menu (online host): banner and crewmates of The Skeld.
    [HarmonyPatch(typeof(CreateGameOptions), nameof(CreateGameOptions.MapChanged))]
    [HarmonyPrefix]
    public static bool MapChangedPrefix(CreateGameOptions __instance)
    {
        if (__instance.mapPicker.GetSelectedID() is not (int)MapNames.Dleks) return true;

        __instance.mapBanner.flipX = false;
        __instance.rendererBGCrewmates.sprite = __instance.bgCrewmates[0];
        __instance.mapBanner.sprite = Text.LoadAsset();
        __instance.TurnOffCrewmates();
        __instance.currentCrewSprites = __instance.skeldCrewSprites;
        __instance.SetCrewmateGraphic(__instance.capacityOption.Value - 1f);
        return false;
    }

    [HarmonyPatch(typeof(CreateGameOptions), nameof(CreateGameOptions.Start))]
    [HarmonyPrefix]
    public static void CreateGameOptionsStartPrefix(CreateGameOptions __instance)
    {
        if (__instance.currentCrewSprites == null)
            __instance.mapBanner.sprite = Text.LoadAsset();

        __instance.currentCrewSprites ??= __instance.skeldCrewSprites;
        __instance.mapTooltips[(int)MapNames.Dleks] = MapTooltip;
    }

    // Freeplay: dlekS button above The Fungle one.
    [HarmonyPatch(typeof(FreeplayPopover), nameof(FreeplayPopover.Show))]
    [HarmonyPriority(Priority.First)]
    [HarmonyPrefix]
    public static void FreeplayShowPrefix(FreeplayPopover __instance)
    {
        if (_lastPopover == __instance) return;

        _lastPopover = __instance;
        var fungleButton = __instance.buttons[4];
        var dleksButton = Object.Instantiate(fungleButton, fungleButton.transform.parent);
        dleksButton.name = "DleksButton";
        dleksButton.map = MapNames.Dleks;
        dleksButton.GetComponent<SpriteRenderer>().sprite = TextSmall.LoadAsset();
        dleksButton.OnPressEvent = fungleButton.OnPressEvent;
        dleksButton.transform.position = new Vector3(fungleButton.transform.position.x, __instance.buttons[0].transform.position.y + 0.7f, fungleButton.transform.position.z);
        __instance.buttons = new List<FreeplayPopoverButton>(__instance.buttons) { dleksButton }.ToArray();
    }

    // TOU-Mira: rewritten to fix issues on Epic Games.
    [HarmonyPatch(typeof(FreeplayPopover), nameof(FreeplayPopover.OnMapButtonPressed))]
    [HarmonyPrefix]
    public static bool FreeplayButtonPrefix(FreeplayPopover __instance, FreeplayPopoverButton button)
    {
        __instance.background.GetComponent<PassiveButton>().OnClick.RemoveListener((UnityAction)(() => __instance.Close()));
        foreach (var other in __instance.buttons)
        {
            other.Button.enabled = false;
        }

        AmongUsClient.Instance.TutorialMapId = (int)button.Map;
        __instance.hostGameButton.OnClick();
        return false;
    }

    /// <summary>
    ///     Start of the game by the host: the game loads The Skeld instead of dlekS outside of its events. Same code
    ///     without that check (TOU-Mira, from Galster: https://github.com/Tommy-XL/Unlock-dlekS-ehT).
    /// </summary>
    [HarmonyPatch]
    public static class CoStartGameHostPatch
    {
        public static MethodBase TargetMethod()
        {
            return Helpers.GetStateMachineMoveNext<AmongUsClient>(nameof(AmongUsClient.CoStartGameHost))!;
        }

        public static bool Prefix(Il2CppObjectBase __instance, ref bool __result)
        {
            var wrapper = new StateMachineWrapper<AmongUsClient>(__instance);
            if (wrapper.GetState() != 0) return true;

            var client = wrapper.Instance;
            wrapper.SetState(-1);
            if (LobbyBehaviour.Instance)
                LobbyBehaviour.Instance.Despawn();

            if (ShipStatus.Instance)
            {
                wrapper.SetRecentReturn(null!);
                wrapper.SetState(2);
                __result = true;
                return false;
            }

            var mapId = Mathf.Clamp(GameOptionsManager.Instance.CurrentGameOptions.MapId, 0, Constants.MapNames.Length - 1);
            client.ShipLoadingAsyncHandle = client.ShipPrefabs[mapId].InstantiateAsync();
            wrapper.SetRecentReturn(client.ShipLoadingAsyncHandle);
            wrapper.SetState(1);
            __result = true;
            return false;
        }
    }
}
