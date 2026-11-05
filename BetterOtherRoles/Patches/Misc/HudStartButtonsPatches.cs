using HarmonyLib;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Fix of a random failure of MiraAPI 0.6.0 when the HUD starts (HudManagerPatches.StartPostfix):
///     for each vanilla button, it reads <c>key.GetComponent&lt;ActionButton&gt;().GetType()</c> and looks the type up
///     (KillButton, UseButton...) in its vanilla keybinds. Il2CppInterop only keeps its C# objects by weak reference
///     (Il2CppObjectPool): if the KillButton object made just before was already collected by the GC, it builds an
///     ActionButton one, and the lookup fails ("The given key 'ActionButton' was not present in the dictionary").
///     The end of the postfix is then skipped: no button scale (MiraAPI ButtonScaleFactor), buttons left too big, and no
///     keybind icons. Strong references to the vanilla buttons, with their own types, are held during the postfix.
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Start))]
public static class HudStartButtonsPatches
{
    private static ActionButton[]? _keptButtons;

    // Before the MiraAPI postfix (normal priority).
    [HarmonyPostfix]
    [HarmonyPriority(Priority.First)]
    public static void KeepButtonsPostfix(HudManager __instance)
    {
        _keptButtons =
        [
            __instance.KillButton, __instance.UseButton, __instance.ReportButton, __instance.ImpostorVentButton,
            __instance.SabotageButton, __instance.AbilityButton,
        ];
    }

    // After the MiraAPI postfix.
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    public static void ReleaseButtonsPostfix()
    {
        _keptButtons = null;
    }
}
