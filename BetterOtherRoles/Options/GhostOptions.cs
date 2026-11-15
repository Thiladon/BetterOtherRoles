using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Translation;

namespace BetterOtherRoles.Options;

/// <summary>
///     Options of the dead players: haunting (<see cref="Modules.Roles.BorHaunt"/>) and zooming out
///     (<see cref="Modules.Display.BorZoom"/>).
/// </summary>
public sealed class GhostOptions : AbstractOptionGroup
{
    public override string GroupName => MiraLocaleManager.Get("BetterOtherRoles.Options.Groups.Ghost", "Ghost Options");

    public override uint GroupPriority => 5;

    /// <summary>
    ///     Gets when a dead player may haunt (TOU-Mira 1.7.9 DeadCanHaunt, <see cref="Modules.Roles.BorHaunt"/>): always,
    ///     from the round after its death, or never (<see cref="HauntMode"/>).
    /// </summary>
    public ModdedEnumOption DeadCanHaunt { get; } =
        new("BetterOtherRoles.Option.Gameplay.DeadCanHaunt", (int)HauntMode.Always, typeof(HauntMode),
            ["BetterOtherRoles.Option.Gameplay.DeadCanHauntAlways", "BetterOtherRoles.Option.Gameplay.DeadCanHauntNextRound", "BetterOtherRoles.Option.Gameplay.DeadCanHauntNever"]);

    /// <summary>
    ///     Gets whether a dead player has to finish its tasks before haunting (BetterOtherRoles 1.7.1
    ///     finishTasksBeforeHauntingOrZoomingOut, haunt part): the haunt button is hidden until then.
    /// </summary>
    public ModdedToggleOption FinishTasksBeforeHaunt { get; } =
        new("BetterOtherRoles.Option.Gameplay.FinishTasksBeforeHaunt", true)
        {
            Visible = () => OptionGroupSingleton<GhostOptions>.Instance.DeadCanHaunt.Value != (int)HauntMode.Never,
        };

    /// <summary>
    ///     Gets whether a dead player has to finish its tasks before zooming out (BetterOtherRoles 1.7.1
    ///     finishTasksBeforeHauntingOrZoomingOut, zoom part; <see cref="Modules.Display.BorZoom"/>).
    /// </summary>
    public ModdedToggleOption FinishTasksBeforeZoom { get; } = new("BetterOtherRoles.Option.Gameplay.FinishTasksBeforeZoom", true);
}

/// <summary>
///     When a dead player may haunt.
/// </summary>
public enum HauntMode
{
    Always,
    NextRound,
    Never,
}
