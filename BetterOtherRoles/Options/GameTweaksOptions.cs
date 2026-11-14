using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Translation;

namespace BetterOtherRoles.Options;

/// <summary>
///     Game Tweaks: small changes of the game on every map (BetterOtherRoles 1.7.1 general options).
/// </summary>
public sealed class GameTweaksOptions : AbstractOptionGroup
{
    public override string GroupName => MiraLocaleManager.Get("BetterOtherRoles.Options.Groups.GameTweaks", "Game Tweaks");

    public override uint GroupPriority => 5;

    /// <summary>
    ///     Gets whether several players can be scanned in MedBay at the same time (BOR allowParallelMedBayScans,
    ///     <see cref="Patches.GameTweaks.MedScanPatches"/>).
    /// </summary>
    public ModdedToggleOption ParallelMedScans { get; } = new("BetterOtherRoles.Option.GameTweaks.ParallelMedScans", false);

    /// <summary>
    ///     Gets whether the player stands at a random place of the scanner during the scan, not always the same
    ///     (BOR RandomizePositionDuringScan, <see cref="Patches.GameTweaks.MedScanPatches"/>).
    /// </summary>
    public ModdedToggleOption RandomScanPosition { get; } = new("BetterOtherRoles.Option.GameTweaks.RandomScanPosition", false);
}
