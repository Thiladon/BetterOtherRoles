using System.Text;
using MiraAPI.Translation;

namespace BetterOtherRoles.Modules.Commands;

/// <summary>
///     /help lists the commands the player can use where they are (lobby or game, host, Debug Mode);
///     /help {command} shows one command.
/// </summary>
public sealed class HelpCommand : BorCommand
{
    public override string Name => "help";

    public override void Run(string args)
    {
        if (args.Length > 0)
        {
            var command = BorCommandManager.Find(args.TrimStart('/'));
            if (command == null)
            {
                BorCommandUtils.Reply(MiraLocaleManager.Get("BetterOtherRoles.Command.Unknown", "Unknown command: /{0}. Type /help for the list.")
                    .Replace("{0}", args.TrimStart('/')));
                return;
            }

            BorCommandUtils.Reply(Describe(command));
            return;
        }

        var builder = new StringBuilder(MiraLocaleManager.Get("BetterOtherRoles.Command.Help.Title", "Commands:"));
        foreach (var command in BorCommandManager.AvailableCommands)
        {
            builder.Append('\n').Append(Describe(command));
        }

        BorCommandUtils.Reply(builder.ToString());
    }

    private static string Describe(BorCommand command)
    {
        return command.Description.Length == 0
            ? command.Syntax
            : MiraLocaleManager.Get("BetterOtherRoles.Command.Help.Line", "{0}: {1}")
                .Replace("{0}", command.Syntax)
                .Replace("{1}", command.Description);
    }
}
