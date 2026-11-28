using MiraAPI.Colors;
using UnityEngine;

namespace BetterOtherRoles.Modules.Colors;

/// <summary>
///     Player colors of BetterOtherRoles 1.7.1 (CustomColors), then of TOU-Mira 1.7.9, registered by MiraAPI after the
///     18 colors of the game, in this order (color ids 18 to 41 for BOR, 42 to 68 for TOU, used by
///     <see cref="Patches.Misc.ColorTabPatches"/>: a new color goes at the end). The names are locale keys
///     (BetterOtherRoles.Color.*). Lighter or darker as in BOR, but for Tamarind (darker), Ocean, Sundown and Slime
///     (lighter), from their brightness.
/// </summary>
[RegisterCustomColors]
public static class BorPlayerColors
{
    public static CustomColor Tamarind { get; } = Make("Tamarind", 48, 28, 34, 30, 11, 16, false);

    public static CustomColor Army { get; } = Make("Army", 39, 45, 31, 11, 30, 24, false);

    public static CustomColor Olive { get; } = Make("Olive", 154, 140, 61, 104, 95, 40, true);

    public static CustomColor Turquoise { get; } = Make("Turquoise", 22, 132, 176, 15, 89, 117, false);

    public static CustomColor Mint { get; } = Make("Mint", 111, 192, 156, 65, 148, 111, true);

    public static CustomColor Lavender { get; } = Make("Lavender", 173, 126, 201, 131, 58, 203, true);

    public static CustomColor Nougat { get; } = Make("Nougat", 160, 101, 56, 115, 15, 78, false);

    public static CustomColor Peach { get; } = Make("Peach", 255, 164, 119, 238, 128, 100, true);

    public static CustomColor Wasabi { get; } = Make("Wasabi", 112, 143, 46, 72, 92, 29, false);

    public static CustomColor HotPink { get; } = Make("HotPink", 255, 51, 102, 232, 0, 58, true);

    public static CustomColor Petrol { get; } = Make("Petrol", 0, 99, 105, 0, 61, 54, false);

    public static CustomColor Lemon { get; } = Make("Lemon", 0xDB, 0xFD, 0x2F, 0x74, 0xE5, 0x10, true);

    public static CustomColor SignalOrange { get; } = Make("SignalOrange", 0xF7, 0x44, 0x17, 0x9B, 0x2E, 0x0F, true);

    public static CustomColor Teal { get; } = Make("Teal", 0x25, 0xB8, 0xBF, 0x12, 0x89, 0x86, true);

    public static CustomColor Blurple { get; } = Make("Blurple", 61, 44, 142, 25, 14, 90, false);

    public static CustomColor Sunrise { get; } = Make("Sunrise", 0xFF, 0xCA, 0x19, 0xDB, 0x44, 0x42, true);

    public static CustomColor Ice { get; } = Make("Ice", 0xA8, 0xDF, 0xFF, 0x59, 0x9F, 0xC8, true);

    public static CustomColor Fuchsia { get; } = Make("Fuchsia", 164, 17, 129, 104, 3, 79, false);

    public static CustomColor RoyalGreen { get; } = Make("RoyalGreen", 9, 82, 33, 0, 46, 8, false);

    public static CustomColor Slime { get; } = Make("Slime", 244, 255, 188, 167, 239, 112, true);

    public static CustomColor Navy { get; } = Make("Navy", 9, 43, 119, 0, 13, 56, false);

    public static CustomColor Darkness { get; } = Make("Darkness", 36, 39, 40, 10, 10, 10, false);

    public static CustomColor Ocean { get; } = Make("Ocean", 55, 159, 218, 62, 92, 158, true);

    public static CustomColor Sundown { get; } = Make("Sundown", 252, 194, 100, 197, 98, 54, true);

    // TOU-Mira 1.7.9 (TownOfUsPlayerColors), color ids 42 to 68: without those too close to a color above or of the
    // game (Macau, Midnight, Gold, Chocolate, Silver, Beige) nor the rainbow; Olive, Turquoise and Mint renamed (BOR
    // has them); Tawny darker, from its brightness.
    public static CustomColor Charcoal { get; } = Make("Charcoal", 50, 48, 78, 12, 15, 46, false);

    public static CustomColor Denim { get; } = Make("Denim", 54, 47, 188, 21, 21, 129, false);

    public static CustomColor Mandarin { get; } = Make("Mandarin", 255, 149, 79, 230, 52, 76, true);

    public static CustomColor Ash { get; } = Make("Ash", 11, 14, 19, 4, 5, 7, false);

    public static CustomColor SeaGreen { get; } = Make("SeaGreen", 61, 255, 181, 31, 128, 91, true);

    public static CustomColor Chartreuse { get; } = Make("Chartreuse", 207, 255, 0, 143, 191, 61, true);

    public static CustomColor Blood { get; } = Make("Blood", 110, 0, 21, 61, 0, 46, false);

    public static CustomColor Glass { get; } = Make("Glass", 149, 202, 220, 79, 125, 161, true);

    public static CustomColor Jungle { get; } = Make("Jungle", 0, 47, 0, 0, 23, 0, false);

    public static CustomColor Nacho { get; } = Make("Nacho", 242, 166, 38, 185, 87, 25, true);

    public static CustomColor Tawny { get; } = Make("Tawny", 205, 63, 0, 141, 31, 0, false);

    public static CustomColor SkyBlue { get; } = Make("SkyBlue", 61, 129, 255, 31, 65, 128, true);

    public static CustomColor Shimmer { get; } = Make("Shimmer", 54, 252, 169, 30, 189, 191, true);

    public static CustomColor Crimson { get; } = Make("Crimson", 174, 29, 74, 107, 22, 72, false);

    public static CustomColor Violet { get; } = Make("Violet", 128, 6, 178, 78, 16, 145, false);

    public static CustomColor Snow { get; } = Make("Snow", 255, 255, 255, 163, 194, 223, true);

    public static CustomColor Steel { get; } = Make("Steel", 93, 97, 118, 59, 60, 81, false);

    public static CustomColor Plum { get; } = Make("Plum", 79, 0, 127, 55, 0, 95, false);

    public static CustomColor Grass { get; } = Make("Grass", 59, 130, 90, 9, 86, 73, false);

    public static CustomColor Azure { get; } = Make("Azure", 1, 166, 255, 17, 104, 151, true);

    public static CustomColor Watermelon { get; } = Make("Watermelon", 168, 50, 62, 101, 30, 37, false);

    public static CustomColor Magenta { get; } = Make("Magenta", 255, 0, 127, 191, 0, 95, false);

    public static CustomColor CottonCandy { get; } = Make("CottonCandy", 255, 141, 189, 241, 68, 166, true);

    public static CustomColor Lilac { get; } = Make("Lilac", 186, 161, 255, 93, 81, 128, true);

    public static CustomColor DarkOlive { get; } = Make("DarkOlive", 97, 114, 24, 66, 91, 15, false);

    public static CustomColor Lagoon { get; } = Make("Lagoon", 31, 164, 159, 4, 102, 141, true);

    public static CustomColor LightMint { get; } = Make("LightMint", 151, 255, 151, 109, 191, 109, true);

    private static CustomColor Make(string key, byte r, byte g, byte b, byte shadowR, byte shadowG, byte shadowB, bool lighter) =>
        new("BetterOtherRoles.Color." + key, new Color32(r, g, b, byte.MaxValue), new Color32(shadowR, shadowG, shadowB, byte.MaxValue))
        {
            ColorBrightness = lighter ? CustomColorBrightness.Lighter : CustomColorBrightness.Darker,
        };
}
