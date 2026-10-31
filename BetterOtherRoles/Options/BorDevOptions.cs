using BetterOtherRoles.Modules.Admin;
using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Translation;

namespace BetterOtherRoles.Options;

/// <summary>
///     Options only shown when the host is a BetterOtherRoles admin (<see cref="BorAdmin.IsHostAdmin"/>), to everyone then.
/// </summary>
public sealed class BorDevOptions : AbstractOptionGroup
{
    public override string GroupName => MiraLocaleManager.Get("BetterOtherRoles.Options.Groups.Dev", "Dev");

    public override Func<bool> GroupVisible => () => BorAdmin.IsHostAdmin;

    public override uint GroupPriority => 0;

    /// <summary>
    ///     Gets the Debug Mode: the game can start with less than 4 players and never ends.
    ///     Turned off by a host without the admin key (<see cref="Patches.Misc.TestModePatches"/>).
    /// </summary>
    public ModdedToggleOption TestMode { get; } = new("BetterOtherRoles.Option.Dev.TestMode", false, false)
    {
        Visible = () => BorAdmin.IsHostAdmin,
    };

    /// <summary>
    ///     Gets a value indicating whether the Debug Mode is active (host with the admin key only).
    /// </summary>
    [MiraIgnore]
    public static bool IsTestModeActive => BorAdmin.IsHostAdmin && OptionGroupSingleton<BorDevOptions>.Instance.TestMode.Value;
}
