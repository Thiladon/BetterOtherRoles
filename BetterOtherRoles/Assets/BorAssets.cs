using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace BetterOtherRoles.Assets;

/// <summary>
///     Sprites embedded in the DLL (Resources folder), taken from BetterOtherRoles 1.7.1 (role buttons) or made for this
///     mod (Debug button, menu buttons and icons).
/// </summary>
public static class BorAssets
{
    private const string ButtonsPath = "BetterOtherRoles.Resources.Buttons.";
    private const string MenusPath = "BetterOtherRoles.Resources.Menus.";

    // BetterOtherRoles 1.7.1 loads the button sprites with 115 pixels per unit.
    private const float ButtonPixelsPerUnit = 115f;
    private const float MenuPixelsPerUnit = 100f;

    // Vent on the map, white to be tinted with the color of its network (BetterOtherRoles 1.7.1, 150 pixels per unit).
    public static LoadableAsset<Sprite> MapVent { get; } = new LoadableResourceAsset("BetterOtherRoles.Resources.Map.Vent.png", 150f);

    public static LoadableAsset<Sprite> DeputyHandcuffButton { get; } =
        new LoadableResourceAsset(ButtonsPath + "DeputyHandcuffButton.png", ButtonPixelsPerUnit);

    public static LoadableAsset<Sprite> DeputyHandcuffed { get; } =
        new LoadableResourceAsset(ButtonsPath + "DeputyHandcuffed.png", ButtonPixelsPerUnit);

    // Debug button (wrench), drawn on the role guide button sprites (same size: 102 x 92).
    public static LoadableAsset<Sprite> DebugButton { get; } = new LoadableResourceAsset(ButtonsPath + "DebugButton.png");

    public static LoadableAsset<Sprite> DebugButtonActive { get; } = new LoadableResourceAsset(ButtonsPath + "DebugButtonActive.png");

    // Zoom button (BetterOtherRoles 1.7.1 images, redrawn at the size of the guide ones: 102 x 92): minus to zoom out,
    // plus to zoom back in.
    public static LoadableAsset<Sprite> ZoomOutButton { get; } = new LoadableResourceAsset(ButtonsPath + "Minus_Button.png");

    public static LoadableAsset<Sprite> ZoomOutButtonActive { get; } = new LoadableResourceAsset(ButtonsPath + "Minus_ButtonActive.png");

    public static LoadableAsset<Sprite> ZoomInButton { get; } = new LoadableResourceAsset(ButtonsPath + "Plus_Button.png");

    public static LoadableAsset<Sprite> ZoomInButtonActive { get; } = new LoadableResourceAsset(ButtonsPath + "Plus_ButtonActive.png");

    // Close button of the role guide, like the game settings one (idle and under the mouse).
    public static LoadableAsset<Sprite> CloseButton { get; } = new LoadableResourceAsset(MenusPath + "CloseButton.png", MenuPixelsPerUnit);

    public static LoadableAsset<Sprite> CloseButtonHover { get; } = new LoadableResourceAsset(MenusPath + "CloseButtonHover.png", MenuPixelsPerUnit);

    // Round back button of the phone menus, at the size of their close button (65 px).
    public static LoadableAsset<Sprite> BackButton { get; } = new LoadableResourceAsset(MenusPath + "BackButton.png", MenuPixelsPerUnit);

    // Icons of the Presets tab, in the color of the refresh icon.
    public static LoadableAsset<Sprite> PresetTrash { get; } = new LoadableResourceAsset(MenusPath + "PresetTrash.png", MenuPixelsPerUnit);

    public static LoadableAsset<Sprite> PresetPencil { get; } = new LoadableResourceAsset(MenusPath + "PresetPencil.png", MenuPixelsPerUnit);

    // Button of the end game summary, right of the exit button (TOU-Mira 1.7.9 GameSummaryButton, Ressources/TOU-Ressources).
    public static LoadableAsset<Sprite> GameSummaryButton { get; } = new LoadableResourceAsset(MenusPath + "GameSummaryButton.png", MenuPixelsPerUnit);
}
