using BetterOtherRoles.Options.Maps;
using HarmonyLib;
using MiraAPI.GameOptions;
using UnityEngine;

namespace BetterOtherRoles.Patches.BetterMaps;

/// <summary>
///     Durations of the sabotages (<see cref="SabotageOptions"/>): the host sets the countdown when a sabotage starts
///     (then sent to everyone by the game).
/// </summary>
[HarmonyPatch]
public static class SabotageDurationPatches
{
    // Countdown of the crash of the Airship set by the game (HeliSabotageSystem.UpdateSystem).
    private const float GameCrashCountdown = 90f;

    private static SabotageOptions Options => OptionGroupSingleton<SabotageOptions>.Instance;

    // Reactor (Skeld, dlekS, MIRA HQ, Polus, The Fungle): run by the host when a player sabotages or repairs.
    [HarmonyPatch(typeof(ReactorSystemType), nameof(ReactorSystemType.UpdateSystem))]
    [HarmonyPrefix]
    public static void ReactorUpdatePrefix(ReactorSystemType __instance, out bool __state) => __state = __instance.IsActive;

    [HarmonyPatch(typeof(ReactorSystemType), nameof(ReactorSystemType.UpdateSystem))]
    [HarmonyPostfix]
    public static void ReactorUpdatePostfix(ReactorSystemType __instance, bool __state)
    {
        if (__state || !__instance.IsActive || Options.Reactor(CurrentMap()) is not { } duration) return;

        __instance.Countdown = duration;
    }

    // Oxygen (Skeld, dlekS, MIRA HQ).
    [HarmonyPatch(typeof(LifeSuppSystemType), nameof(LifeSuppSystemType.UpdateSystem))]
    [HarmonyPrefix]
    public static void OxygenUpdatePrefix(LifeSuppSystemType __instance, out bool __state) => __state = __instance.IsActive;

    [HarmonyPatch(typeof(LifeSuppSystemType), nameof(LifeSuppSystemType.UpdateSystem))]
    [HarmonyPostfix]
    public static void OxygenUpdatePostfix(LifeSuppSystemType __instance, bool __state)
    {
        if (__state || !__instance.IsActive || Options.Oxygen(CurrentMap()) is not { } duration) return;

        __instance.Countdown = duration;
    }

    // Crash of the Airship: the game sets 90 s each time it is sabotaged (even while it runs).
    [HarmonyPatch(typeof(HeliSabotageSystem), nameof(HeliSabotageSystem.UpdateSystem))]
    [HarmonyPrefix]
    public static void CrashUpdatePrefix(HeliSabotageSystem __instance, out float __state) => __state = __instance.Countdown;

    [HarmonyPatch(typeof(HeliSabotageSystem), nameof(HeliSabotageSystem.UpdateSystem))]
    [HarmonyPostfix]
    public static void CrashUpdatePostfix(HeliSabotageSystem __instance, float __state)
    {
        if (Mathf.Approximately(__instance.Countdown, __state) || !Mathf.Approximately(__instance.Countdown, GameCrashCountdown)) return;

        __instance.Countdown = Options.AirshipCrash.Value;
    }

    // Mushroom mixup of The Fungle: time until everyone gets their look back.
    [HarmonyPatch(typeof(MushroomMixupSabotageSystem), nameof(MushroomMixupSabotageSystem.UpdateSystem))]
    [HarmonyPrefix]
    public static void MixupUpdatePrefix(MushroomMixupSabotageSystem __instance, out bool __state) => __state = __instance.IsActive;

    [HarmonyPatch(typeof(MushroomMixupSabotageSystem), nameof(MushroomMixupSabotageSystem.UpdateSystem))]
    [HarmonyPostfix]
    public static void MixupUpdatePostfix(MushroomMixupSabotageSystem __instance, bool __state)
    {
        if (__state || !__instance.IsActive) return;

        __instance.currentSecondsUntilHeal = Options.FungleMixup.Value;
    }

    private static MapNames CurrentMap() => (MapNames)GameOptionsManager.Instance.CurrentGameOptions.MapId;
}
