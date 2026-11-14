using AmongUs.GameOptions;
using HarmonyLib;
using MiraAPI.Translation;

namespace BetterOtherRoles.Patches.Options;

/// <summary>
///     "Very Short" kill distance (BetterOtherRoles 1.7.1 AddToKillDistanceSetting), added to the setting of the game:
///     at the end of its distances (number 3, 0.5), so Short, Medium and Long keep their numbers (BOR put it first and
///     shifted them, changing the saved settings). The menu still shows Very Short, Short, Medium, Long in this order.
/// </summary>
[HarmonyPatch]
public static class KillDistancePatches
{
    private const int VeryShort = 3;
    private const float VeryShortDistance = 0.5f;

    // Short, Medium, Long of the game, then Very Short.
    private static readonly float[] Distances = [1f, 1.8f, 2.5f, VeryShortDistance];

    // Numbers in the order of the menu.
    private static readonly int[] MenuOrder = [VeryShort, 0, 1, 2];

    private static StringNames VeryShortName => MiraLocaleManager.GetOrCreateLocaleString("BetterOtherRoles.Setting.KillDistanceVeryShort");

    /// <summary>
    ///     Distances of the game replaced once (the field is read-only in C#, not in IL2CPP).
    /// </summary>
    public static void Apply()
    {
        NormalGameOptionsV12.KillDistances = Distances;
    }

    /// <summary>
    ///     Settings with Very Short not refused (the game only accepts 0 to 2).
    /// </summary>
    [HarmonyPatch(typeof(NormalGameOptionsV12), nameof(NormalGameOptionsV12.AreInvalid))]
    [HarmonyPostfix]
    public static void AreInvalidPostfix(NormalGameOptionsV12 __instance, int maxExpectedPlayers, ref bool __result)
    {
        if (!__result || __instance.KillDistance != VeryShort) return;

        __instance.KillDistance = 0;
        __result = __instance.AreInvalid(maxExpectedPlayers);
        __instance.KillDistance = VeryShort;
    }

    /// <summary>
    ///     Value shown for the setting (lobby settings, notifications of changes).
    /// </summary>
    [HarmonyPatch(typeof(StringGameSetting), nameof(StringGameSetting.GetValueString))]
    [HarmonyPrefix]
    public static bool GetValueStringPrefix(StringGameSetting __instance, float value, ref string __result)
    {
        if (__instance.OptionName != Int32OptionNames.KillDistance || (int)value != VeryShort) return true;

        __result = TranslationController.Instance.GetString(VeryShortName);
        return false;
    }

    /// <summary>
    ///     Summary of the settings (the game writes the text 204 + number: Short, Medium, Long).
    /// </summary>
    [HarmonyPatch(typeof(IGameOptionsExtensions), nameof(IGameOptionsExtensions.AppendItem),
        typeof(Il2CppSystem.Text.StringBuilder), typeof(StringNames), typeof(string))]
    [HarmonyPrefix]
    public static void AppendItemPrefix(StringNames stringName, ref string value)
    {
        if (stringName != StringNames.GameKillDistance) return;
        if (GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.KillDistance) != VeryShort) return;

        value = TranslationController.Instance.GetString(VeryShortName);
    }

    /// <summary>
    ///     Menu: Very Short added to the values.
    /// </summary>
    [HarmonyPatch(typeof(StringOption), nameof(StringOption.Initialize))]
    [HarmonyPrefix]
    public static void InitializePrefix(StringOption __instance)
    {
        if (!IsKillDistance(__instance) || __instance.Values.Length > VeryShort) return;

        __instance.Values = new[] { __instance.Values[0], __instance.Values[1], __instance.Values[2], VeryShortName };
    }

    [HarmonyPatch(typeof(StringOption), nameof(StringOption.Increase))]
    [HarmonyPrefix]
    public static bool IncreasePrefix(StringOption __instance) => !IsKillDistance(__instance) || Move(__instance, 1);

    [HarmonyPatch(typeof(StringOption), nameof(StringOption.Decrease))]
    [HarmonyPrefix]
    public static bool DecreasePrefix(StringOption __instance) => !IsKillDistance(__instance) || Move(__instance, -1);

    [HarmonyPatch(typeof(StringOption), nameof(StringOption.AdjustButtonsActiveState))]
    [HarmonyPrefix]
    public static bool AdjustButtonsPrefix(StringOption __instance)
    {
        if (!IsKillDistance(__instance)) return true;

        var place = Array.IndexOf(MenuOrder, __instance.Value);
        __instance.MinusBtn.SetInteractable(place > 0);
        __instance.PlusBtn.SetInteractable(place < MenuOrder.Length - 1);
        return false;
    }

    private static bool IsKillDistance(StringOption option) => option.Title == StringNames.GameKillDistance;

    // As the "+" and "-" buttons of the game, in the order of the menu. Always false: the game's method skipped.
    private static bool Move(StringOption option, int step)
    {
        var place = Math.Clamp(Array.IndexOf(MenuOrder, option.Value) + step, 0, MenuOrder.Length - 1);
        option.Value = MenuOrder[place];
        option.UpdateValue();
        option.OnValueChanged.Invoke(option);
        option.AdjustButtonsActiveState();
        return false;
    }
}
