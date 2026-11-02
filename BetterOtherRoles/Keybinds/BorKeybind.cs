using MiraAPI.Keybinds;
using Rewired;

namespace BetterOtherRoles.Keybinds;

/// <summary>
///     Keybind of a role action. Its key can be shared with actions of other roles (only one of them is on screen),
///     but not with an action of the same role nor with a universal action (see <see cref="BorKeybindRules"/>).
/// </summary>
public sealed class BorKeybind : MiraKeybind
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="BorKeybind"/> class.
    /// </summary>
    /// <param name="id">The Rewired action name (unique, saved with the player's mappings).</param>
    /// <param name="nameKey">The locale key of the action name.</param>
    /// <param name="defaultKey">The default keyboard key.</param>
    /// <param name="defaultControllerButton">The default controller button.</param>
    /// <param name="roles">The roles that can have this action; the first one names its group in Remap Controls.</param>
    public BorKeybind(string id, string nameKey, KeyboardKeyCode defaultKey, ControllerButton defaultControllerButton, params Type[] roles)
        : base(id, defaultKey, defaultControllerButton)
    {
        NameKey = nameKey;
        Roles = roles;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="BorKeybind"/> class, with no controller button by default.
    /// </summary>
    /// <param name="id">The Rewired action name (unique, saved with the player's mappings).</param>
    /// <param name="nameKey">The locale key of the action name.</param>
    /// <param name="defaultKey">The default keyboard key.</param>
    /// <param name="roles">The roles that can have this action; the first one names its group in Remap Controls.</param>
    public BorKeybind(string id, string nameKey, KeyboardKeyCode defaultKey, params Type[] roles)
        : base(id, defaultKey)
    {
        NameKey = nameKey;
        Roles = roles;
    }

    /// <summary>
    ///     Gets the locale key of the action name.
    /// </summary>
    public string NameKey { get; }

    /// <summary>
    ///     Gets the roles that can have this action. Empty for a universal action.
    /// </summary>
    public IReadOnlyList<Type> Roles { get; }
}
