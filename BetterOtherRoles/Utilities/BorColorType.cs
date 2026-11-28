using MiraAPI.Colors;
using MiraAPI.Translation;

namespace BetterOtherRoles.Utilities;

/// <summary>
///     Whether the color of a player is lighter or darker, from its real color (default outfit, not a camouflage or a
///     morph), as MiraAPI knows it: the colors of the game (the lighter ones are those of BetterOtherRoles 1.7.1
///     CustomColors.lighterColors), then those of the mods (<see cref="Modules.Colors.BorPlayerColors"/>).
///     BetterOtherRoles 1.7.1 no longer used them (lighter for an even PlayerId); here the color counts, as in
///     TOU-Mira 1.7.9.
/// </summary>
public static class BorColorType
{
    /// <summary>
    ///     Gets whether the color is a lighter one.
    /// </summary>
    public static bool IsLighter(int colorId) => PaletteManager.GetBrightness(colorId) == CustomColorBrightness.Lighter;

    /// <summary>
    ///     Gets whether the real color of the player is a lighter one.
    /// </summary>
    public static bool IsLighter(NetworkedPlayerInfo data) => IsLighter(data.DefaultOutfit.ColorId);

    /// <summary>
    ///     Gets the letter of the color type: "L" or "D" in English.
    /// </summary>
    public static string GetLetter(NetworkedPlayerInfo data) => IsLighter(data)
        ? MiraLocaleManager.Get("BetterOtherRoles.ColorType.LighterLetter", "L")
        : MiraLocaleManager.Get("BetterOtherRoles.ColorType.DarkerLetter", "D");
}
