using UnityEngine;

namespace BetterOtherRoles;

/// <summary>
///     Role and modifier colors, taken from BetterOtherRoles 1.7.1.
/// </summary>
public static class BorColors
{
    public static Color Crewmate => Palette.CrewmateRoleBlue;
    public static Color Impostor => Palette.ImpostorRed;
    public static Color Neutral => Color.gray;

    // Crewmate Colors
    public static Color Detective => new Color32(45, 106, 165, 255);
    public static Color Deputy => Sheriff;
    public static Color Engineer => new Color32(0, 40, 245, 255);
    public static Color Guesser => new Color32(255, 255, 0, 255);
    public static Color Hacker => new Color32(117, 250, 76, 255);
    public static Color Lighter => new Color32(238, 229, 190, 255);
    public static Color Mayor => new Color32(32, 77, 66, 255);
    public static Color Medic => new Color32(126, 251, 194, 255);
    public static Color Medium => new Color32(98, 120, 115, 255);
    public static Color Portalmaker => new Color32(69, 69, 169, 255);
    public static Color SecurityGuard => new Color32(195, 178, 95, 255);
    public static Color Seer => new Color32(97, 178, 108, 255);
    public static Color Sheriff => new Color32(248, 205, 70, 255);
    public static Color Snitch => new Color32(184, 251, 79, 255);
    public static Color Spy => Palette.ImpostorRed;
    public static Color Swapper => new Color32(134, 55, 86, 255);
    public static Color TimeMaster => new Color32(112, 142, 239, 255);
    public static Color Tracker => new Color32(100, 58, 220, 255);
    public static Color Trapper => new Color32(110, 57, 105, 255);

    // Impostor Colors
    public static Color BountyHunter => Palette.ImpostorRed;
    public static Color Bomber => Palette.ImpostorRed;
    public static Color Camouflager => Palette.ImpostorRed;
    public static Color Cleaner => Palette.ImpostorRed;
    public static Color Eraser => Palette.ImpostorRed;
    public static Color Godfather => Palette.ImpostorRed;
    public static Color Janitor => Palette.ImpostorRed;
    public static Color Mafioso => Palette.ImpostorRed;
    public static Color Morphling => Palette.ImpostorRed;
    public static Color Ninja => Palette.ImpostorRed;
    public static Color Trickster => Palette.ImpostorRed;
    public static Color Vampire => Palette.ImpostorRed;
    public static Color Warlock => Palette.ImpostorRed;
    public static Color Witch => Palette.ImpostorRed;
    public static Color Yoyo => Palette.ImpostorRed;

    // Neutral Colors
    public static Color Arsonist => new Color32(238, 112, 46, 255);
    public static Color Jackal => new Color32(0, 180, 235, 255);
    public static Color Jester => new Color32(236, 98, 165, 255);
    public static Color Lawyer => new Color32(134, 153, 25, 255);
    public static Color Pursuer => Lawyer;
    public static Color Sidekick => Jackal;
    public static Color Thief => new Color32(71, 99, 45, 255);
    public static Color Vulture => new Color32(139, 69, 19, 255);

    // Modifier Colors
    public static Color Bait => new Color32(0, 247, 255, 255);
    public static Color Lover => new Color32(232, 57, 185, 255);
    public static Color Mini => Color.yellow;
}
