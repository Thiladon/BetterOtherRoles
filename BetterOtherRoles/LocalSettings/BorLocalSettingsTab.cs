using BepInEx.Configuration;
using MiraAPI.LocalSettings;
using MiraAPI.LocalSettings.Attributes;
using MiraAPI.Utilities;

namespace BetterOtherRoles.LocalSettings;

/// <summary>
///     BetterOtherRoles tab in the game options menu (local settings, saved per player).
///     Found and registered automatically by MiraAPI. The config section is the locale key of the category label.
/// </summary>
public class BorLocalSettingsTab(ConfigFile config) : LocalSettingsTab(config)
{
    /// <summary>
    ///     Gets the tab name key. The button shows the initials of the translated name when not hovered.
    /// </summary>
    public override string TabName => "BetterOtherRoles.LocalSettings.Tab";

    /// <summary>
    ///     Gets whether the player goes back to the lobby by itself at the end of a game (TOU-Mira 1.7.9 AutoRejoin).
    /// </summary>
    [LocalToggleSetting("BetterOtherRoles.LocalSettings.AutoRejoin", "BetterOtherRoles.LocalSettings.AutoRejoinDescription")]
    public ConfigEntry<bool> AutoRejoin { get; private set; } = config.Bind(
        "BetterOtherRoles.LocalSettings.General",
        "AutoRejoin",
        true,
        "Goes back to the lobby by itself at the end of a game.");

    /// <summary>
    ///     Gets the delay before going back to the lobby, in seconds, counted from the "Play Again" button.
    /// </summary>
    [LocalSliderSetting(
        "BetterOtherRoles.LocalSettings.AutoRejoinDelay",
        0f,
        30f,
        displayValue: true,
        formatString: "0",
        roundValue: true,
        suffixType: MiraNumberSuffixes.Seconds)]
    public ConfigEntry<float> AutoRejoinDelay { get; private set; } = config.Bind(
        "BetterOtherRoles.LocalSettings.General",
        "AutoRejoinDelay",
        4f,
        "Delay before going back to the lobby, in seconds (0 to 30).");

    /// <summary>
    ///     Gets whether the host's lobby code is copied to the clipboard when an online lobby is created
    ///     (BetterOtherRoles 1.7.1, <see cref="Modules.Lobby.BorLobbyCode"/>).
    /// </summary>
    [LocalToggleSetting("BetterOtherRoles.LocalSettings.CopyLobbyCode", "BetterOtherRoles.LocalSettings.CopyLobbyCodeDescription")]
    public ConfigEntry<bool> CopyLobbyCode { get; private set; } = config.Bind(
        "BetterOtherRoles.LocalSettings.General",
        "CopyLobbyCode",
        true,
        "Copies the lobby code to the clipboard when you create an online lobby.");
}
