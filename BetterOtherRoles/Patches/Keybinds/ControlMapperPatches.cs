using BetterOtherRoles.Keybinds;
using HarmonyLib;
using Rewired;
using Rewired.UI.ControlMapper;

namespace BetterOtherRoles.Patches.Keybinds;

/// <summary>
///     Remap Controls (Rewired ControlMapper): role keybind names in the mod language, key names in the keyboard layout
///     (<see cref="RemapKeyNamesBehaviour"/>), and no conflict window
///     when the key or button is only used by actions that can't be on screen together (<see cref="BorKeybindRules"/>).
/// </summary>
[HarmonyPatch(typeof(ControlMapper))]
public static class ControlMapperPatches
{
    [HarmonyPrefix]
    [HarmonyPatch("Open", typeof(bool))]
    public static void OpenPrefix(ControlMapper __instance)
    {
        BorKeybindRules.RefreshNames();
        BorKeybindRules.RefreshAdminKeybinds();
        if (__instance.GetComponent<RemapKeyNamesBehaviour>() == null)
        {
            __instance.gameObject.AddComponent<RemapKeyNamesBehaviour>();
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch("HasElementAssignmentConflicts")]
    public static void HasElementAssignmentConflictsPostfix(Player player, ControlMapper.InputMapping mapping, ElementAssignment assignment, ref bool __result)
    {
        if (!__result || player == null || mapping?.map == null)
        {
            return;
        }

        var actionId = mapping.fieldInfo.actionId;
        var others = FindActionsOnSameInput(player, mapping, assignment, actionId);

        // Only when every action found allows the sharing: otherwise (or if Rewired found the conflict elsewhere),
        // its conflict window stays.
        if (others.Count > 0 && others.All(other => !BorKeybindRules.Conflicts(actionId, other)))
        {
            __result = false;
        }
    }

    private static List<int> FindActionsOnSameInput(Player player, ControlMapper.InputMapping mapping, ElementAssignment assignment, int actionId)
    {
        var actions = new List<int>();
        var maps = new Il2CppSystem.Collections.Generic.List<ControllerMap>(
            player.controllers.maps.GetMaps(mapping.controllerType, mapping.controllerId)
                .Cast<Il2CppSystem.Collections.Generic.IEnumerable<ControllerMap>>()).ToArray();
        foreach (var map in maps)
        {
            if (map.categoryId != mapping.map.categoryId)
            {
                continue;
            }

            foreach (var elementMap in map.GetElementMaps())
            {
                if (elementMap.id == assignment.elementMapId || elementMap.actionId == actionId)
                {
                    continue;
                }

                var sameInput = mapping.controllerType == ControllerType.Keyboard
                    ? elementMap.keyCode == assignment.keyboardKey && elementMap.modifierKeyFlags == assignment.modifierKeyFlags
                    : elementMap.elementIdentifierId == assignment.elementIdentifierId;
                if (sameInput && !actions.Contains(elementMap.actionId))
                {
                    actions.Add(elementMap.actionId);
                }
            }
        }

        return actions;
    }
}
