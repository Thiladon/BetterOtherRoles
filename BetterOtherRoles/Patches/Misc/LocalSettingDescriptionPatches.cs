using System.Reflection;
using BetterOtherRoles.LocalSettings;
using HarmonyLib;
using MiraAPI.LocalSettings.SettingTypes;
using MiraAPI.Translation;
using TMPro;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     MiraAPI shows the description of a local setting on hover as it is, untranslated (LocalToggleSetting).
///     Our descriptions are locale keys: they are translated when read, in the mod language.
/// </summary>
[HarmonyPatch]
public static class LocalSettingDescriptionPatches
{
    private const string KeyPrefix = "BetterOtherRoles.";

    // The value type settings each have their own getter (bool for the toggles).
    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(LocalSettingBase<bool>), nameof(LocalSettingBase<bool>.Description));
    }

    public static void Postfix(ref string __result)
    {
        if (__result != null && __result.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            __result = MiraLocaleManager.Get(__result, __result);
        }
    }
}

/// <summary>
///     The description replaces the toggle text on hover, in the same narrow box: a long one overflowed the button.
///     For every toggle of our tab, on hover: the text box takes the inner width of the button background, then the
///     font gets smaller step by step until the text fits the height. Both are given back on mouse out.
/// </summary>
[HarmonyPatch(typeof(LocalToggleSetting), nameof(LocalToggleSetting.CreateOption))]
public static class LocalToggleTextFitPatches
{
    // Part of the background width used by the text, smallest font (part of the normal one) and font step.
    private const float InnerWidthRatio = 0.9f;
    private const float MinFontRatio = 0.5f;
    private const float FontStep = 0.05f;

    [HarmonyPostfix]
    public static void CreateOptionPostfix(LocalToggleSetting __instance, GameObject __result)
    {
        var toggle = __result.GetComponent<ToggleButtonBehaviour>();
        var text = __result.transform.Find("Text_TMP")?.GetComponent<TextMeshPro>();
        var button = __result.GetComponent<PassiveButton>();
        if (__instance.Tab is not BorLocalSettingsTab || toggle == null || text == null || button == null)
        {
            return;
        }

        var size = text.rectTransform.sizeDelta;
        var fontSize = text.fontSize;
        var autoSizing = text.enableAutoSizing;

        // Added after the MiraAPI listeners: the description is already in the text on hover.
        button.OnMouseOver.AddListener((Action)(() => Fit(toggle, text, size, fontSize)));
        button.OnMouseOut.AddListener((Action)(() =>
        {
            text.rectTransform.sizeDelta = size;
            text.enableAutoSizing = autoSizing;
            text.fontSize = fontSize;
        }));
    }

    private static void Fit(ToggleButtonBehaviour toggle, TextMeshPro text, Vector2 size, float fontSize)
    {
        var width = size.x;
        if (toggle.Background && text.transform.lossyScale.x > 0f)
        {
            width = Mathf.Max(width, toggle.Background.bounds.size.x / text.transform.lossyScale.x * InnerWidthRatio);
        }

        text.rectTransform.sizeDelta = new Vector2(width, size.y);
        text.enableAutoSizing = false;
        text.fontSize = fontSize;
        while (text.fontSize > fontSize * MinFontRatio && text.GetPreferredValues(text.text, width, 0f).y > size.y)
        {
            text.fontSize -= fontSize * FontStep;
        }
    }
}
