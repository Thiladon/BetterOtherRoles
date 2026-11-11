using System.Reflection;
using HarmonyLib;

namespace BetterOtherRoles.Patches.Compatibility;

/// <summary>
///     Hides the popup of Reactor on a server without its modded handshake (Modded EU, ...): "This server doesn't
///     support Reactor's modded handshake. The lobbies shown could be incompatible with your current mods."
///     Only the filtering of the lobby list by the server is missing: in the lobby, Reactor still kicks a player without
///     the mod or with another version, and <see cref="Modules.Lobby.BorBuildCheck"/> one with another build.
///     Reactor only lets the text be changed (ReactorConfig.HandshakePopupMessage, empty = its own), and still writes
///     a warning in the log.
/// </summary>
[HarmonyPatch]
public static class ReactorHandshakePopupPatch
{
    public static bool Prepare() => TargetMethod() != null;

    public static MethodBase? TargetMethod() =>
        AccessTools.Method(AccessTools.TypeByName("Reactor.Networking.HandshakePopup"), "Show");

    // Harmony prefix: false skips the original method. Show returns nothing (no __result): the original method is
    // asked for, a parameter Harmony fills for any method.
    public static bool Prefix(MethodBase __originalMethod) => false;
}
