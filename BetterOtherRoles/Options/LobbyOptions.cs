using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Translation;

namespace BetterOtherRoles.Options;

/// <summary>
///     Lobby options, from BetterOtherRoles 1.7.1 (CustomOptionHolder.anyPlayerCanStopStart).
/// </summary>
public sealed class LobbyOptions : AbstractOptionGroup
{
    public override string GroupName => MiraLocaleManager.Get("BetterOtherRoles.Options.Groups.Lobby", "Lobby");

    public override uint GroupPriority => 1;

    /// <summary>
    ///     Gets whether every player can stop the game start countdown (the host and the admins always can).
    /// </summary>
    public ModdedToggleOption AnyPlayerCanStopStart { get; } = new("BetterOtherRoles.Option.Lobby.AnyPlayerCanStopStart", false);
}
