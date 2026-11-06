using System.Reflection;
using BetterOtherRoles.Modules.Debug;
using BetterOtherRoles.Patches.Misc;
using HarmonyLib;
using MiraAPI.Translation;

namespace BetterOtherRoles.Modules.Commands;

/// <summary>
///     Chat commands: the only patch of ChatController.SendChat for them. A message starting with "/" never reaches the
///     other players: the command runs if it exists and if the player can use it here, or the player gets an answer
///     saying why (unknown command, host only, lobby only...).
///     The commands are the <see cref="BorCommand"/> classes of the mod, found once by reflection.
/// </summary>
[HarmonyPatch]
public static class BorCommandManager
{
    private static BorCommand[]? _commands;

    /// <summary>
    ///     Gets every command of this build (<see cref="BorCommand.Exists"/>), sorted by name.
    /// </summary>
    public static IReadOnlyList<BorCommand> Commands => _commands ??= FindCommands();

    /// <summary>
    ///     Gets the commands the local player can use where they are, sorted by name (/help).
    /// </summary>
    public static IEnumerable<BorCommand> AvailableCommands => Commands.Where(x => GetRefusal(x) == null);

    /// <summary>
    ///     Finds a command by its name, without the "/" (any case).
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The command, or null.</returns>
    public static BorCommand? Find(string name)
    {
        return Commands.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.SendChat))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static bool SendChatPrefix(ChatController __instance)
    {
        var text = __instance.freeChatField.Text.Trim();
        ChatInputPatches.AddToHistory(text);
        if (!text.StartsWith('/')) return true;

        var space = text.IndexOfAny([' ', '\t']);
        var name = space < 0 ? text[1..] : text[1..space];
        var args = space < 0 ? string.Empty : text[(space + 1)..].Trim();
        RunCommand(name, args);

        __instance.freeChatField.Clear();
        __instance.quickChatMenu.Clear();
        __instance.quickChatField.Clear();
        __instance.UpdateChatMode();
        return false;
    }

    private static void RunCommand(string name, string args)
    {
        var command = Find(name);
        if (command == null)
        {
            BorCommandUtils.Reply(MiraLocaleManager.Get("BetterOtherRoles.Command.Unknown", "Unknown command: /{0}. Type /help for the list.")
                .Replace("{0}", name));
            return;
        }

        var refusal = GetRefusal(command);
        if (refusal != null)
        {
            BorCommandUtils.Reply(refusal);
            return;
        }

        try
        {
            command.Run(args);
        }
        catch (Exception e)
        {
            Error($"Command /{command.Name} {args}: {e}");
            BorCommandUtils.Reply(MiraLocaleManager.Get("BetterOtherRoles.Command.Failed", "The command failed (see the BepInEx log)."));
        }
    }

    /// <summary>
    ///     Why the local player cannot use the command here, or null if they can.
    /// </summary>
    private static string? GetRefusal(BorCommand command)
    {
        switch (command.Access)
        {
            case CommandAccess.Debug when !BorDebugActions.CanUse:
                return MiraLocaleManager.Get("BetterOtherRoles.Command.DebugOnly", "Command only available to the host, in game, with the Debug Mode.");
            case CommandAccess.Host when !AmongUsClient.Instance.AmHost:
                return MiraLocaleManager.Get("BetterOtherRoles.Command.HostOnly", "Command only available to the host.");
        }

        var inGame = AmongUsClient.Instance.IsGameStarted || TutorialManager.InstanceExists;
        return command.Place switch
        {
            CommandPlace.Lobby when inGame || !LobbyBehaviour.Instance =>
                MiraLocaleManager.Get("BetterOtherRoles.Command.LobbyOnly", "Command only available in the lobby."),
            CommandPlace.Game when !inGame =>
                MiraLocaleManager.Get("BetterOtherRoles.Command.GameOnly", "Command only available in game."),
            _ => null,
        };
    }

    private static BorCommand[] FindCommands()
    {
        Type[] types;
        try
        {
            types = Assembly.GetExecutingAssembly().GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            // A type using a missing optional dependency: the other ones are still given.
            types = e.Types.Where(x => x != null).ToArray()!;
        }

        var commands = types
            .Where(x => x.IsSubclassOf(typeof(BorCommand)) && !x.IsAbstract)
            .Select(x => (BorCommand)Activator.CreateInstance(x)!)
            .Where(x => x.Exists)
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToArray();

        foreach (var duplicate in commands.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1))
        {
            Warning($"Command /{duplicate.Key} declared {duplicate.Count()} times: only the first one runs.");
        }

        Info($"Chat commands: {string.Join(", ", commands.Select(x => "/" + x.Name))}.");
        return commands;
    }
}
