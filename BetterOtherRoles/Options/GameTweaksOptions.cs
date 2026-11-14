using BetterOtherRoles.Patches.Options;
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

    public GameTweaksOptions()
    {
        ImpostorsIgnoreNightVision.SetNotificationParent(NightVisionCams);
    }

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

    /// <summary>
    ///     Gets whether the wiring panels are shuffled between their places at each game (BOR Randomize Wire Tasks
    ///     Positions, <see cref="Patches.GameTweaks.TaskPositionsPatches"/>).
    /// </summary>
    public ModdedToggleOption RandomWirePositions { get; } = new("BetterOtherRoles.Option.GameTweaks.RandomWirePositions", false);

    /// <summary>
    ///     Gets whether the upload console may swap places with a download console at each game (BOR Randomize Upload
    ///     Task Positions, <see cref="Patches.GameTweaks.TaskPositionsPatches"/>).
    /// </summary>
    public ModdedToggleOption RandomUploadPosition { get; } = new("BetterOtherRoles.Option.GameTweaks.RandomUploadPosition", false);

    /// <summary>
    ///     Gets whether the cams switch to night vision when the lights are off: every player looks the same, without
    ///     name (BOR Cams Switch To Night Vision If Lights Are Off, <see cref="Patches.GameTweaks.NightVisionCamsPatches"/>).
    /// </summary>
    public ModdedToggleOption NightVisionCams { get; } = new("BetterOtherRoles.Option.GameTweaks.NightVisionCams", false);

    /// <summary>
    ///     Gets whether the impostors keep the normal cams when the lights are off (BOR Impostor Vision Ignores Night
    ///     Vision Cams).
    /// </summary>
    public ModdedToggleOption ImpostorsIgnoreNightVision { get; } =
        new("BetterOtherRoles.Option.GameTweaks.ImpostorsIgnoreNightVision", false)
        {
            Visible = () => OptionGroupSingleton<GameTweaksOptions>.Instance.NightVisionCams.Value,
        };
}
