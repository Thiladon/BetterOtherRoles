using MiraAPI.Translation;

namespace BetterOtherRoles.Utilities;

/// <summary>
///     Whether the color of a player is lighter or darker, from its real color (default outfit, not a camouflage or a
///     morph). The lighter colors are those of BetterOtherRoles 1.7.1 (CustomColors.lighterColors), which no longer used
///     them (lighter for an even PlayerId); here the color counts, as in TOU-Mira 1.7.9.
/// </summary>
public static class BorColorType
{
    // Pink, orange, yellow, white, cyan, lime, rose, banana, coral.
    private static readonly HashSet<int> LighterColors = [3, 4, 5, 7, 10, 11, 13, 14, 17];

    /// <summary>
    ///     Gets whether the color is a lighter one.
    /// </summary>
    public static bool IsLighter(int colorId) => LighterColors.Contains(colorId);

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
