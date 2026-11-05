using AmongUs.GameOptions;
using HarmonyLib;

namespace BetterOtherRoles.Patches.Options;

/// <summary>
///     The vanilla settings (settings.amogus) are shared with the other installations of the game: another mod can
///     save a map this version does not have (6, seen after a game of BetterOtherRoles 1.7.0). The lobby then shows
///     The Fungle and GameStartManager.Update throws every frame (start button gone).
///     The host goes back to The Skeld when its map does not exist.
/// </summary>
[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Start))]
public static class InvalidMapPatches
{
    [HarmonyPostfix]
    public static void StartPostfix()
    {
        if (!AmongUsClient.Instance || !AmongUsClient.Instance.AmHost || GameOptionsManager.Instance == null) return;

        var options = GameOptionsManager.Instance.CurrentGameOptions;
        var mapId = options.MapId;
        if (mapId < AmongUsClient.Instance.ShipPrefabs.Count) return;

        Warning($"Map {mapId} does not exist in this game version: back to The Skeld.");
        options.SetByte(ByteOptionNames.MapId, 0);
        if (GameManager.Instance)
            GameManager.Instance.LogicOptions.SyncOptions();
    }
}
