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

    public static LoadableAsset<Sprite> DeputyHandcuffButton { get; } =
        new LoadableResourceAsset(ButtonsPath + "DeputyHandcuffButton.png", ButtonPixelsPerUnit);

    public static LoadableAsset<Sprite> DeputyHandcuffed { get; } =
        new LoadableResourceAsset(ButtonsPath + "DeputyHandcuffed.png", ButtonPixelsPerUnit);

    // Debug button (wrench), drawn on the role guide button sprites (same size: 102 x 92).
    public static LoadableAsset<Sprite> DebugButton { get; } = new LoadableResourceAsset(ButtonsPath + "DebugButton.png");

    public static LoadableAsset<Sprite> DebugButtonActive { get; } = new LoadableResourceAsset(ButtonsPath + "DebugButtonActive.png");

    // Close button of the role guide, like the game settings one (idle and under the mouse).
    public static LoadableAsset<Sprite> CloseButton { get; } = new LoadableResourceAsset(MenusPath + "CloseButton.png", MenuPixelsPerUnit);

    public static LoadableAsset<Sprite> CloseButtonHover { get; } = new LoadableResourceAsset(MenusPath + "CloseButtonHover.png", MenuPixelsPerUnit);

    // Round back button of the phone menus, at the size of their close button (65 px).
    public static LoadableAsset<Sprite> BackButton { get; } = new LoadableResourceAsset(MenusPath + "BackButton.png", MenuPixelsPerUnit);

    // Icons of the Presets tab, in the color of the refresh icon.
    public static LoadableAsset<Sprite> PresetTrash { get; } = new LoadableResourceAsset(MenusPath + "PresetTrash.png", MenuPixelsPerUnit);

    public static LoadableAsset<Sprite> PresetPencil { get; } = new LoadableResourceAsset(MenusPath + "PresetPencil.png", MenuPixelsPerUnit);
}
