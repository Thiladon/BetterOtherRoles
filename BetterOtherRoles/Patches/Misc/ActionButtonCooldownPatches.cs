using BetterOtherRoles.Modules.Cooldowns;
using HarmonyLib;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Keeps the cooldown shadow of vanilla action buttons (kill button, ...) in sync with their timer.
///     The game draws it relative to the maximum cooldown: a 10 s start cooldown on a 30 s kill cooldown
///     only covers a third of the button. Each button now remembers the value its countdown started from.
///     BetterOtherRoles buttons handle this themselves (<see cref="Buttons.BorTargetButton{T}"/>).
/// </summary>
[HarmonyPatch(typeof(ActionButton))]
public static class ActionButtonCooldownPatches
{
    private static readonly Dictionary<IntPtr, (float Last, float Start)> Timers = [];

    /// <summary>
    ///     Forgets the buttons of the previous game.
    /// </summary>
    public static void Clear()
    {
        Timers.Clear();
    }

    // Vanilla buttons (kill button, ...): SetCoolDown(timer, maxTimer).
    [HarmonyPrefix]
    [HarmonyPatch(nameof(ActionButton.SetCoolDown))]
    public static void SetCoolDownPrefix(ActionButton __instance, float __0, ref float __1)
    {
        var start = Track(Timers, __instance.Pointer, __0);
        if (start > 0f)
            __1 = start;
    }

    // Tenths of a second with the local setting (BorCooldownText).
    [HarmonyPostfix]
    [HarmonyPatch(nameof(ActionButton.SetCoolDown))]
    public static void SetCoolDownPostfix(ActionButton __instance, float __0)
    {
        BorCooldownText.Apply(__instance, __0);
    }

    // A value going up means a new countdown: it becomes the start value.
    private static float Track(Dictionary<IntPtr, (float Last, float Start)> values, IntPtr button, float value)
    {
        var (last, start) = values.TryGetValue(button, out var known) ? known : (0f, value);
        if (value > last)
            start = value;

        values[button] = (value, start);
        return start;
    }
}
