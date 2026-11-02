using BetterOtherRoles.Roles.Crewmate;
using MiraAPI.Keybinds;
using Rewired;

namespace BetterOtherRoles.Keybinds;

/// <summary>
///     BetterOtherRoles keybinds, registered by MiraAPI in Rewired (menu Remap Controls, keyboard and controller).
///     Killing actions have no keybind of their own: they use the vanilla Kill key (see <see cref="BorKeybindRules"/>).
///     Defaults follow BetterOtherRoles 1.7.1: abilities on the role ability key (F / Right trigger),
///     then G / East for a second ability.
///     Rewired saves the player's mappings by action id, given in this order: always add new keybinds at the end.
/// </summary>
[RegisterCustomKeybinds]
public static class BorKeybinds
{
    /// <summary>
    ///     Gets the Deputy handcuff keybind (also used by a promoted Sheriff who keeps the handcuffs).
    /// </summary>
    public static MiraKeybind DeputyHandcuff { get; } = new BorKeybind(
        "bor_deputy_handcuff",
        "BetterOtherRoles.Role.DeputyHandcuff",
        KeyboardKeyCode.F,
        ControllerButton.RightTrigger,
        typeof(DeputyRole),
        typeof(SheriffRole));

    /// <summary>
    ///     Gets the keybind opening (or closing) the Debug menu, shown in Remap Controls to the admins only.
    /// </summary>
    public static MiraKeybind DebugMenu { get; } = new BorKeybind(
        "bor_debug_menu",
        "BetterOtherRoles.Debug.Menu.Keybind",
        KeyboardKeyCode.F8);
}
