using BetterOtherRoles.Modules.Editions;
using BetterOtherRoles.Utilities;
using HarmonyLib;
using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Dingus edition (<see cref="BorEditions"/>): the Undertale specimen of the specimens task of Polus replaced by the
///     image of BetterOtherRoles 1.7.1 (SpecimenGamePatches, Resources/dfg.png at 500 pixels per unit).
/// </summary>
[HarmonyPatch(typeof(SpecimenGame), nameof(SpecimenGame.Begin))]
public static class DingusSpecimenPatches
{
    private const string Specimen = "panel_speciment_undertalesans";
    private const float PixelsPerUnit = 500f;

    private static readonly LoadableAsset<Sprite> Image = new LoadableResourceAsset("BetterOtherRoles.Resources.Editions.dfg.png");

    private static Sprite? _sprite;

    [HarmonyPostfix]
    public static void Postfix(SpecimenGame __instance)
    {
        if (!BorEditions.IsDingus) return;

        var renderer = __instance.SpecimenSprites.FirstOrDefault(x => x && x.name == Specimen);
        var sprite = GetSprite();
        if (renderer && sprite)
            renderer!.sprite = sprite;
    }

    private static Sprite? GetSprite()
    {
        if (_sprite) return _sprite;

        var source = Image.LoadAsset();
        if (!source) return null;

        var texture = source.texture;
        _sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        _sprite.name = "BorDingusSpecimen";
        BorSpriteUtils.KeepLoaded(_sprite);
        return _sprite;
    }
}
