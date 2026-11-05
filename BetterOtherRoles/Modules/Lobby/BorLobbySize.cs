using AmongUs.GameOptions;
using BetterOtherRoles.Options;
using HarmonyLib;
using Hazel;
using InnerNet;
using MiraAPI.GameOptions;

namespace BetterOtherRoles.Modules.Lobby;

/// <summary>
///     Lobby size changed after the lobby creation, from BetterOtherRoles 1.7.1 (DynamicLobbies): the server keeps the
///     lobby size given at its creation, so it always gets 15 places, and the host refuses the players beyond the size
///     chosen (<see cref="LobbyOptions.MaxPlayers"/>, also changed by /size N: <see cref="Commands.Lobby.SizeCommand"/>). The players already in the lobby stay.
///     Online games only: local games keep the vanilla behavior (lobby counter and games list not updated, no refusal).
/// </summary>
[HarmonyPatch]
public static class BorLobbySize
{
    public const float MinSize = 4f;
    public const float MaxSize = 15f;

    private static int _creationSize = (int)MaxSize;

    // Set when a lobby is created, cleared once the option has the size chosen on the creation screen.
    private static bool _pendingCreationSize;

    /// <summary>
    ///     Writes the size to the vanilla settings (player counter of the lobby, settings view) and sends it to the
    ///     players. Host only, once in the lobby: called by the option on every change.
    /// </summary>
    public static void Apply(int size)
    {
        if (!AmongUsClient.Instance || !AmongUsClient.Instance.AmHost || !PlayerControl.LocalPlayer ||
            GameOptionsManager.Instance == null)
            return;

        var options = GameOptionsManager.Instance.CurrentGameOptions;
        if (options.MaxPlayers == size) return;

        options.SetInt(Int32OptionNames.MaxPlayers, size);
        if (GameManager.Instance)
            GameManager.Instance.LogicOptions.SyncOptions();

        // The player counter is only rewritten when the player count changes: force it.
        if (GameStartManager.InstanceExists)
            GameStartManager.Instance.LastPlayerCount = -1;
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Lobby creation: 15 places on the server, the size chosen kept in the option
    // ---------------------------------------------------------------------------------------------------------------

    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.HostGame))]
    [HarmonyPrefix]
    public static void HostGamePrefix([HarmonyArgument(0)] IGameOptions settings)
    {
        _creationSize = Math.Clamp(settings.MaxPlayers, (int)MinSize, (int)MaxSize);
        _pendingCreationSize = true;
        settings.SetInt(Int32OptionNames.MaxPlayers, (int)MaxSize);
    }

    /// <summary>
    ///     Gives the option the size chosen on the creation screen, once in the lobby: MiraAPI shows a notification on every
    ///     change, which needs the HUD (missing while the lobby is created: the creation failed).
    /// </summary>
    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Update))]
    [HarmonyPostfix]
    public static void GameStartManagerUpdatePostfix()
    {
        if (_pendingCreationSize && HudManager.InstanceExists && AmongUsClient.Instance && AmongUsClient.Instance.AmHost &&
            PlayerControl.LocalPlayer)
        {
            _pendingCreationSize = false;
            OptionGroupSingleton<LobbyOptions>.Instance.MaxPlayers.SetValue(_creationSize);
        }
    }

    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.HostGame))]
    [HarmonyPostfix]
    public static void HostGamePostfix([HarmonyArgument(0)] IGameOptions settings)
    {
        settings.SetInt(Int32OptionNames.MaxPlayers, _creationSize);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Players beyond the size: refused as in a full game
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>
    ///     Online games only: the local game server (run by the host) does not take this removal, the player stayed in the
    ///     lobby. Local games keep the vanilla behavior.
    /// </summary>
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerJoined))]
    [HarmonyPrefix]
    public static bool OnPlayerJoinedPrefix(AmongUsClient __instance, [HarmonyArgument(0)] ClientData client)
    {
        if (!__instance.AmHost || __instance.NetworkMode != NetworkModes.OnlineGame ||
            __instance.GameState == InnerNetClient.GameStates.Started)
            return true;

        // allClients already holds the new player.
        var size = (int)OptionGroupSingleton<LobbyOptions>.Instance.MaxPlayers.Value;
        if (__instance.allClients.Count <= size) return true;

        Info($"Lobby full ({size} players): {client.PlayerName} refused.");
        RemoveAsGameFull(__instance, client.Id);
        return false;
    }

    // Removes the player with the "game full" reason (BetterOtherRoles 1.7.1): the player sees the vanilla message.
    private static void RemoveAsGameFull(InnerNetClient client, int clientId)
    {
        var writer = MessageWriter.Get(SendOption.Reliable);
        writer.StartMessage(4);
        writer.Write(client.GameId);
        writer.WritePacked(clientId);
        writer.Write((byte)DisconnectReasons.GameFull);
        writer.EndMessage();
        client.SendOrDisconnect(writer);
        writer.Recycle();
    }
}
