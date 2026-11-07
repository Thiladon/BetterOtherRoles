using BetterOtherRoles.Modules.Lobby;
using MiraAPI.Translation;

namespace BetterOtherRoles.Modules.Commands.Lobby;

/// <summary>
///     /setname {name} changes the name of the local player in the lobby (TOU-Mira 1.7.9 /setname): 1 to 12 characters,
///     no rich text tags (<see cref="BorNameCheck.IsAllowed"/>, the host would kick), not the name of another player.
///     Sent to the host as the game does (CmdCheckName, then RpcSetName by the host): kept until the end of the next
///     game, the name of the profile is not changed.
/// </summary>
public sealed class SetNameCommand : BorCommand
{
    private const int MaxLength = 12;

    public override string Name => "setname";

    public override CommandPlace Place => CommandPlace.Lobby;

    protected override string Key => "SetName";

    public override void Run(string args)
    {
        if (args.Length == 0)
        {
            ReplyUsage();
            return;
        }

        if (args.Length > MaxLength)
        {
            BorCommandUtils.Reply(MiraLocaleManager.Get("BetterOtherRoles.Command.SetName.TooLong", "The name cannot be longer than {0} characters.")
                .Replace("{0}", BorCommandUtils.ToText(MaxLength)));
            return;
        }

        if (!BorNameCheck.IsAllowed(args))
        {
            BorCommandUtils.Reply(MiraLocaleManager.Get("BetterOtherRoles.Command.SetName.Tags", "The characters < and > are not allowed."));
            return;
        }

        var local = PlayerControl.LocalPlayer;
        var taken = BorCommandUtils.GetPlayers()
            .Any(x => x != local && string.Equals(x.Data.PlayerName.Trim(), args, StringComparison.OrdinalIgnoreCase));
        if (taken)
        {
            BorCommandUtils.Reply(MiraLocaleManager.Get("BetterOtherRoles.Command.SetName.Taken", "Another player is already named {0}.")
                .Replace("{0}", args));
            return;
        }

        local.CmdCheckName(args);
        BorCommandUtils.Reply(MiraLocaleManager.Get("BetterOtherRoles.Command.SetName.Done", "Your name is now {0}, until the end of the next game.")
            .Replace("{0}", args));
    }
}
