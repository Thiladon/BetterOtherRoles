using BetterOtherRoles.Options.Maps;
using HarmonyLib;
using MiraAPI.GameOptions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.BetterMaps;

/// <summary>
///     Better Polus (BetterOtherRoles 1.7.1 Modules/BetterPolus.cs, TOU-Mira 1.7.9 BetterPolusPatches), with
///     <see cref="BetterPolusOptions"/>: vents, vitals, tasks and decorations changed when the map starts, the same on
///     every client (the options are shared); the countdown of the reactor sabotage set by the host.
///     The map is a new object at each game: nothing to undo.
/// </summary>
[HarmonyPatch]
public static class BetterPolusPatches
{
    // Places from BOR 1.7.1 (TOU-Mira for the bathroom vent).
    private static readonly Vector3 VitalsPosition = new(31.275f, -6.45f, 1f);
    private static readonly Vector3 VitalsScreenPosition = new(26.635f, -15.92f, 1f);
    private const float VitalsScreenScale = 0.75f;
    private static readonly Vector3 WifiPosition = new(15.975f, 0.084f, 0.001f);
    private static readonly Vector3 ChartCoursePosition = new(11.07f, -15.298f, -0.015f);
    private static readonly Vector3 ColdTemperaturePosition = new(7.772f, -17.103f, -0.017f);
    private static readonly Vector3 BathroomVentPosition = new(34.063f, -10.226f, 2f);

    // Rooms shown in the task list for the moved tasks (the consoles keep their room: the game finds them with it).
    private static bool _wifiAndChartSwapped;
    private static bool _coldTemperatureMoved;

    /// <summary>
    ///     Changes Polus once all its objects are awake (the decorations of the date are chosen in their Awake).
    /// </summary>
    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
    [HarmonyPostfix]
    public static void ShipStartPostfix(ShipStatus __instance)
    {
        _wifiAndChartSwapped = false;
        _coldTemperatureMoved = false;
        if (__instance.Type != ShipStatus.MapType.Pb || !BetterPolusOptions.IsOn()) return;

        var options = OptionGroupSingleton<BetterPolusOptions>.Instance;
        TryApply("vents", () => AdjustVents(__instance, options));
        if (options.VitalsInLaboratory.Value) TryApply("vitals", () => MoveVitals(__instance));
        if (options.VitalsInLaboratory.Value || options.ColdTemperatureOutside.Value) TryApply("cold temperature", () => MoveColdTemperature(__instance));
        if (options.SwapWifiAndChartCourse.Value) TryApply("wifi and chart course", () => SwapWifiAndChartCourse(__instance));
        TryApply("theme", () => SetTheme(__instance, (PolusTheme)options.Theme.Value));
    }

    /// <summary>
    ///     Task list: the moved tasks show the room where they now are (Dropship, Comms, Outside).
    /// </summary>
    [HarmonyPatch(typeof(NormalPlayerTask), nameof(NormalPlayerTask.AppendTaskText))]
    [HarmonyPrefix]
    public static void AppendTaskTextPrefix(NormalPlayerTask __instance, out SystemTypes? __state)
    {
        __state = null;
        var room = ShownRoom(__instance);
        if (room == null) return;

        __state = __instance.StartAt;
        __instance.StartAt = room.Value;
    }

    [HarmonyPatch(typeof(NormalPlayerTask), nameof(NormalPlayerTask.AppendTaskText))]
    [HarmonyPostfix]
    public static void AppendTaskTextPostfix(NormalPlayerTask __instance, SystemTypes? __state)
    {
        if (__state != null) __instance.StartAt = __state.Value;
    }

    /// <summary>
    ///     Countdown of the reactor sabotage (laboratory): set when it starts (run by the host, then sent to everyone).
    /// </summary>
    [HarmonyPatch(typeof(ReactorSystemType), nameof(ReactorSystemType.UpdateSystem))]
    [HarmonyPrefix]
    public static void ReactorUpdatePrefix(ReactorSystemType __instance, out bool __state)
    {
        __state = __instance.IsActive;
    }

    [HarmonyPatch(typeof(ReactorSystemType), nameof(ReactorSystemType.UpdateSystem))]
    [HarmonyPostfix]
    public static void ReactorUpdatePostfix(ReactorSystemType __instance, bool __state)
    {
        if (__state || !__instance.IsActive) return;
        if (!ShipStatus.Instance || ShipStatus.Instance.Type != ShipStatus.MapType.Pb || !BetterPolusOptions.IsOn()) return;

        __instance.Countdown = OptionGroupSingleton<BetterPolusOptions>.Instance.ReactorSabotage.Value;
    }

    private static SystemTypes? ShownRoom(NormalPlayerTask task)
    {
        if (_wifiAndChartSwapped && task.TaskType == TaskTypes.RebootWifi) return SystemTypes.Dropship;
        if (_wifiAndChartSwapped && task.TaskType == TaskTypes.ChartCourse) return SystemTypes.Comms;
        if (_coldTemperatureMoved && task.TaskType == TaskTypes.RecordTemperature && task.StartAt == SystemTypes.Laboratory)
            return SystemTypes.Outside;

        return null;
    }

    // One change failing (object renamed by an update of the game) does not stop the others.
    private static void TryApply(string name, Action change)
    {
        try
        {
            change();
        }
        catch (Exception e)
        {
            Error($"Better Polus: could not change the {name}: {e}");
        }
    }

    private static void AdjustVents(ShipStatus ship, BetterPolusOptions options)
    {
        if (options.VentNetwork.Value)
        {
            // Electrical ↔ the building outside it, Science building ↔ Storage.
            var electricBuilding = FindVent(ship, "ElectricBuildingVent");
            var electrical = FindVent(ship, "ElectricalVent");
            electricBuilding.Left = electrical;
            electrical.Center = electricBuilding;

            var scienceBuilding = FindVent(ship, "ScienceBuildingVent");
            var storage = FindVent(ship, "StorageVent");
            scienceBuilding.Left = storage;
            storage.Center = scienceBuilding;
        }

        if (options.BathroomVent.Value) FindVent(ship, "BathroomVent").transform.position = BathroomVentPosition;
    }

    // Vitals in the laboratory, with a copy of the screen of the admin room beside them.
    private static void MoveVitals(ShipStatus ship)
    {
        var science = FindObject(ship, "Science").transform;
        var vitals = ship.GetComponentsInChildren<SystemConsole>(true).First(console => console.name == "panel_vitals").transform;
        vitals.SetParent(science, true);
        vitals.position = VitalsPosition;

        // Child of the map: destroyed with it after the game.
        var screen = Object.Instantiate(FindObject(ship, "dvdscreen"), science).transform;
        screen.position = VitalsScreenPosition;
        screen.localScale = new Vector3(VitalsScreenScale, screen.localScale.y, screen.localScale.z);
    }

    // Cold temperature outside, in the valley below the laboratory: a solid console, as the walls outside.
    private static void MoveColdTemperature(ShipStatus ship)
    {
        var console = FindConsole(ship, "panel_tempcold");
        console.transform.SetParent(FindObject(ship, "Outside").transform, true);
        console.transform.position = ColdTemperaturePosition;

        var collider = console.GetComponent<BoxCollider2D>();
        collider.isTrigger = false;
        collider.size += new Vector2(0f, -0.3f);
        _coldTemperatureMoved = true;
    }

    // Reboot Wifi in the dropship, Chart Course in Comms.
    private static void SwapWifiAndChartCourse(ShipStatus ship)
    {
        var wifi = FindConsole(ship, "panel_wifi");
        wifi.transform.SetParent(FindObject(ship, "Dropship").transform, true);
        wifi.transform.position = WifiPosition;

        var chartCourse = FindConsole(ship, "panel_nav");
        chartCourse.transform.SetParent(FindObject(ship, "Comms").transform, true);
        chartCourse.transform.position = ChartCoursePosition;
        chartCourse.checkWalls = true;
        _wifiAndChartSwapped = true;
    }

    private static void SetTheme(ShipStatus ship, PolusTheme theme)
    {
        if (theme == PolusTheme.Auto) return;

        FindObject(ship, "HalloweenDecorPolus").SetActive(theme == PolusTheme.Halloween);
    }

    private static Vent FindVent(ShipStatus ship, string name) => ship.AllVents.First(vent => vent.name == name);

    private static Console FindConsole(ShipStatus ship, string name) =>
        ship.GetComponentsInChildren<Console>(true).First(console => console.name == name);

    // Inactive objects included (decorations of another date).
    private static GameObject FindObject(ShipStatus ship, string name) =>
        ship.GetComponentsInChildren<Transform>(true).First(child => child.name == name).gameObject;
}
