using BetterOtherRoles.Modules.Lobby;
using BetterOtherRoles.Patches.Options;
using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Translation;
using MiraAPI.Utilities;

namespace BetterOtherRoles.Options;

/// <summary>
///     Lobby options, from BetterOtherRoles 1.7.1 (CustomOptionHolder.anyPlayerCanStopStart, DynamicLobbies).
/// </summary>
public sealed class LobbyOptions : AbstractOptionGroup
{
    public override string GroupName => MiraLocaleManager.Get("BetterOtherRoles.Options.Groups.Lobby", "Lobby");

    public override uint GroupPriority => 1;

    /// <summary>
    ///     Gets the lobby size, changed after the lobby creation (also with /size). Left out of the presets, like the map:
    ///     it belongs to the lobby. Set to the size chosen on the creation screen when the lobby is created.
    ///     Shown in the vanilla game settings tab, under the game mode (<see cref="LobbySizeOptionPatches"/>), not here.
    /// </summary>
    public ModdedNumberOption MaxPlayers { get; } =
        new("BetterOtherRoles.Option.Lobby.MaxPlayers", BorLobbySize.MaxSize, BorLobbySize.MinSize, BorLobbySize.MaxSize, 1f,
            MiraNumberSuffixes.None, formatString: "0", includeInPreset: false)
        {
            Visible = () => false,
            ChangedEvent = x =>
            {
                BorLobbySize.Apply((int)x);
                LobbySizeOptionPatches.Refresh(x);
            },
        };

    /// <summary>
    ///     Gets whether every player can stop the game start countdown (the host and the admins always can).
    /// </summary>
    public ModdedToggleOption AnyPlayerCanStopStart { get; } = new("BetterOtherRoles.Option.Lobby.AnyPlayerCanStopStart", false);

    /// <summary>
    ///     Gets whether the host sends its rules to each player joining the lobby, once (<see cref="BorLobbyRules"/>).
    ///     Left out of the presets: it belongs to the host.
    /// </summary>
    public ModdedToggleOption ShowRulesOnJoin { get; } = new("BetterOtherRoles.Option.Lobby.ShowRulesOnJoin", true, false);
}
