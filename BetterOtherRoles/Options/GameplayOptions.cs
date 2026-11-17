using BetterOtherRoles.Modules.Cooldowns;
using BetterOtherRoles.Patches.Options;
using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Translation;

namespace BetterOtherRoles.Options;

/// <summary>
///     Game options: how the game plays, whatever the roles.
/// </summary>
public sealed class GameplayOptions : AbstractOptionGroup
{
    public override string GroupName => MiraLocaleManager.Get("BetterOtherRoles.Options.Groups.Gameplay", "Gameplay");

    public override uint GroupPriority => 3;

    /// <summary>
    ///     Gets the cooldown of the impostors' kill when the game starts (10 s in the game), then the kill cooldown of
    ///     the game. Shown in the vanilla game settings tab, under the kill cooldown, not here
    ///     (<see cref="KillCooldownPatches"/>).
    /// </summary>
    public ModdedNumberOption KillStartCooldown { get; } =
        BorCooldownOptions.StartCooldown("BetterOtherRoles.Setting.KillStartCooldown", () => false, KillCooldownPatches.Refresh);

    /// <summary>
    ///     Gets whether the cooldowns of each faction stop in minigames and at the admin table (BetterOtherRoles 1.7.1).
    ///     Off: they keep running (vanilla, TOU-Mira 1.7.9 TickCooldownsInMinigame). Always paused in a vent, whatever
    ///     these options (<see cref="BorCooldowns"/>).
    /// </summary>
    public ModdedToggleOption PauseCooldownsCrewmates { get; } = new("BetterOtherRoles.Option.Gameplay.PauseCooldownsCrewmates", true);

    public ModdedToggleOption PauseCooldownsImpostors { get; } = new("BetterOtherRoles.Option.Gameplay.PauseCooldownsImpostors", true);

    public ModdedToggleOption PauseCooldownsNeutrals { get; } = new("BetterOtherRoles.Option.Gameplay.PauseCooldownsNeutrals", true);

    /// <summary>
    ///     Gets whether the impostors see the role of the other impostors above their names
    ///     (<see cref="Patches.Misc.RoleNamePatches"/>); without it, they only see them in red.
    /// </summary>
    public ModdedToggleOption ImpostorsKnowRoles { get; } = new("BetterOtherRoles.Option.Gameplay.ImpostorsKnowRoles", false);

    /// <summary>
    ///     Gets the background of the kill animation (TOU-Mira 1.7.9 KillAnimationBackgroundColor,
    ///     <see cref="Patches.Misc.KillBackgroundPatches"/>): red as in the game, the faction of the killer, or its role.
    /// </summary>
    public ModdedEnumOption KillBackgroundColor { get; } =
        new("BetterOtherRoles.Option.Gameplay.KillBackgroundColor", (int)KillBackground.Red, typeof(KillBackground),
            ["BetterOtherRoles.Option.Gameplay.KillBackgroundColorRed", "BetterOtherRoles.Option.Gameplay.KillBackgroundColorFaction",
                "BetterOtherRoles.Option.Gameplay.KillBackgroundColorRole"]);

    /// <summary>
    ///     Gets how the players whose body is cleaned show on the vitals (TOU-Mira 1.7.9 CleanedBodiesAppearance,
    ///     <see cref="Modules.Bodies.BorCleanedBodies"/>): missing, dead or disconnected.
    /// </summary>
    public ModdedEnumOption CleanedBodies { get; } =
        new("BetterOtherRoles.Option.Gameplay.CleanedBodies", (int)CleanedBody.Missing, typeof(CleanedBody),
            ["BetterOtherRoles.Option.Gameplay.CleanedBodiesMissing", "BetterOtherRoles.Option.Gameplay.CleanedBodiesDead",
                "BetterOtherRoles.Option.Gameplay.CleanedBodiesDisconnected"]);

    /// <summary>
    ///     Gets who sees the pets in game (TOU-Mira 1.7.9 ShowPetsMode, <see cref="Patches.Misc.PetVisibilityPatches"/>):
    ///     everyone, each player their own, or everyone until the player dies.
    /// </summary>
    public ModdedEnumOption PetVisibility { get; } =
        new("BetterOtherRoles.Option.Gameplay.PetVisibility", (int)Options.PetVisibility.RemovedOnDeath, typeof(PetVisibility),
            ["BetterOtherRoles.Option.Gameplay.PetVisibilityEveryone", "BetterOtherRoles.Option.Gameplay.PetVisibilityOwnOnly",
                "BetterOtherRoles.Option.Gameplay.PetVisibilityRemovedOnDeath"]);

    /// <summary>
    ///     Gets when the names of the other players are hidden out of sight (TOU-Mira 1.7.9 HideNamesOutOfSight,
    ///     <see cref="Patches.Misc.HiddenNamesPatches"/>): never, out of sight unless in the same room, or out of sight.
    /// </summary>
    public ModdedEnumOption HideNamesOutOfSight { get; } =
        new("BetterOtherRoles.Option.Gameplay.HideNamesOutOfSight", (int)NameSight.Basic, typeof(NameSight),
            ["BetterOtherRoles.Option.Gameplay.HideNamesOutOfSightBasic", "BetterOtherRoles.Option.Gameplay.HideNamesOutOfSightRoomBased",
                "BetterOtherRoles.Option.Gameplay.HideNamesOutOfSightHarsh"]);

    /// <summary>
    ///     Gets when the lid of a vent only moves for the players who see it (TOU-Mira 1.7.9 HideVentAnimationNotInVision,
    ///     <see cref="Patches.Misc.VentAnimationPatches"/>): in sight, in sight from the same room, or never hidden.
    /// </summary>
    public ModdedEnumOption HideVentAnimations { get; } =
        new("BetterOtherRoles.Option.Gameplay.HideVentAnimations", (int)VentSight.InSight, typeof(VentSight),
            ["BetterOtherRoles.Option.Gameplay.HideVentAnimationsInSight", "BetterOtherRoles.Option.Gameplay.HideVentAnimationsSameRoom",
                "BetterOtherRoles.Option.Gameplay.HideVentAnimationsNever"]);

    /// <summary>
    ///     Gets whether the living players are shown in a random order in meetings, the same for everyone
    ///     (BetterOtherRoles 1.7.1 RandomizePlayersInMeeting, <see cref="Modules.Meeting.BorMeetingShuffle"/>).
    /// </summary>
    public ModdedToggleOption ShuffleMeetingOrder { get; } = new("BetterOtherRoles.Option.Gameplay.ShuffleMeetingOrder", false);
}

/// <summary>
///     Who sees the lid of a vent move when another player goes in or out.
/// </summary>
public enum VentSight
{
    InSight,
    SameRoom,
    Never,
}

/// <summary>
///     When the names of the other players are hidden out of sight.
/// </summary>
public enum NameSight
{
    Basic,
    RoomBased,
    Harsh,
}

/// <summary>
///     Who sees the pets in game.
/// </summary>
public enum PetVisibility
{
    Everyone,
    OwnOnly,
    RemovedOnDeath,
}

/// <summary>
///     How a player whose body is cleaned shows on the vitals.
/// </summary>
public enum CleanedBody
{
    Missing,
    Dead,
    Disconnected,
}

/// <summary>
///     Background of the kill animation.
/// </summary>
public enum KillBackground
{
    Red,
    Faction,
    Role,
}
