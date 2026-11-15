using BetterOtherRoles.Patches.Options;
using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Translation;

namespace BetterOtherRoles.Options;

/// <summary>
///     Chat options: the impostor chat (<see cref="Modules.Chat.BorImpostorChat"/>).
/// </summary>
public sealed class ChatOptions : AbstractOptionGroup
{
    public override string GroupName => MiraLocaleManager.Get("BetterOtherRoles.Options.Groups.Chat", "Chat");

    public ChatOptions()
    {
        ImpostorChatSeparate.SetNotificationParent(ImpostorChat);
    }

    public override uint GroupPriority => 6;

    /// <summary>
    ///     Gets when the impostors have their own chat (<see cref="Modules.Chat.BorImpostorChat"/>): never, in meetings,
    ///     or during the whole game (<see cref="ImpostorChatMode"/>).
    /// </summary>
    public ModdedEnumOption ImpostorChat { get; } =
        new("BetterOtherRoles.Option.Gameplay.ImpostorChat", (int)ImpostorChatMode.Meetings, typeof(ImpostorChatMode),
            ["BetterOtherRoles.Option.Gameplay.ImpostorChatOff", "BetterOtherRoles.Option.Gameplay.ImpostorChatMeetings", "BetterOtherRoles.Option.Gameplay.ImpostorChatWholeGame"]);

    /// <summary>
    ///     Gets whether the impostor chat is a chat of its own (red button, own list). Off: out of meetings, a living
    ///     impostor writes in the chat of the game, sent to the impostors only (red bubbles in the public list); in
    ///     meetings, everyone has the public chat. Only for the whole game mode (off in meetings only would leave no
    ///     impostor chat).
    /// </summary>
    public ModdedToggleOption ImpostorChatSeparate { get; } =
        new("BetterOtherRoles.Option.Gameplay.ImpostorChatSeparate", true)
        {
            Visible = () => OptionGroupSingleton<ChatOptions>.Instance.ImpostorChat.Value == (int)ImpostorChatMode.WholeGame,
        };
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
