using BetterOtherRoles.Modules.Lobby;
using BetterOtherRoles.Options;
using MiraAPI.GameOptions;
using MiraAPI.Translation;

namespace BetterOtherRoles.Modules.Commands.Lobby;

/// <summary>
///     /size shows the lobby size, /size N changes it (host only).
///     Linked to the option <see cref="LobbyOptions.MaxPlayers"/>: the command changes the option, which applies the size
///     (<see cref="BorLobbySize.Apply"/>), as from the settings menu; the limits are the option ones.
/// </summary>
public sealed class SizeCommand : BorCommand
{
    public override string Name => "size";

    public override CommandPlace Place => CommandPlace.Lobby;

    // Only the host can give a size: the other players see "/size" alone.
    public override string Usage => !AmongUsClient.Instance.AmHost
        ? string.Empty
        : base.Usage
            .Replace("{0}", BorCommandUtils.ToText(Min))
            .Replace("{1}", BorCommandUtils.ToText(Max));

    public override string Description => !AmongUsClient.Instance.AmHost
        ? MiraLocaleManager.Get("BetterOtherRoles.Command.Size.DescriptionPlayer", "shows the lobby size.")
        : base.Description;

    private static int Min => (int)OptionGroupSingleton<LobbyOptions>.Instance.MaxPlayers.Min;

    private static int Max => (int)OptionGroupSingleton<LobbyOptions>.Instance.MaxPlayers.Max;

    public override void Run(string args)
    {
        var option = OptionGroupSingleton<LobbyOptions>.Instance.MaxPlayers;
        var current = (int)option.Value;
        if (args.Length == 0)
        {
            BorCommandUtils.Reply(MiraLocaleManager.Get("BetterOtherRoles.Command.Size.Current", "Lobby size: {0} players.")
                .Replace("{0}", BorCommandUtils.ToText(current)));
            return;
        }

        if (!AmongUsClient.Instance.AmHost)
        {
            BorCommandUtils.Reply(MiraLocaleManager.Get("BetterOtherRoles.Command.Size.HostOnly", "Only the host can change the lobby size."));
            return;
        }

        if (!int.TryParse(args, out var size) || size < Min || size > Max)
        {
            ReplyUsage();
            return;
        }

        if (size == current)
        {
            BorCommandUtils.Reply(MiraLocaleManager.Get("BetterOtherRoles.Command.Size.Already", "The lobby size is already {0} players.")
                .Replace("{0}", BorCommandUtils.ToText(size)));
            return;
        }

        // Sent to the players and saved by MiraAPI; the option then writes it to the vanilla settings.
        option.SetValue(size);
        BorCommandUtils.Reply(MiraLocaleManager.Get("BetterOtherRoles.Command.Size.Changed", "Lobby size changed to {0} players.")
            .Replace("{0}", BorCommandUtils.ToText(size)));
    }
}
