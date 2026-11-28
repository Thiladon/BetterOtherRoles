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
    ///     Gets whether the hats of BetterOtherRoles are downloaded when the game starts, when missing
    ///     (TOU-Mira 1.7.9 AutoDownloadCosmetics, <see cref="Modules.Cosmetics.BorCosmetics"/>).
    /// </summary>
    [LocalToggleSetting("BetterOtherRoles.LocalSettings.AutoDownloadCosmetics", "BetterOtherRoles.LocalSettings.AutoDownloadCosmeticsDescription")]
    public ConfigEntry<bool> AutoDownloadCosmetics { get; private set; } = config.Bind(
        "BetterOtherRoles.LocalSettings.General",
        "AutoDownloadCosmetics",
        true,
        "Downloads the hats of BetterOtherRoles when the game starts, when they are missing.");

    /// <summary>
    ///     Gets whether the name is shown under each hat of BetterOtherRoles in the hats tab
    ///     (BetterOtherRoles 1.7.1, <see cref="Patches.Misc.HatNamePatches"/>).
    /// </summary>
    [LocalToggleSetting("BetterOtherRoles.LocalSettings.ShowHatNames", "BetterOtherRoles.LocalSettings.ShowHatNamesDescription")]
    public ConfigEntry<bool> ShowHatNames { get; private set; } = config.Bind(
        "BetterOtherRoles.LocalSettings.General",
        "ShowHatNames",
        true,
        "Shows the name under each hat of BetterOtherRoles in the hats tab.");

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
    ///     Gets whether the welcome message is shown in the chat of the first lobby after launching the game
    ///     (TOU-Mira 1.7.9 ShowWelcomeMessage, <see cref="Patches.Misc.LobbyChatPatches"/>).
    /// </summary>
    [LocalToggleSetting("BetterOtherRoles.LocalSettings.ShowWelcomeMessage", "BetterOtherRoles.LocalSettings.ShowWelcomeMessageDescription")]
    public ConfigEntry<bool> ShowWelcomeMessage { get; private set; } = config.Bind(
        "BetterOtherRoles.LocalSettings.Chat",
        "ShowWelcomeMessage",
        true,
        "Shows the welcome message in the chat of the first lobby after launching the game.");

    /// <summary>
    ///     Gets whether the summary of the game is shown in the chat when going back to the lobby
    ///     (TOU-Mira 1.7.9 ShowSummaryMessage, <see cref="Patches.Misc.LobbyChatPatches"/>).
    /// </summary>
    [LocalToggleSetting("BetterOtherRoles.LocalSettings.ShowLobbySummary", "BetterOtherRoles.LocalSettings.ShowLobbySummaryDescription")]
    public ConfigEntry<bool> ShowLobbySummary { get; private set; } = config.Bind(
        "BetterOtherRoles.LocalSettings.Chat",
        "ShowLobbySummary",
        true,
        "Shows the summary of the game in the chat when going back to the lobby.");

    /// <summary>
    ///     Gets how the summary is written in the lobby chat (TOU-Mira 1.7.9 SummaryMsgBreakdown): a short line, the line of
    ///     the end game screen, or a title and a list per player.
    /// </summary>
    [LocalEnumSetting(
        "BetterOtherRoles.LocalSettings.LobbySummaryFormat",
        "BetterOtherRoles.LocalSettings.LobbySummaryFormatDescription",
        [
            "BetterOtherRoles.LocalSettings.LobbySummaryFormat.Short",
            "BetterOtherRoles.LocalSettings.LobbySummaryFormat.Normal",
            "BetterOtherRoles.LocalSettings.LobbySummaryFormat.Complete",
        ])]
    public ConfigEntry<SummaryFormat> LobbySummaryFormat { get; private set; } = config.Bind(
        "BetterOtherRoles.LocalSettings.Chat",
        "LobbySummaryFormat",
        SummaryFormat.Complete,
        "How the summary is written in the lobby chat: short, normal or complete.");

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

    /// <summary>
    ///     Gets whether "(L)" or "(D)" is shown after the names in meetings, from the real color of each player
    ///     (BetterOtherRoles 1.7.1 ShowLighterDarker, <see cref="Patches.Misc.ColorTypePatches"/>).
    /// </summary>
    [LocalToggleSetting("BetterOtherRoles.LocalSettings.ShowColorType", "BetterOtherRoles.LocalSettings.ShowColorTypeDescription")]
    public ConfigEntry<bool> ShowColorType { get; private set; } = config.Bind(
        "BetterOtherRoles.LocalSettings.Players",
        "ShowColorType",
        true,
        "Shows (L) or (D) after the names in meetings: lighter or darker color.");

    /// <summary>
    ///     Gets whether a dead player sees the task counter of every crewmate (<see cref="Modules.Roles.BorGhostInfo"/>).
    /// </summary>
    [LocalToggleSetting("BetterOtherRoles.LocalSettings.GhostsSeeTasks", "BetterOtherRoles.LocalSettings.GhostsSeeTasksDescription")]
    public ConfigEntry<bool> GhostsSeeTasks { get; private set; } = config.Bind(
        "BetterOtherRoles.LocalSettings.Ghosts",
        "GhostsSeeTasks",
        true,
        "Once dead, you see the task counter of every crewmate.");

    /// <summary>
    ///     Gets whether a dead player sees the role of every player, with the name and in its color (<see cref="Modules.Roles.BorGhostInfo"/>).
    /// </summary>
    [LocalToggleSetting("BetterOtherRoles.LocalSettings.GhostsSeeRoles", "BetterOtherRoles.LocalSettings.GhostsSeeRolesDescription")]
    public ConfigEntry<bool> GhostsSeeRoles { get; private set; } = config.Bind(
        "BetterOtherRoles.LocalSettings.Ghosts",
        "GhostsSeeRoles",
        true,
        "Once dead, you see the role of every player.");

    /// <summary>
    ///     Gets whether a dead player sees who voted, even with anonymous votes (<see cref="Patches.Misc.GhostVotePatches"/>).
    /// </summary>
    [LocalToggleSetting("BetterOtherRoles.LocalSettings.GhostsSeeVotes", "BetterOtherRoles.LocalSettings.GhostsSeeVotesDescription")]
    public ConfigEntry<bool> GhostsSeeVotes { get; private set; } = config.Bind(
        "BetterOtherRoles.LocalSettings.Ghosts",
        "GhostsSeeVotes",
        true,
        "Once dead, you see who voted, even with anonymous votes.");

    /// <summary>
    ///     Gets whether a dead player keeps the tab of his role while alive, under the task list
    ///     (<see cref="Patches.Misc.GhostRoleTabPatches"/>).
    /// </summary>
    [LocalToggleSetting("BetterOtherRoles.LocalSettings.GhostsKeepRoleTab", "BetterOtherRoles.LocalSettings.GhostsKeepRoleTabDescription")]
    public ConfigEntry<bool> GhostsKeepRoleTab { get; private set; } = config.Bind(
        "BetterOtherRoles.LocalSettings.Ghosts",
        "GhostsKeepRoleTab",
        true,
        "Once dead, you keep the tab of your role under the task list.");

    /// <summary>
    ///     Gets whether the dead see all the players on the map, the dead ones faded, except the dead impostors
    ///     (BetterOtherRoles 1.7.1, <see cref="Patches.Misc.MapGhostPatches"/>).
    /// </summary>
    [LocalToggleSetting("BetterOtherRoles.LocalSettings.GhostsSeePlayersOnMap", "BetterOtherRoles.LocalSettings.GhostsSeePlayersOnMapDescription")]
    public ConfigEntry<bool> GhostsSeePlayersOnMap { get; private set; } = config.Bind(
        "BetterOtherRoles.LocalSettings.Ghosts",
        "GhostsSeePlayersOnMap",
        true,
        "Once dead, you see all the players on the map (not for the dead impostors).");

    /// <summary>
    ///     Gets whether the FPS counter is shown (TOU-Mira 1.7.9 ShowFpsCounter, <see cref="Modules.Display.BorFpsCounter"/>).
    /// </summary>
    [LocalToggleSetting("BetterOtherRoles.LocalSettings.ShowFps", "BetterOtherRoles.LocalSettings.ShowFpsDescription")]
    public ConfigEntry<bool> ShowFps { get; private set; } = config.Bind(
        "BetterOtherRoles.LocalSettings.Interface",
        "ShowFps",
        false,
        "Shows the frames per second next to the ping.");

    /// <summary>
    ///     Gets how the button cooldowns are shown: whole seconds as the game, tenths under 10 s
    ///     (TOU-Mira 1.7.9 PreciseCooldowns) or tenths everywhere (<see cref="Modules.Cooldowns.BorCooldownText"/>).
    /// </summary>
    [LocalEnumSetting(
        "BetterOtherRoles.LocalSettings.CooldownDisplay",
        "BetterOtherRoles.LocalSettings.CooldownDisplayDescription",
        [
            "BetterOtherRoles.LocalSettings.CooldownDisplay.Whole",
            "BetterOtherRoles.LocalSettings.CooldownDisplay.DecimalsUnder10",
            "BetterOtherRoles.LocalSettings.CooldownDisplay.Decimals",
        ])]
    public ConfigEntry<CooldownDisplay> CooldownDisplay { get; private set; } = config.Bind(
        "BetterOtherRoles.LocalSettings.Interface",
        "CooldownDisplay",
        LocalSettings.CooldownDisplay.DecimalsUnder10,
        "How the button cooldowns are shown: whole seconds, tenths under 10 s, or tenths everywhere.");

    /// <summary>
    ///     Gets whether the vents are shown on the map, in the color of their network (BetterOtherRoles 1.7.1
    ///     ShowVentsOnMap, <see cref="Patches.Misc.MapVentsPatches"/>).
    /// </summary>
    [LocalToggleSetting("BetterOtherRoles.LocalSettings.ShowVentsOnMap", "BetterOtherRoles.LocalSettings.ShowVentsOnMapDescription")]
    public ConfigEntry<bool> ShowVentsOnMap { get; private set; } = config.Bind(
        "BetterOtherRoles.LocalSettings.Interface",
        "ShowVentsOnMap",
        true,
        "Shows the vents on the map, in the color of their network.");

    /// <summary>
    ///     Gets whether the cams go to the next camera (Polus, Airship) or the next page of four (The Skeld with more than
    ///     four cams) by themselves (BetterOtherRoles 1.7.1, <see cref="Patches.Misc.CamPagingPatches"/>).
    /// </summary>
    [LocalToggleSetting("BetterOtherRoles.LocalSettings.AutoCams", "BetterOtherRoles.LocalSettings.AutoCamsDescription")]
    public ConfigEntry<bool> AutoCams { get; private set; } = config.Bind(
        "BetterOtherRoles.LocalSettings.Interface",
        "AutoCams",
        false,
        "The cams go to the next camera or page by themselves.");

    /// <summary>
    ///     Gets the delay before the cams go to the next camera or page, in seconds.
    /// </summary>
    [LocalSliderSetting(
        "BetterOtherRoles.LocalSettings.AutoCamsDelay",
        2f,
        10f,
        displayValue: true,
        formatString: "0",
        roundValue: true,
        suffixType: MiraNumberSuffixes.Seconds)]
    public ConfigEntry<float> AutoCamsDelay { get; private set; } = config.Bind(
        "BetterOtherRoles.LocalSettings.Interface",
        "AutoCamsDelay",
        3f,
        "Delay before the cams go to the next camera or page, in seconds (2 to 10).");

    /// <summary>
    ///     Gets how the summary is shown on the end game screen: full list on the left, two short columns or hidden
    ///     (TOU-Mira 1.7.9 EndSummaryVisibility, <see cref="Patches.Misc.EndGameSummaryPatches"/>).
    /// </summary>
    [LocalEnumSetting(
        "BetterOtherRoles.LocalSettings.EndSummary",
        "BetterOtherRoles.LocalSettings.EndSummaryDescription",
        [
            "BetterOtherRoles.LocalSettings.EndSummary.Hidden",
            "BetterOtherRoles.LocalSettings.EndSummary.Split",
            "BetterOtherRoles.LocalSettings.EndSummary.LeftSide",
        ])]
    public ConfigEntry<EndSummaryDisplay> EndSummary { get; private set; } = config.Bind(
        "BetterOtherRoles.LocalSettings.Interface",
        "EndSummary",
        EndSummaryDisplay.LeftSide,
        "How the summary is shown on the end game screen: hidden, two columns or full list on the left.");
}

/// <summary>
///     How the summary is shown on the end game screen.
/// </summary>
public enum EndSummaryDisplay
{
    Hidden,
    Split,
    LeftSide,
}

/// <summary>
///     How the summary is written in the chat: a short line per player (without the killer), the line of the end game
///     screen, or a title and a list per player.
/// </summary>
public enum SummaryFormat
{
    // The name is the value in the config file and the English argument of /summary.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Name of the format typed in /summary.")]
    Short,
    Normal,
    Complete,
}

/// <summary>
///     How the button cooldowns are shown.
/// </summary>
public enum CooldownDisplay
{
    Whole,
    DecimalsUnder10,
    Decimals,
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
