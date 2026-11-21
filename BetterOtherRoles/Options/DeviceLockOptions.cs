using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Translation;
using MiraAPI.Utilities;

namespace BetterOtherRoles.Options;

/// <summary>
///     Devices locked for the crewmates (TOU-Mira 1.7.9 Advanced Utility Options,
///     <see cref="Patches.GameTweaks.DeviceLockPatches"/>): each device has its tasks to do and its rounds locked
///     (0: no such condition, both must be met when both are set).
/// </summary>
public sealed class DeviceLockOptions : AbstractOptionGroup
{
    public override string GroupName => MiraLocaleManager.Get("BetterOtherRoles.Options.Groups.Sabotages", "Sabotages");

    public override uint GroupPriority => 13;

    public ModdedToggleOption LockAdmin { get; } = Lock("LockAdmin");

    public ModdedNumberOption TasksForAdmin { get; } = Tasks("TasksForAdmin", 1f, options => options.LockAdmin);

    public ModdedNumberOption RoundsForAdmin { get; } = Rounds("RoundsForAdmin", options => options.LockAdmin);

    public ModdedToggleOption LockCams { get; } = Lock("LockCams");

    public ModdedNumberOption TasksForCams { get; } = Tasks("TasksForCams", 2f, options => options.LockCams);

    public ModdedNumberOption RoundsForCams { get; } = Rounds("RoundsForCams", options => options.LockCams);

    public ModdedToggleOption LockDoorLog { get; } = Lock("LockDoorLog");

    public ModdedNumberOption TasksForDoorLog { get; } = Tasks("TasksForDoorLog", 0f, options => options.LockDoorLog);

    public ModdedNumberOption RoundsForDoorLog { get; } = Rounds("RoundsForDoorLog", options => options.LockDoorLog);

    public ModdedToggleOption LockVitals { get; } = Lock("LockVitals");

    public ModdedNumberOption TasksForVitals { get; } = Tasks("TasksForVitals", 3f, options => options.LockVitals);

    public ModdedNumberOption RoundsForVitals { get; } = Rounds("RoundsForVitals", options => options.LockVitals);

    private static ModdedToggleOption Lock(string name) => new($"BetterOtherRoles.Option.Devices.{name}", false);

    // Tasks to do before using the device (0: not locked by the tasks).
    private static ModdedNumberOption Tasks(string name, float defaultValue, Func<DeviceLockOptions, ModdedToggleOption> lockOf) =>
        new($"BetterOtherRoles.Option.Devices.{name}", defaultValue, 0f, 15f, 1f, MiraNumberSuffixes.None, "0")
        {
            Visible = () => lockOf(OptionGroupSingleton<DeviceLockOptions>.Instance).Value,
        };

    // Rounds the device stays locked (1: until the end of the first meeting, 0: not locked by the rounds).
    private static ModdedNumberOption Rounds(string name, Func<DeviceLockOptions, ModdedToggleOption> lockOf) =>
        new($"BetterOtherRoles.Option.Devices.{name}", 0f, 0f, 5f, 1f, MiraNumberSuffixes.None, "0")
        {
            Visible = () => lockOf(OptionGroupSingleton<DeviceLockOptions>.Instance).Value,
        };
}
