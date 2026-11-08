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

    /// <summary>
    ///     Gets whether the names in the chat are in the color of the role, when it is known
    ///     (<see cref="Patches.Misc.ChatRoleColorPatches"/>).
    /// </summary>
    [LocalToggleSetting("BetterOtherRoles.LocalSettings.ChatRoleColors", "BetterOtherRoles.LocalSettings.ChatRoleColorsDescription")]
    public ConfigEntry<bool> ChatRoleColors { get; private set; } = config.Bind(
        "BetterOtherRoles.LocalSettings.Chat",
        "ChatRoleColors",
        true,
        "Shows the names in the chat in the color of the role, when you know it.");

    /// <summary>
    ///     Gets whether the notification of a new chat message is shown in game too (TOU-Mira 1.7.9 ShowChatNotifsInGame;
    ///     the game shows it in the lobby only, <see cref="Patches.Misc.ChatNotificationPatches"/>).
    /// </summary>
    [LocalToggleSetting("BetterOtherRoles.LocalSettings.ChatNotificationsInGame", "BetterOtherRoles.LocalSettings.ChatNotificationsInGameDescription")]
    public ConfigEntry<bool> ChatNotificationsInGame { get; private set; } = config.Bind(
        "BetterOtherRoles.LocalSettings.Chat",
        "ChatNotificationsInGame",
        true,
        "Shows the notification of a new chat message in game too.");

    /// <summary>
    ///     Gets on which players the name is in the color of the role (<see cref="Patches.Misc.NameColorPatches"/>):
    ///     never (game colors), on me only, or on everyone whose role is known.
    /// </summary>
    [LocalEnumSetting(
        "BetterOtherRoles.LocalSettings.NameColors",
        "BetterOtherRoles.LocalSettings.NameColorsDescription",
        [
            "BetterOtherRoles.LocalSettings.NameColors.Never",
            "BetterOtherRoles.LocalSettings.NameColors.OnSelf",
            "BetterOtherRoles.LocalSettings.NameColors.Everyone",
        ])]
    public ConfigEntry<NameColorDisplay> NameColors { get; private set; } = config.Bind(
        "BetterOtherRoles.LocalSettings.Players",
        "NameColors",
        NameColorDisplay.OnSelf,
        "On which players the name is in the color of the role.");

    /// <summary>
    ///     Gets on which players the progress is shown with the name: task counter of a crewmate, or the progress of a
    ///     role (TOU-Mira 1.7.9 DisplayPlayerProgress, <see cref="Patches.Misc.RoleNamePatches"/>).
    /// </summary>
    [LocalEnumSetting(
        "BetterOtherRoles.LocalSettings.PlayerProgress",
        "BetterOtherRoles.LocalSettings.PlayerProgressDescription",
        [
            "BetterOtherRoles.LocalSettings.PlayerProgress.Never",
            "BetterOtherRoles.LocalSettings.PlayerProgress.OnSelf",
            "BetterOtherRoles.LocalSettings.PlayerProgress.OnOthers",
            "BetterOtherRoles.LocalSettings.PlayerProgress.Always",
        ])]
    public ConfigEntry<ProgressDisplay> PlayerProgress { get; private set; } = config.Bind(
        "BetterOtherRoles.LocalSettings.Players",
        "PlayerProgress",
        ProgressDisplay.OnOthers,
        "On which players the progress (tasks, role) is shown with the name.");

    /// <summary>
    ///     Gets where the role is shown in game, above or below the name (<see cref="Patches.Misc.RoleNamePatches"/>).
    /// </summary>
    [LocalEnumSetting(
        "BetterOtherRoles.LocalSettings.RoleInGame",
        "BetterOtherRoles.LocalSettings.RoleInGameDescription",
        [
            "BetterOtherRoles.LocalSettings.RolePlace.Above",
            "BetterOtherRoles.LocalSettings.RolePlace.Below",
        ])]
    public ConfigEntry<RolePlace> RoleInGame { get; private set; } = config.Bind(
        "BetterOtherRoles.LocalSettings.Players",
        "RoleInGame",
        RolePlace.Above,
        "Where the role is shown in game: above or below the name.");

    /// <summary>
    ///     Gets where the role is shown in meetings, above or below the name (<see cref="Patches.Misc.RoleNamePatches"/>).
    /// </summary>
    [LocalEnumSetting(
        "BetterOtherRoles.LocalSettings.RoleInMeeting",
        "BetterOtherRoles.LocalSettings.RoleInMeetingDescription",
        [
            "BetterOtherRoles.LocalSettings.RolePlace.Above",
            "BetterOtherRoles.LocalSettings.RolePlace.Below",
        ])]
    public ConfigEntry<RolePlace> RoleInMeeting { get; private set; } = config.Bind(
        "BetterOtherRoles.LocalSettings.Players",
        "RoleInMeeting",
        RolePlace.Below,
        "Where the role is shown in meetings: above or below the name.");
}

/// <summary>
///     Where the role is shown, from the name.
/// </summary>
public enum RolePlace
{
    Above,
    Below,
}

/// <summary>
///     On which players the name is in the color of the role.
/// </summary>
public enum NameColorDisplay
{
    Never,
    OnSelf,
    Everyone,
}

/// <summary>
///     On which players the progress is shown with the name.
/// </summary>
public enum ProgressDisplay
{
    Never,
    OnSelf,
    OnOthers,
    Always,
}
