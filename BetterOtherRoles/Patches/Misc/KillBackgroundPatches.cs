using BetterOtherRoles.Modules.Roles;
using BetterOtherRoles.Options;
using BetterOtherRoles.Utilities;
using HarmonyLib;
using MiraAPI.GameOptions;
using MiraAPI.Roles;
using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Background of the kill animation (TOU-Mira 1.7.9 KillOverlayPatch, images of TOU-Mira), option
///     <see cref="GameplayOptions.KillBackgroundColor"/>:
///     <list type="bullet">
///         <item>red: the flames of the game;</item>
///         <item>faction of the killer: red, blue (crewmates) or gray (neutrals) flames, same shape for the three;</item>
///         <item>role of the killer: a mask (red, blue and green parts) drawn with the player shader, in the color of the
///         role (flames) and a lighter one (middle). Game roles: impostor red or crewmate blue.</item>
///     </list>
///     The flames are one object of the overlay, kept from one kill to the next: the sprite and material of the game are
///     put back for red. The killer is found by its current outfit, the one the animation shows (KillOverlayInitData).
/// </summary>
[HarmonyPatch(typeof(OverlayKillAnimation), nameof(OverlayKillAnimation.CoShow))]
public static class KillBackgroundPatches
{
    private const string Path = "BetterOtherRoles.Resources.KillBackgrounds.";

    private static readonly LoadableAsset<Sprite> ImpostorAsset = new LoadableResourceAsset(Path + "KillBackground.png");
    private static readonly LoadableAsset<Sprite> CrewmateAsset = new LoadableResourceAsset(Path + "KillBackgroundCrew.png");
    private static readonly LoadableAsset<Sprite> NeutralAsset = new LoadableResourceAsset(Path + "KillBackgroundNeut.png");
    private static readonly LoadableAsset<Sprite> MaskAsset = new LoadableResourceAsset(Path + "KillBackgroundColor.png");

    private static Sprite? _impostor;
    private static Sprite? _crewmate;
    private static Sprite? _neutral;
    private static Sprite? _mask;

    // The game's flames, to put back, and the copy of the player material for the role color.
    private static Sprite? _gameSprite;
    private static Material? _gameMaterial;
    private static Material? _roleMaterial;

    private static readonly int BodyColor = Shader.PropertyToID("_BodyColor");
    private static readonly int BackColor = Shader.PropertyToID("_BackColor");
    private static readonly int VisorColor = Shader.PropertyToID("_VisorColor");

    public static void Prefix(OverlayKillAnimation __instance, KillOverlay parent)
    {
        var flames = FindFlames(parent);
        if (!flames) return;

        // Not one of ours: the game's (the overlay is made again with the HUD).
        var sprite = flames!.sprite;
        if (sprite != _impostor && sprite != _crewmate && sprite != _neutral && sprite != _mask)
        {
            _gameSprite = sprite;
            _gameMaterial = flames.sharedMaterial;
        }

        var mode = (KillBackground)OptionGroupSingleton<GameplayOptions>.Instance.KillBackgroundColor.Value;
        var role = mode == KillBackground.Red ? null : FindKillerRole(__instance);
        if (!role)
        {
            flames!.sprite = _gameSprite;
            flames.sharedMaterial = _gameMaterial;
            return;
        }

        if (mode == KillBackground.Faction)
        {
            flames!.sprite = BorRoleSelector.GetFaction(role!) switch
            {
                BorFaction.Impostor => Load(ImpostorAsset, ref _impostor),
                BorFaction.Neutral => Load(NeutralAsset, ref _neutral),
                _ => Load(CrewmateAsset, ref _crewmate),
            };
            flames.sharedMaterial = _gameMaterial;
            return;
        }

        var body = __instance.killerParts ? __instance.killerParts.cosmetics.currentBodySprite.BodySprite : null;
        if (!body) return;

        if (!_roleMaterial) _roleMaterial = new Material(body!.sharedMaterial);

        var color = RoleColor(role!);
        _roleMaterial!.SetColor(BodyColor, color);
        _roleMaterial.SetColor(BackColor, Color.Lerp(color, Color.white, 0.35f));
        _roleMaterial.SetColor(VisorColor, Color.white);
        flames!.sprite = Load(MaskAsset, ref _mask);
        flames.sharedMaterial = _roleMaterial;
    }

    // The game's flames: "BackgroundFlame" (under "QuadParent") in the overlay.
    private static SpriteRenderer? FindFlames(KillOverlay parent)
    {
        if (!parent) return null;

        foreach (var renderer in parent.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer.name == "BackgroundFlame") return renderer;
        }

        return null;
    }

    private static RoleBehaviour? FindKillerRole(OverlayKillAnimation animation)
    {
        var outfit = animation.initData?.killerOutfit;
        if (outfit == null) return null;

        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (player && player.Data && player.CurrentOutfit != null && player.CurrentOutfit.Pointer == outfit.Pointer)
                return player.Data.Role;
        }

        return null;
    }

    private static Color RoleColor(RoleBehaviour role)
    {
        if (role is ICustomRole customRole) return customRole.RoleColor;

        return role.IsImpostor ? BorColors.Impostor : BorColors.Crewmate;
    }

    // Images of the mod are unloaded between scenes otherwise (after a game).
    private static Sprite Load(LoadableAsset<Sprite> asset, ref Sprite? sprite)
    {
        if (sprite) return sprite!;

        sprite = asset.LoadAsset();
        BorSpriteUtils.KeepLoaded(sprite);
        return sprite;
    }
}
