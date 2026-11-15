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
