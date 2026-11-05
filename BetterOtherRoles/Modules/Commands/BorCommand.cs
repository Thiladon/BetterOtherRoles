using MiraAPI.Translation;

namespace BetterOtherRoles.Modules.Commands;

/// <summary>
///     Who can use a chat command.
/// </summary>
public enum CommandAccess
{
    /// <summary>Every player.</summary>
    Everyone,

    /// <summary>The host only.</summary>
    Host,

    /// <summary>The host with the admin key, in game or in Freeplay, with the Debug Mode (<see cref="BetterOtherRoles.Modules.Debug.BorDebugActions.CanUse"/>).</summary>
    Debug,
}

/// <summary>
///     Where a chat command can be used.
/// </summary>
public enum CommandPlace
{
    /// <summary>In the lobby and in game.</summary>
    Anywhere,

    /// <summary>In the lobby only.</summary>
    Lobby,

    /// <summary>In game (or in Freeplay) only.</summary>
    Game,
}

/// <summary>
///     A chat command ("/name arguments"): one class per command, in Modules/Commands, found by
///     <see cref="BorCommandManager"/> when the mod loads (nothing else to declare).
///     The command only reads its arguments and answers: what it does lives in the module it belongs to, shared with the
///     option or the menu doing the same thing.
///     Texts: BetterOtherRoles.Command.{Key}.Usage (arguments, after the name) and .Description, in Resources/Locale.
/// </summary>
public abstract class BorCommand
{
    /// <summary>
    ///     Gets the name typed after "/", in English and lower case, whatever the game language.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    ///     Gets who can use the command. The manager refuses the others.
    /// </summary>
    public virtual CommandAccess Access => CommandAccess.Everyone;

    /// <summary>
    ///     Gets where the command can be used. The manager refuses it elsewhere.
    /// </summary>
    public virtual CommandPlace Place => CommandPlace.Anywhere;

    /// <summary>
    ///     Gets a value indicating whether the command exists in this build: the Debug commands only in the dev builds
    ///     (<see cref="BetterOtherRolesPlugin.IsDevBuild"/>). Elsewhere, the command is unknown, as if it had no class.
    /// </summary>
    public virtual bool Exists => Access != CommandAccess.Debug || BetterOtherRolesPlugin.IsDevBuild;

    /// <summary>
    ///     Gets the key of the command texts (BetterOtherRoles.Command.{Key}.*).
    /// </summary>
    protected virtual string Key => char.ToUpperInvariant(Name[0]) + Name[1..];

    /// <summary>
    ///     Gets the arguments, shown after the name by /help and in the usage messages ("[player] {role}"), or empty.
    /// </summary>
    public virtual string Usage => MiraLocaleManager.Get($"BetterOtherRoles.Command.{Key}.Usage", string.Empty);

    /// <summary>
    ///     Gets what the command does, shown by /help.
    /// </summary>
    public virtual string Description => MiraLocaleManager.Get($"BetterOtherRoles.Command.{Key}.Description", string.Empty);

    /// <summary>
    ///     Gets the full syntax: "/name arguments".
    /// </summary>
    public string Syntax => Usage.Length == 0 ? "/" + Name : $"/{Name} {Usage}";

    /// <summary>
    ///     Runs the command, once the manager has checked who and where.
    /// </summary>
    /// <param name="args">The text after the name, trimmed (empty without arguments).</param>
    public abstract void Run(string args);

    /// <summary>
    ///     Answers with the usage message ("Usage: /name arguments").
    /// </summary>
    protected void ReplyUsage()
    {
        BorCommandUtils.Reply(MiraLocaleManager.Get("BetterOtherRoles.Command.Usage", "Usage: {0}").Replace("{0}", Syntax));
    }
}
