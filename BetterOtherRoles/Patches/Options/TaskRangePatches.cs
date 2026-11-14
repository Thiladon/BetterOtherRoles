using System.Collections;
using AmongUs.GameOptions;
using HarmonyLib;
using Reactor.Utilities;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.Options;

/// <summary>
///     Wider task ranges (BetterOtherRoles 1.7.1 CustomOptions, TOU-Mira 1.7.9 VanillaSettingsPatch): the maximum of the
///     common, short and long tasks in the settings is what the chosen map has. The tasks of a map are counted on its
///     prefab, loaded in the background once (then released), or when a game starts on it (maps built during the game:
///     LevelImpostor). When a game starts, the numbers are lowered to what the map has for the deal of the tasks, then
///     given back (the game crashes with more common tasks than the map has).
/// </summary>
[HarmonyPatch]
public static class TaskRangePatches
{
    // Maximum while the tasks of the map are not known (BOR 1.7.1).
    private static readonly TaskCounts Fallback = new(4, 23, 15);

    private static readonly Int32OptionNames[] TaskOptions =
        [Int32OptionNames.NumCommonTasks, Int32OptionNames.NumShortTasks, Int32OptionNames.NumLongTasks];

    // Tasks of each map (id), counted during this session.
    private static readonly Dictionary<int, TaskCounts> Counts = new();
    private static readonly HashSet<int> Loading = [];

    // Numbers of the host before they were lowered for the deal of the tasks.
    private static TaskCounts? _savedNumbers;

    private readonly record struct TaskCounts(int Common, int Short, int Long)
    {
        public int Get(Int32OptionNames option) => option switch
        {
            Int32OptionNames.NumCommonTasks => Common,
            Int32OptionNames.NumShortTasks => Short,
            _ => Long,
        };
    }

    [HarmonyPatch(typeof(GameOptionsMenu), nameof(GameOptionsMenu.Initialize))]
    [HarmonyPostfix]
    public static void MenuInitializePostfix() => Refresh(CurrentMapId());

    [HarmonyPatch(typeof(GameOptionsMapPicker), nameof(GameOptionsMapPicker.SelectMap), typeof(int))]
    [HarmonyPostfix]
    public static void SelectMapPostfix(int mapId) => Refresh(mapId);

    /// <summary>
    ///     Deal of the tasks (host): numbers lowered to what the map has, and its tasks remembered.
    /// </summary>
    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Begin))]
    [HarmonyPrefix]
    public static void ShipBeginPrefix(ShipStatus __instance)
    {
        var options = GameOptionsManager.Instance.CurrentGameOptions;
        var counts = CountTypes(__instance);
        Remember(options.MapId, counts);

        _savedNumbers = new TaskCounts(
            options.GetInt(Int32OptionNames.NumCommonTasks),
            options.GetInt(Int32OptionNames.NumShortTasks),
            options.GetInt(Int32OptionNames.NumLongTasks));
        foreach (var option in TaskOptions)
        {
            options.SetInt(option, Math.Min(_savedNumbers.Value.Get(option), counts.Get(option)));
        }
    }

    /// <summary>
    ///     Scan for every player who draws it: without visual tasks, the game takes it out of the pool once three
    ///     players have it, so the next ones would get a task twice (choice of the user). Its count of scans given is
    ///     set back to zero before each draw.
    /// </summary>
    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.AddTasksFromList))]
    [HarmonyPrefix]
    public static void AddTasksPrefix(ShipStatus __instance) => __instance.numScans = 0;

    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Begin))]
    [HarmonyFinalizer]
    public static void ShipBeginFinalizer()
    {
        if (_savedNumbers == null) return;

        var options = GameOptionsManager.Instance.CurrentGameOptions;
        foreach (var option in TaskOptions)
        {
            options.SetInt(option, _savedNumbers.Value.Get(option));
        }

        _savedNumbers = null;
    }

    private static int CurrentMapId() => GameOptionsManager.Instance.CurrentGameOptions.MapId;

    // Maximum known or not yet (counted in the background, then applied).
    private static void Refresh(int mapId)
    {
        ApplyRanges(mapId);
        if (!Counts.ContainsKey(mapId) && Loading.Add(mapId)) Coroutines.Start(CoCount(mapId));
    }

    // Tasks counted on the prefab of the map (not instantiated), released after.
    private static IEnumerator CoCount(int mapId)
    {
        var prefabs = AmongUsClient.Instance ? AmongUsClient.Instance.ShipPrefabs : null;
        if (prefabs == null || mapId < 0 || mapId >= prefabs.Count)
        {
            Loading.Remove(mapId);
            yield break;
        }

        var handle = Addressables.LoadAssetAsync<GameObject>(prefabs[mapId].RuntimeKey);
        while (!handle.IsDone) yield return null;

        try
        {
            var ship = handle.Result ? handle.Result.GetComponent<ShipStatus>() : null;
            if (ship) Remember(mapId, CountTypes(ship!));
        }
        finally
        {
            Addressables.Release(handle);
            Loading.Remove(mapId);
        }

        if (Counts.ContainsKey(mapId) && CurrentMapId() == mapId) ApplyRanges(mapId);
    }

    /// <summary>
    ///     Most tasks of each length a player can get without the same task twice (two downloads, two series of
    ///     wires...): the kinds of tasks, not the tasks (one download per room). The game gives a kind once to a player
    ///     (ShipStatus.AddTasksFromList), and gives them again once all are used. Long and short tasks share the kinds
    ///     given: a kind of both lists counts for the long ones only.
    /// </summary>
    private static TaskCounts CountTypes(ShipStatus ship)
    {
        var common = Kinds(ship.CommonTasks);
        var longKinds = Kinds(ship.LongTasks);
        var shortKinds = Kinds(ship.ShortTasks);
        shortKinds.ExceptWith(longKinds);

        return new TaskCounts(common.Count, shortKinds.Count, longKinds.Count);
    }

    private static HashSet<TaskTypes> Kinds(NormalPlayerTask[] tasks) =>
        tasks.Where(task => task).Select(task => task.TaskType).ToHashSet();

    // Maps whose prefab has no task (built during the game): not remembered, counted when a game starts on them.
    private static void Remember(int mapId, TaskCounts counts)
    {
        if (counts.Common + counts.Short + counts.Long > 0) Counts[mapId] = counts;
    }

    // Maximum of the three numbers in the open settings, and numbers above it lowered.
    private static void ApplyRanges(int mapId)
    {
        if (!Counts.TryGetValue(mapId, out var counts)) counts = Fallback;

        foreach (var option in Object.FindObjectsOfType<NumberOption>())
        {
            if (!option || !TaskOptions.Contains(option.intOptionName)) continue;

            var max = counts.Get(option.intOptionName);
            option.ValidRange = new FloatRange(0f, max);
            if (option.Value > max)
            {
                // As the "-" button of the game: value saved, then sent to the players.
                option.Value = max;
                option.UpdateValue();
                option.OnValueChanged.Invoke(option);
            }

            // "+" and "-" greyed out or not for the new maximum (the game only does it on a click).
            option.AdjustButtonsActiveState();
        }
    }
}
