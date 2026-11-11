using BetterOtherRoles.Keybinds;
using BetterOtherRoles.Modules.Debug;
using BetterOtherRoles.Modules.Display;
using HarmonyLib;
using MiraAPI.Keybinds;
using Rewired;

namespace BetterOtherRoles.Patches.Keybinds;

/// <summary>
///     Adapts MiraAPI's keybinds to BetterOtherRoles: removes MiraAPI's generic ability keybinds (each role has its own),
///     and applies <see cref="BorKeybindRules"/> to the conflicts shown in red.
/// </summary>
[HarmonyPatch(typeof(KeybindManager))]
public static class KeybindManagerPatches
{
    [HarmonyPrefix]
    [HarmonyPatch("RewiredInit")]
    public static void RewiredInitPrefix()
    {
        MiraKeybind[] generic =
        [
            MiraGlobalKeybinds.PrimaryAbility,
            MiraGlobalKeybinds.SecondaryAbility,
            MiraGlobalKeybinds.TertiaryAbility,
            MiraGlobalKeybinds.ModifierPrimaryAbility,
            MiraGlobalKeybinds.ModifierSecondaryAbility,
            MiraGlobalKeybinds.ModifierTertiaryAbility,
        ];
        KeybindManager.Keybinds.RemoveAll(generic.Contains);
    }

    [HarmonyPostfix]
    [HarmonyPatch("RewiredInit")]
    public static void RewiredInitPostfix()
    {
        BorKeybindRules.RefreshAdminKeybinds();
        BorKeybinds.DebugMenu.OnActivate(BorDebugMenu.Toggle);
        BorKeybinds.ZoomOut.OnActivate(() => BorZoom.Step(true));
        BorKeybinds.ZoomIn.OnActivate(() => BorZoom.Step(false));
    }

    // The role names are written when Remap Controls opens (ControlMapperPatches), not here: Rewired starts
    // before the RoleManager (see BorKeybindRules.RefreshNames).

    [HarmonyPostfix]
    [HarmonyPatch(nameof(KeybindManager.GetConflicts))]
    public static void GetConflictsPostfix(Dictionary<KeyboardKeyCode, List<MiraKeybind>> __result)
    {
        foreach (var key in __result.Keys.ToList())
        {
            var keybinds = __result[key];
            var conflicting = keybinds.Where(keybind => keybinds.Any(other => BorKeybindRules.Conflicts(keybind, other))).ToList();
            if (conflicting.Count > 0)
            {
                __result[key] = conflicting;
            }
            else
            {
                __result.Remove(key);
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(KeybindManager.GetControllerConflicts))]
    public static void GetControllerConflictsPostfix(List<ActionElementMap> __result)
    {
        var conflicting = __result
            .Where(map => __result.Any(other =>
                other.controllerMap.id == map.controllerMap.id &&
                map.CheckForAssignmentConflict(other) &&
                BorKeybindRules.Conflicts(map.actionId, other.actionId)))
            .ToList();
        __result.Clear();
        __result.AddRange(conflicting);
    }
}
