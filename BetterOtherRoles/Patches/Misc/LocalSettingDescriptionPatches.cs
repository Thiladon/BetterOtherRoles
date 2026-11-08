using System.Reflection;
using BetterOtherRoles.LocalSettings;
using HarmonyLib;
using MiraAPI.LocalSettings.SettingTypes;
using MiraAPI.Translation;
using TMPro;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     MiraAPI shows the description of a local setting on hover as it is, untranslated (LocalToggleSetting,
///     LocalEnumSetting).
///     Our descriptions are locale keys: they are translated when read, in the mod language.
/// </summary>
[HarmonyPatch]
public static class LocalSettingDescriptionPatches
{
    private const string KeyPrefix = "BetterOtherRoles.";

    // The value type settings each have their own getter (bool for the toggles, int for the lists).
    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(LocalSettingBase<bool>), nameof(LocalSettingBase<bool>.Description));
        yield return AccessTools.PropertyGetter(typeof(LocalSettingBase<int>), nameof(LocalSettingBase<int>.Description));
    }

    public static void Postfix(ref string __result)
    {
        if (__result != null && __result.StartsWith(KeyPrefix, StringComparison.Ordinal))
            __result = MiraLocaleManager.Get(__result, __result);
    }
}

/// <summary>
///     The description replaces the toggle (or list) text on hover, in the same narrow box: a long one overflowed the
///     button. For every toggle and list of our tab, on hover: the text box takes the inner width of the button background, then the
///     font gets smaller step by step until the text fits the height. Both are given back on mouse out.
/// </summary>
[HarmonyPatch]
public static class LocalToggleTextFitPatches
{
    // Part of the background width used by the text, smallest font (part of the normal one) and font step.
    private const float InnerWidthRatio = 0.9f;
    private const float MinFontRatio = 0.5f;
    private const float FontStep = 0.05f;

    [HarmonyPatch(typeof(LocalToggleSetting), nameof(LocalToggleSetting.CreateOption))]
    [HarmonyPostfix]
    public static void CreateOptionPostfix(LocalToggleSetting __instance, GameObject __result)
    {
        var toggle = __result.GetComponent<ToggleButtonBehaviour>();
        if (__instance.Tab is BorLocalSettingsTab && toggle != null)
            AddFit(__result, toggle.Background);
    }

    // The list button is a copy of a toggle whose behaviour MiraAPI destroys: same background.
    [HarmonyPatch(typeof(LocalEnumSetting), nameof(LocalEnumSetting.CreateOption))]
    [HarmonyPostfix]
    public static void CreateEnumOptionPostfix(LocalEnumSetting __instance, GameObject __result)
    {
        var toggle = __result.GetComponent<ToggleButtonBehaviour>();
        if (__instance.Tab is BorLocalSettingsTab && toggle != null)
            AddFit(__result, toggle.Background);
    }

    private static void AddFit(GameObject option, SpriteRenderer? background)
    {
        var text = option.transform.Find("Text_TMP")?.GetComponent<TextMeshPro>();
        var button = option.GetComponent<PassiveButton>();
        if (text == null || button == null) return;

        var size = text.rectTransform.sizeDelta;
        var fontSize = text.fontSize;
        var autoSizing = text.enableAutoSizing;

        // Added after the MiraAPI listeners: the description is already in the text on hover.
        button.OnMouseOver.AddListener((Action)(() => Fit(background, text, size, fontSize)));
        button.OnMouseOut.AddListener((Action)(() =>
        {
            text.rectTransform.sizeDelta = size;
            text.enableAutoSizing = autoSizing;
            text.fontSize = fontSize;
        }));
    }

    private static void Fit(SpriteRenderer? background, TextMeshPro text, Vector2 size, float fontSize)
    {
        var width = size.x;
        if (background && text.transform.lossyScale.x > 0f)
            width = Mathf.Max(width, background!.bounds.size.x / text.transform.lossyScale.x * InnerWidthRatio);

        text.rectTransform.sizeDelta = new Vector2(width, size.y);
        text.enableAutoSizing = false;
        text.fontSize = fontSize;
        while (text.fontSize > fontSize * MinFontRatio && text.GetPreferredValues(text.text, width, 0f).y > size.y)
        {
            text.fontSize -= fontSize * FontStep;
        }
    }
}
