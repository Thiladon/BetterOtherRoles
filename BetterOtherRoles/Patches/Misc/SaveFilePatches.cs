using System.Text.Json;
using AmongUs.Data;
using AmongUs.Data.Player;
using AmongUs.Data.Settings;
using HarmonyLib;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Our own copies of the game settings (settings.bor.amogus), of the profile (player.bor.amogus) and of the server list
///     (regionInfo.bor.json), next to the vanilla files: another Among Us (vanilla or another mod, newer or older) can no
///     longer give us a file it wrote, the cause of the black screen at startup (settings) and of the terms of use asked again
///     (profile).
///     On the first launch, the vanilla files are copied once, when they can be read. The game reads its settings and profile
///     before the mods are loaded: they are read again from our files once the patches are applied (<see cref="ReloadData"/>).
/// </summary>
[HarmonyPatch]
public static class SaveFilePatches
{
    private const string VanillaSettings = "settings.amogus";
    private const string BorSettings = "settings.bor.amogus";
    private const string VanillaPlayer = "player.amogus";
    private const string BorPlayer = "player.bor.amogus";
    private const string VanillaRegions = "regionInfo.json";
    private const string BorRegions = "regionInfo.bor.json";

    /// <summary>
    ///     Copies the vanilla files to ours when ours do not exist yet, before the game reads them.
    /// </summary>
    public static void Initialize()
    {
        var folder = Application.persistentDataPath;
        CopyOnce(Path.Combine(folder, VanillaSettings), Path.Combine(folder, BorSettings));
        CopyOnce(Path.Combine(folder, VanillaPlayer), Path.Combine(folder, BorPlayer));
        CopyOnce(Path.Combine(folder, VanillaRegions), Path.Combine(folder, BorRegions));
    }

    /// <summary>
    ///     Reads the settings and the profile again, from our files: the game loads them before the mods (from the vanilla
    ///     files). Called once the patches are applied.
    /// </summary>
    public static void ReloadData()
    {
        // The game reads the file into the data already loaded: the lists are added to, not replaced. Without clearing them,
        // they double on every launch (announcements read: 2^26 entries, an 805 MB profile, the game frozen at startup).
        var settings = DataManager.Settings;
        if (settings != null)
        {
            settings.Multiplayer?.quickChatFavorites?.Clear();
            settings.ForceLoad();
        }

        var player = DataManager.Player;
        if (player != null)
        {
            player.Announcements?.ids?.Clear();
            player.Friends?.cachedNames?.Clear();
            player.Store?.legacyPurchases?.Clear();
            player.ForceLoad();
        }
    }

    [HarmonyPatch(typeof(SettingsData), nameof(SettingsData.FileName), MethodType.Getter)]
    [HarmonyPostfix]
    public static void SettingsFileNamePostfix(ref string __result)
    {
        if (__result == VanillaSettings)
        {
            __result = BorSettings;
        }
    }

    [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.FileName), MethodType.Getter)]
    [HarmonyPostfix]
    public static void PlayerFileNamePostfix(ref string __result)
    {
        if (__result == VanillaPlayer)
        {
            __result = BorPlayer;
        }
    }

    [HarmonyPatch(typeof(ServerManager), nameof(ServerManager.LoadServers))]
    [HarmonyPatch(typeof(ServerManager), nameof(ServerManager.SaveServers))]
    [HarmonyPrefix]
    public static void ServersPrefix(ServerManager __instance)
    {
        var path = __instance.serverInfoFileJson;
        if (!string.IsNullOrEmpty(path) && Path.GetFileName(path) == VanillaRegions)
        {
            __instance.serverInfoFileJson = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, BorRegions);
        }
    }

    // Only a file the game wrote as valid JSON is copied: otherwise the game starts from its default values.
    private static void CopyOnce(string source, string destination)
    {
        if (File.Exists(destination) || !File.Exists(source))
        {
            return;
        }

        try
        {
            JsonDocument.Parse(File.ReadAllText(source)).Dispose();
            File.Copy(source, destination);
            Info($"Save files: {Path.GetFileName(source)} copied to {Path.GetFileName(destination)}.");
        }
        catch (Exception e)
        {
            Warning($"Save files: {Path.GetFileName(source)} not copied, default values used instead ({e.Message}).");
        }
    }
}
