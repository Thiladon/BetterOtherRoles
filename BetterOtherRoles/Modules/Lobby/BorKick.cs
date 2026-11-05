using System.Collections;
using System.Reflection;
using BetterOtherRoles.Utilities;
using HarmonyLib;
using MiraAPI.Translation;
using Reactor.Networking.Attributes;
using Reactor.Networking.Extensions;
using Reactor.Utilities;
using UnityEngine;

namespace BetterOtherRoles.Modules.Lobby;

/// <summary>
///     Kick of a player by the host, with a reason shown to that player in its own language, as Reactor does for a wrong
///     version: the host tells the player the reason key (<see cref="RpcKick"/>), the player leaves the lobby by itself
///     with the translated reason, and the host kicks it after 3 s if it is still there (modified client, old build),
///     with the reason in the host's language.
/// </summary>
public static class BorKick
{
    private const float FallbackDelay = 3f;
    private const char ArgSeparator = '\u001F';

    // Reactor's kick with a reason (internal there): the kicked player sees the reason.
    private static readonly MethodInfo? KickWithReasonMethod =
        AccessTools.Method("Reactor.Networking.Extensions.ClientExtensions:KickWithReason");

    /// <summary>
    ///     Kicks a player (host only), with a message of the mod in the host's chat.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="reasonKey">Key of the reason shown to the player; {0}, {1}... replaced by <paramref name="args"/>.</param>
    /// <param name="hostMessage">Message of the mod in the host's chat, already translated.</param>
    /// <param name="args">Values of the reason, the same for both languages.</param>
    public static void Kick(PlayerControl player, string reasonKey, string hostMessage, params string[] args)
    {
        if (!AmongUsClient.Instance.AmHost || !player || player.AmOwner) return;

        var clientId = player.OwnerId;
        Warning($"Kicking client {clientId} ({player.Data?.PlayerName}): {reasonKey}.");
        BorChat.AddSystemMessage(hostMessage);
        RpcKick(PlayerControl.LocalPlayer, clientId, reasonKey, string.Join(ArgSeparator, args));
        Coroutines.Start(CoKickLater(clientId, reasonKey, args));
    }

    /// <summary>
    ///     Tells a player it is kicked: it leaves the lobby with the reason in its language.
    /// </summary>
    [MethodRpc((uint)BorRpc.Kick)]
    public static void RpcKick(PlayerControl host, int clientId, string reasonKey, string args)
    {
        if (AmongUsClient.Instance.ClientId != clientId || AmongUsClient.Instance.AmHost) return;

        AmongUsClient.Instance.DisconnectWithReason(Format(reasonKey, args.Length == 0 ? [] : args.Split(ArgSeparator)));
    }

    private static string Format(string reasonKey, string[] args)
    {
        var text = MiraLocaleManager.Get(reasonKey, reasonKey);
        for (var i = 0; i < args.Length; i++)
        {
            text = text.Replace("{" + i + "}", args[i]);
        }

        return text;
    }

    private static IEnumerator CoKickLater(int clientId, string reasonKey, string[] args)
    {
        yield return new WaitForSeconds(FallbackDelay);
        if (!AmongUsClient.Instance || !AmongUsClient.Instance.AmHost || AmongUsClient.Instance.FindClientById(clientId) == null)
            yield break;

        if (KickWithReasonMethod != null)
        {
            KickWithReasonMethod.Invoke(null, [AmongUsClient.Instance, clientId, Format(reasonKey, args)]);
        }
        else
        {
            AmongUsClient.Instance.KickPlayer(clientId, false);
        }
    }
}
