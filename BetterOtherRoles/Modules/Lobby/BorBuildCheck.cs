using BetterOtherRoles.Utilities;
using HarmonyLib;
using InnerNet;
using MiraAPI.Translation;
using Reactor.Networking.Attributes;

namespace BetterOtherRoles.Modules.Lobby;

/// <summary>
///     Same build for every player (BetterOtherRoles 1.7.1 VersionHandshake, build part only).
///     Reactor already kicks a player without the mod or with another version (RequireOnAllClients, checked by the host),
///     but two builds of the same version (two alpha compilations) look the same to it: each player sends the id of its
///     build (<see cref="System.Reflection.Module.ModuleVersionId"/>, new at each compilation) when it appears in the lobby, and the host
///     kicks a player with another build, with a reason shown to that player in its language (<see cref="BorKick"/>),
///     as Reactor does for a wrong version.
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
        if (!player || !Guid.TryParse(build, out var guid)) return;

        Builds[player.OwnerId] = guid;
        if (!AmongUsClient.Instance.AmHost || player.AmOwner || guid == LocalBuild) return;

        var name = player.Data?.PlayerName ?? "?";
        BorKick.Kick(player, "BetterOtherRoles.Lobby.Build.KickReason",
            MiraLocaleManager.Get("BetterOtherRoles.Lobby.Build.Kicked", "{0} was kicked: not the same build of the mod.").Replace("{0}", name),
            ToShort(guid), ToShort(LocalBuild));
    }

    /// <summary>
    ///     No game start while a player has not sent its build yet.
    /// </summary>
    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.BeginGame))]
    [HarmonyPrefix]
    public static bool BeginGamePrefix(GameStartManager __instance)
    {
        if (!AmongUsClient.Instance.AmHost || __instance.startState != GameStartManager.StartingStates.NotStarting)
            return true;

        var waiting = AmongUsClient.Instance.allClients.ToArray()
            .Where(x => x.Id != AmongUsClient.Instance.ClientId && x.Character && !Builds.ContainsKey(x.Id))
            .Select(x => x.PlayerName)
            .ToArray();
        if (waiting.Length == 0) return true;

        BorChat.AddSystemMessage(MiraLocaleManager.Get("BetterOtherRoles.Lobby.Build.Waiting", "Cannot start: waiting for the build of {0}.")
            .Replace("{0}", string.Join(", ", waiting)));
        return false;
    }
}
