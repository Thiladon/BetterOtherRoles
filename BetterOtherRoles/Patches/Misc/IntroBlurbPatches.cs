using System.Collections;
using HarmonyLib;
using Reactor.Utilities;
using TMPro;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     The description of the role in the intro: a long one went out of the screen on both sides. It now takes the
///     middle two thirds of the screen (from 1/6 to 5/6 of the width) and goes to the next line when it is longer;
///     beyond two lines, the font gets smaller until it holds on two lines (down to half its size).
///     The width is set before the intro; the size when the text shows, and again when something changes it.
/// </summary>
[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin))]
public static class IntroBlurbPatches
{
    private const float WidthOfScreen = 2f / 3f;
    private const int MaxLines = 2;
    private const float MinSize = 0.5f;
    private const float Step = 0.05f;

    [HarmonyPrefix]
    public static void CoBeginPrefix(IntroCutscene __instance)
    {
        var blurb = __instance.RoleBlurbText;
        var camera = Camera.main;
        if (!blurb || !camera) return;

        // Width of the screen in world units, then in units of the text (its scale).
        var screenWidth = camera!.orthographicSize * 2f * camera.aspect;
        var scale = Mathf.Abs(blurb.transform.lossyScale.x);
        if (scale <= 0f) return;

        var rect = blurb.rectTransform;
        rect.sizeDelta = new Vector2(screenWidth * WidthOfScreen / scale, rect.sizeDelta.y);

        // Centered on the screen.
        var position = blurb.transform.position;
        blurb.transform.position = new Vector3(camera.transform.position.x, position.y, position.z);

        blurb.enableWordWrapping = true;
        blurb.enableAutoSizing = false;
        blurb.overflowMode = TextOverflowModes.Overflow;
        blurb.horizontalAlignment = HorizontalAlignmentOptions.Center;

        Coroutines.Start(CoFit(__instance, blurb, blurb.fontSize));
    }

    // While the intro lasts: each time the text shows or changes, the size that holds it on two lines.
    private static IEnumerator CoFit(IntroCutscene intro, TextMeshPro blurb, float size)
    {
        var last = string.Empty;
        while (intro && blurb)
        {
            if (blurb.gameObject.activeInHierarchy && blurb.text != last)
            {
                last = blurb.text;
                Fit(blurb, size);
            }

            yield return null;
        }
    }

    private static void Fit(TextMeshPro blurb, float size)
    {
        var min = size * MinSize;
        blurb.fontSize = size;
        blurb.ForceMeshUpdate();
        while (blurb.textInfo.lineCount > MaxLines && blurb.fontSize > min)
        {
            blurb.fontSize = Mathf.Max(min, blurb.fontSize - (size * Step));
            blurb.ForceMeshUpdate();
        }
    }
}
