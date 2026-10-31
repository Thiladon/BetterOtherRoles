using BepInEx.Configuration;
using BetterOtherRoles.Modules.Presets;
using System.Reflection;
using HarmonyLib;
using MiraAPI.PluginLoading;
using MiraAPI.Presets;

namespace BetterOtherRoles.Patches.Options;

/// <summary>
///     Presets of MiraAPI, fixed and completed (<see cref="BorPresets"/>):
///     - loading a preset applies the option values (SetValue without RPC) but never wrote them to the plugin config:
///       after a restart, the options were back to their values before the preset. Every option value is written to the
///       plugin config (the role counts and chances already are), then the vanilla settings of the preset are applied,
///       and the preset becomes the active one;
///     - MiraAPI writes every preset file again each time the settings menu opens (PresetManager.LoadPresets): the files
///       are only written when saved. The values missing in a file still get their default value when loaded;
///     - the presets are sorted ("Default" first, then by name), and the buttons of the presets read again are taken out
///       of the grid at once (destroyed at the end of the frame only, they left holes in it after "Refresh").
/// </summary>
[HarmonyPatch]
public static class PresetLoadSavePatches
{
    private static readonly PropertyInfo InternalPresetsProperty = AccessTools.Property(typeof(MiraPluginInfo), "InternalPresets");
    private static readonly PropertyInfo PresetsProperty = AccessTools.Property(typeof(MiraPluginInfo), nameof(MiraPluginInfo.Presets));

    private static bool _readingPresets;

    [HarmonyPatch(typeof(OptionPreset), nameof(OptionPreset.LoadPreset))]
    [HarmonyPostfix]
    public static void LoadPresetPostfix(OptionPreset __instance)
    {
        var config = __instance.PluginConfig;
        foreach (var option in __instance.Plugin.Options)
        {
            var definition = option.ConfigDefinition;
            if (definition == null || !config.ContainsKey(definition))
            {
                continue;
            }

            var value = AccessTools.Property(option.GetType(), "Value")?.GetValue(option);
            if (value != null)
            {
                config[definition].BoxedValue = value;
            }
        }

        config.Save();

        if (__instance.Plugin.PluginId == BetterOtherRolesPlugin.Id)
        {
            BorPresets.ApplyVanilla(__instance.PresetConfig);
            if (__instance.Name.Length > 0)
            {
                BorPresets.SetActive(__instance.Name);
            }
        }
    }

    [HarmonyPatch(typeof(PresetManager), nameof(PresetManager.LoadPresets))]
    [HarmonyPrefix]
    public static void LoadPresetsPrefix(MiraPluginInfo plugin)
    {
        _readingPresets = true;
        foreach (var preset in plugin.Presets)
        {
            if (preset.PresetButton)
            {
                preset.PresetButton!.SetActive(false);
                preset.PresetButton.transform.SetParent(null, false);
            }
        }
    }

    [HarmonyPatch(typeof(PresetManager), nameof(PresetManager.LoadPresets))]
    [HarmonyPostfix]
    public static void LoadPresetsPostfix(MiraPluginInfo plugin)
    {
        if (InternalPresetsProperty.GetValue(plugin) is not List<OptionPreset> presets)
        {
            return;
        }

        presets.Sort((a, b) => a.Name == BorPresets.DefaultName ? (b.Name == BorPresets.DefaultName ? 0 : -1)
            : b.Name == BorPresets.DefaultName ? 1 : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        PresetsProperty.SetValue(plugin, presets.ToArray());
    }

    [HarmonyPatch(typeof(PresetManager), nameof(PresetManager.LoadPresets))]
    [HarmonyFinalizer]
    public static void LoadPresetsFinalizer()
    {
        _readingPresets = false;
    }

    // While MiraAPI reads the preset files: not written again.
    [HarmonyPatch(typeof(ConfigFile), nameof(ConfigFile.Save))]
    [HarmonyPrefix]
    public static bool ConfigSavePrefix(ConfigFile __instance)
    {
        return !_readingPresets || !__instance.ConfigFilePath.StartsWith(PresetManager.PresetDirectory, StringComparison.OrdinalIgnoreCase);
    }
}
