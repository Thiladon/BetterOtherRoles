using System.Collections;
using System.Reflection;
using BetterOtherRoles.Utilities;
using HarmonyLib;
using InnerNet;
using MiraAPI.Translation;
using Reactor.Networking.Attributes;
using Reactor.Networking.Extensions;
using Reactor.Utilities;
using UnityEngine;

namespace BetterOtherRoles.Modules.Lobby;

/// <summary>
///     Same build for every player (BetterOtherRoles 1.7.1 VersionHandshake, build part only).
///     Reactor already kicks a player without the mod or with another version (RequireOnAllClients, checked by the host),
///     but two builds of the same version (two alpha compilations) look the same to it: each player sends the id of its
///     build (<see cref="Module.ModuleVersionId"/>, new at each compilation) when it appears in the lobby, and the host
///     kicks a player with another build, with a reason shown to that player in its language (the player leaves by itself,
///     <see cref="RpcBuildMismatch"/>; kicked by the host after 3 s if still there), as Reactor does for a wrong version.
///     The host does not start the game until every player has sent its build.
/// </summary>
[HarmonyPatch]
public static class BorBuildCheck
{
    /// <summary>
    ///     Gets the id of this build.
    /// </summary>
    public static Guid LocalBuild { get; } = typeof(BorBuildCheck).Assembly.ManifestModule.ModuleVersionId;

    // Build of each client of the lobby, the local one included.
    private static readonly Dictionary<int, Guid> Builds = new();

    // Reactor's kick with a reason (internal there): the kicked player sees the reason, as for a wrong version.
    private static readonly MethodInfo? KickWithReasonMethod =
        AccessTools.Method("Reactor.Networking.Extensions.ClientExtensions:KickWithReason");

    /// <summary>
    ///     Gets the short form of a build id, shown to the players.
    /// </summary>
    /// <param name="build">The build id.</param>
    /// <returns>Its first 8 characters.</returns>
    public static string ToShort(Guid build) => build.ToString("N")[..8];

    /// <summary>
    ///     Gets the build sent by a client, if any.
    /// </summary>
    /// <param name="clientId">The client id.</param>
    /// <param name="build">The build id.</param>
    /// <returns>True when the client has sent its build.</returns>
    public static bool TryGetBuild(int clientId, out Guid build) => Builds.TryGetValue(clientId, out build);

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
    [HarmonyPostfix]
    public static void OnGameJoinedPostfix()
    {
        Builds.Clear();
    }

    /// <summary>
    ///     The local player sends its build once its PlayerControl exists (lobby start, join, back from a game).
    /// </summary>
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Start))]
    [HarmonyPostfix]
    public static void PlayerStartPostfix(PlayerControl __instance)
    {
        if (__instance.AmOwner && LobbyBehaviour.Instance)
        {
            Builds[__instance.OwnerId] = LocalBuild;
            RpcSendBuild(__instance, LocalBuild.ToString("N"));
        }
    }

    [MethodRpc((uint)BorRpc.BuildCheck)]
    public static void RpcSendBuild(PlayerControl player, string build)
    {
        if (!player || !Guid.TryParse(build, out var guid))
        {
            return;
        }

        Builds[player.OwnerId] = guid;
        if (!AmongUsClient.Instance.AmHost || player.AmOwner || guid == LocalBuild)
        {
            return;
        }

        var name = player.Data?.PlayerName ?? "?";
        Warning($"Kicking {name}: build {ToShort(guid)}, host build {ToShort(LocalBuild)}.");
        BorChat.AddSystemMessage(MiraLocaleManager.Get("BetterOtherRoles.Lobby.Build.Kicked", "{0} was kicked: not the same build of the mod.")
            .Replace("{0}", name));

        // The player leaves by itself, with the reason in its own language; kicked by the host if it is still there.
        var clientId = player.OwnerId;
        RpcBuildMismatch(PlayerControl.LocalPlayer, clientId, LocalBuild.ToString("N"));
        Coroutines.Start(CoKickLater(clientId, guid));
    }

    /// <summary>
    ///     Tells a player that its build is not the host's one: it leaves the lobby with the reason in its language.
    /// </summary>
    [MethodRpc((uint)BorRpc.BuildMismatch)]
    public static void RpcBuildMismatch(PlayerControl host, int clientId, string hostBuild)
    {
        if (AmongUsClient.Instance.ClientId != clientId || AmongUsClient.Instance.AmHost || !Guid.TryParse(hostBuild, out var guid))
        {
            return;
        }

        AmongUsClient.Instance.DisconnectWithReason(GetKickReason(LocalBuild, guid));
    }

    // Reason shown to the kicked player.
    private static string GetKickReason(Guid playerBuild, Guid hostBuild)
    {
        return MiraLocaleManager.Get("BetterOtherRoles.Lobby.Build.KickReason",
                "BetterOtherRoles: your build of the mod ({0}) is not the host's build ({1}).\nInstall the same version as the host.")
            .Replace("{0}", ToShort(playerBuild))
            .Replace("{1}", ToShort(hostBuild));
    }

    // A player that did not leave (older build, without RpcBuildMismatch) is kicked with the reason in the host's language.
    private static IEnumerator CoKickLater(int clientId, Guid playerBuild)
    {
        yield return new WaitForSeconds(3f);
        if (!AmongUsClient.Instance || !AmongUsClient.Instance.AmHost || AmongUsClient.Instance.FindClientById(clientId) == null)
        {
            yield break;
        }

        var reason = GetKickReason(playerBuild, LocalBuild);
        if (KickWithReasonMethod != null)
        {
            KickWithReasonMethod.Invoke(null, [AmongUsClient.Instance, clientId, reason]);
        }
        else
        {
            AmongUsClient.Instance.KickPlayer(clientId, false);
        }
    }

    /// <summary>
    ///     No game start while a player has not sent its build yet.
    /// </summary>
    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.BeginGame))]
    [HarmonyPrefix]
    public static bool BeginGamePrefix(GameStartManager __instance)
    {
        if (!AmongUsClient.Instance.AmHost || __instance.startState != GameStartManager.StartingStates.NotStarting)
        {
            return true;
        }

        var waiting = AmongUsClient.Instance.allClients.ToArray()
            .Where(x => x.Id != AmongUsClient.Instance.ClientId && x.Character && !Builds.ContainsKey(x.Id))
            .Select(x => x.PlayerName)
            .ToArray();
        if (waiting.Length == 0)
        {
            return true;
        }

        BorChat.AddSystemMessage(MiraLocaleManager.Get("BetterOtherRoles.Lobby.Build.Waiting", "Cannot start: waiting for the build of {0}.")
            .Replace("{0}", string.Join(", ", waiting)));
        return false;
    }
}
