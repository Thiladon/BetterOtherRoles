using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace BetterOtherRoles.Assets;

/// <summary>
///     Sprites embedded in the DLL (Resources folder), taken from BetterOtherRoles 1.7.1.
/// </summary>
public static class BorAssets
{
    private const string ButtonsPath = "BetterOtherRoles.Resources.Buttons.";

    // BetterOtherRoles 1.7.1 loads the button sprites with 115 pixels per unit.
    private const float ButtonPixelsPerUnit = 115f;

    public static LoadableAsset<Sprite> DeputyHandcuffButton { get; } =
        new LoadableResourceAsset(ButtonsPath + "DeputyHandcuffButton.png", ButtonPixelsPerUnit);

    public static LoadableAsset<Sprite> DeputyHandcuffed { get; } =
        new LoadableResourceAsset(ButtonsPath + "DeputyHandcuffed.png", ButtonPixelsPerUnit);
}
