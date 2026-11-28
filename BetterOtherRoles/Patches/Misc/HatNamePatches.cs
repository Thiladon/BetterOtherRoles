using System.Text.Json;
using BetterOtherRoles.LocalSettings;
using CorsacCosmetics.Cosmetics;
using HarmonyLib;
using MiraAPI.LocalSettings;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Hats tab with the hats of BetterOtherRoles (BetterOtherRoles 1.7.1 HatsTabPatches): in alphabetical order, and
///     the name under each one, rows further apart, unless turned off in the local settings
///     (<see cref="BorLocalSettingsTab.ShowHatNames"/>); the author goes in the credits. CorsacCosmetics makes the
///     chips over several frames: each new chip gets its text, and its row is moved; a text out of the scroller is
///     hidden (the mask does not cut it). The hats of BetterOtherRoles and their authors are read from their bundles
///     (field "Author" of each hat, skipped by CorsacCosmetics).
/// </summary>
[HarmonyPatch]
public static class HatNamePatches
{
    private const string LabelName = "BorHatName";

    // Rows further apart, for the text.
    private const float RowSpacing = 1.25f;

    private const float LabelY = -0.65f;
    private const float LabelScale = 0.5f;

    private static Dictionary<string, string>? _authors;

    private static BorLocalSettingsTab Settings => LocalSettingsTabSingleton<BorLocalSettingsTab>.Instance;

    // The hats of BetterOtherRoles in alphabetical order (CorsacCosmetics shows them in the order of this list, which
    // is not the one of the bundle); the others keep their order, first.
    [HarmonyPatch(typeof(HatManager), nameof(HatManager.GetUnlockedHats))]
    [HarmonyPostfix]
    public static void GetUnlockedHatsPostfix(ref Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<HatData> __result)
    {
        var hats = __result.ToArray();
        var sorted = hats.Select((hat, index) => (hat, index))
            .OrderBy(x => Authors.ContainsKey(x.hat.name) ? 1 : 0)
            .ThenBy(x => Authors.ContainsKey(x.hat.name) ? x.hat.name : string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.index)
            .Select(x => x.hat)
            .ToArray();
        __result = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<HatData>(sorted);
    }

    [HarmonyPatch(typeof(HatsTab), nameof(HatsTab.Update))]
    [HarmonyPostfix]
    public static void UpdatePostfix(HatsTab __instance)
    {
        if (!Settings.ShowHatNames.Value || !PlayerCustomizationMenu.Instance) return;

        var chips = __instance.ColorChips;
        var changed = false;
        var hitbox = __instance.scroller.Hitbox;
        for (var i = 0; i < chips.Count; i++)
        {
            var chip = chips[i];
            if (!chip) continue;

            var label = chip.transform.Find(LabelName);
            if (!label)
            {
                var hat = chip.Tag?.TryCast<HatData>();
                if (!hat || !Authors.ContainsKey(hat!.name)) continue;

                label = AddLabel(chip, hat.name).transform;
                var position = chip.transform.localPosition;
                chip.transform.localPosition = new Vector3(position.x, __instance.YStart - (i / __instance.NumPerRow * __instance.YOffset * RowSpacing), position.z);
                changed = true;
            }

            // Text is not cut by the mask of the scroller: hidden once out of it.
            if (hitbox)
            {
                var text = label!.GetComponent<TextMeshPro>();
                var bounds = text.renderer.bounds;
                text.enabled = bounds.max.y <= hitbox.bounds.max.y && bounds.min.y >= hitbox.bounds.min.y;
            }
        }

        if (changed)
            __instance.scroller.CalculateAndSetYBounds(chips.Count, __instance.NumPerRow, __instance.NumRowsVisible / RowSpacing, __instance.YOffset * RowSpacing);
    }

    // The name of the hat under it (the author goes in the credits).
    private static TextMeshPro AddLabel(ColorChip chip, string name)
    {
        var template = PlayerCustomizationMenu.Instance.itemName;
        var label = Object.Instantiate(template, chip.transform);
        label.name = LabelName;
        label.transform.localPosition = new Vector3(0f, LabelY, -1f);
        label.transform.localScale = Vector3.one * LabelScale;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = false;
        label.enableWordWrapping = false;
        label.text = name;
        return label;
    }

    // Name -> author of the hats of BetterOtherRoles, from the manifests of their bundles (read once they are there).
    private static Dictionary<string, string> Authors
    {
        get
        {
            if (_authors is { Count: > 0 }) return _authors;

            _authors = ReadAuthors();
            return _authors;
        }
    }

    private static Dictionary<string, string> ReadAuthors()
    {
        var authors = new Dictionary<string, string>();
        if (!Directory.Exists(CosmeticPaths.BundlePath)) return authors;

        foreach (var file in Directory.EnumerateFiles(CosmeticPaths.BundlePath, "BOR.*.ccb"))
        {
            try
            {
                using var stream = File.OpenRead(file);
                var header = new byte[16];
                if (stream.Read(header, 0, header.Length) != header.Length) continue;

                var manifest = new byte[BitConverter.ToUInt32(header, 8)];
                if (stream.Read(manifest, 0, manifest.Length) != manifest.Length) continue;

                using var json = JsonDocument.Parse(manifest);
                foreach (var group in json.RootElement.GetProperty("Groups").EnumerateArray())
                {
                    foreach (var hat in group.GetProperty("Hats").EnumerateArray())
                    {
                        var author = hat.TryGetProperty("Author", out var value) ? value.GetString() ?? string.Empty : string.Empty;
                        authors[hat.GetProperty("Name").GetString() ?? string.Empty] = author;
                    }
                }
            }
            catch (Exception e)
            {
                Warning($"[HatNames] Could not read {Path.GetFileName(file)}: {e.Message}");
            }
        }

        Info($"[HatNames] {authors.Count} hats of BetterOtherRoles");
        return authors;
    }
}
