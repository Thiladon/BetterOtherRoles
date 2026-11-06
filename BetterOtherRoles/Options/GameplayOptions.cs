using BetterOtherRoles.Modules.Cooldowns;
using BetterOtherRoles.Patches.Options;
using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Translation;

namespace BetterOtherRoles.Options;

/// <summary>
///     Game options: how the game plays, whatever the roles.
/// </summary>
public sealed class GameplayOptions : AbstractOptionGroup
{
    public override string GroupName => MiraLocaleManager.Get("BetterOtherRoles.Options.Groups.Gameplay", "Gameplay");

    public GameplayOptions()
    {
        // Lobby notification: "Cooldowns Stop In Minigames > Impostors set to Off".
        PauseCooldownsCrewmates.SetNotificationParent(PauseCooldownsInMinigames);
        PauseCooldownsImpostors.SetNotificationParent(PauseCooldownsInMinigames);
        PauseCooldownsNeutrals.SetNotificationParent(PauseCooldownsInMinigames);
    }

    public override uint GroupPriority => 3;

    /// <summary>
    ///     Gets whether the cooldowns stop in minigames and at the admin table (BetterOtherRoles 1.7.1), for the
    ///     factions chosen below. Off: they keep running for everyone (vanilla, TOU-Mira 1.7.9 TickCooldownsInMinigame).
    ///     Always paused in a vent, whatever this option (<see cref="BorCooldowns"/>).
    /// </summary>
    public ModdedToggleOption PauseCooldownsInMinigames { get; } =
        new("BetterOtherRoles.Option.Gameplay.PauseCooldownsInMinigames", true);

    public ModdedToggleOption PauseCooldownsCrewmates { get; } =
        new("BetterOtherRoles.Option.Gameplay.PauseCooldownsCrewmates", true)
        {
            Visible = () => OptionGroupSingleton<GameplayOptions>.Instance.PauseCooldownsInMinigames.Value,
        };

    public ModdedToggleOption PauseCooldownsImpostors { get; } =
        new("BetterOtherRoles.Option.Gameplay.PauseCooldownsImpostors", true)
        {
            Visible = () => OptionGroupSingleton<GameplayOptions>.Instance.PauseCooldownsInMinigames.Value,
        };

    public ModdedToggleOption PauseCooldownsNeutrals { get; } =
        new("BetterOtherRoles.Option.Gameplay.PauseCooldownsNeutrals", true)
        {
            Visible = () => OptionGroupSingleton<GameplayOptions>.Instance.PauseCooldownsInMinigames.Value,
        };

    /// <summary>
    ///     Gets when the impostors have their own chat (<see cref="Modules.Chat.BorImpostorChat"/>): never, in meetings,
    ///     or during the whole game (<see cref="ImpostorChatMode"/>).
    /// </summary>
    public ModdedEnumOption ImpostorChat { get; } =
        new("BetterOtherRoles.Option.Gameplay.ImpostorChat", (int)ImpostorChatMode.Meetings, typeof(ImpostorChatMode),
            ["BetterOtherRoles.Option.Gameplay.ImpostorChatOff", "BetterOtherRoles.Option.Gameplay.ImpostorChatMeetings", "BetterOtherRoles.Option.Gameplay.ImpostorChatWholeGame"]);
}

/// <summary>
///     When the impostors have their own chat.
/// </summary>
public enum ImpostorChatMode
{
    Off,
    Meetings,
    WholeGame,
}
