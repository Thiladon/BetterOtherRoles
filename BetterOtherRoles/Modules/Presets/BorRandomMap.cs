using System.Collections;
using AmongUs.GameOptions;
using BepInEx;
using BepInEx.Configuration;
using BetterOtherRoles.Options.Maps;
using BetterOtherRoles.Utilities;
using HarmonyLib;
using MiraAPI.GameOptions;
using MiraAPI.Translation;
using Reactor.Utilities;

namespace BetterOtherRoles.Modules.Presets;

/// <summary>
///     Random map (<see cref="RandomMapOptions"/>), drawn by the host when the start countdown ends (a countdown stopped
///     draws nothing), before the game starts and the roles are given:
///     - a map at 100 % is forced, with the other ones at 100 % (BetterOtherRoles 1.7.1), otherwise each map is drawn by
///       its weight (TOU-Mira 1.7.9); no weight: the map of the lobby;
///     - with presets per map, the preset of the drawn map is loaded for this game only, without a name (the active
///       preset of the lobby stays): the settings of the lobby are saved first in a backup file;
///     - back in the lobby, the host gets its settings and its map back (also after a crash: the file stays until then).
/// </summary>
[HarmonyPatch]
public static class BorRandomMap
{
    private const string BackupSection = "RandomMap";

    // Outside the presets folder: not a preset.
    private static string BackupPath => Path.Combine(Paths.ConfigPath, $"{BetterOtherRolesPlugin.Id}.randommap.cfg");

    /// <summary>
    ///     Gives the presets of the host as choices for each map. A renamed preset stays chosen under its new name, a
    ///     deleted one goes back to "None".
    /// </summary>
    /// <param name="oldName">Name of a renamed preset.</param>
    /// <param name="newName">New name of that preset.</param>
    public static void RefreshPresets(string? oldName = null, string? newName = null)
    {
        if (!AmongUsClient.Instance || !AmongUsClient.Instance.AmHost) return;

        var presets = BorPresets.GetPresetNames()
            .OrderBy(x => x != BorPresets.DefaultName)
            .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var (_, _, option) in OptionGroupSingleton<RandomMapOptions>.Instance.Maps)
        {
            option.SetChoices(presets, false);
            if (oldName != null && newName != null && option.Value == oldName)
                option.SetValue(newName);
            else if (option.Preset != null && !presets.Contains(option.Value))
                option.SetValue(BorPresetOption.None);
            else
                continue;

            // The line was set on the old value (gone from the choices: "None" shown): set again on the new one.
            option.SetChoices(presets, false);
        }
    }

    // FinallyBegin is inlined in GameStartManager.Update by IL2CPP (a patch of it is never called): the end of the
    // countdown is computed as Update does, just before it (same Time.deltaTime in the frame).
    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Update))]
    [HarmonyPrefix]
    public static void UpdatePrefix(GameStartManager __instance)
    {
        if (!AmongUsClient.Instance || !AmongUsClient.Instance.AmHost ||
            __instance.startState != GameStartManager.StartingStates.Countdown ||
            UnityEngine.Mathf.CeilToInt(__instance.countDownTimer - UnityEngine.Time.deltaTime) > 0) return;

        try
        {
            Draw();
        }
        catch (Exception e)
        {
            // The game starts on the map of the lobby.
            Error($"Random map: {e}");
        }
    }

    // Back in the lobby: the host waits for its player (the settings are sent by it).
    [HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
    [HarmonyPostfix]
    public static void LobbyStartPostfix() => Coroutines.Start(CoRestore());

    // Choices up to date each time the settings are opened.
    [HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.Start))]
    [HarmonyPrefix]
    public static void SettingsStartPrefix() => RefreshPresets();

    private static void Draw()
    {
        var options = OptionGroupSingleton<RandomMapOptions>.Instance;
        if (!options.Enabled.Value) return;

        var maps = options.Maps.Where(x => x.Chance.Value > 0).ToList();
        if (maps.Count == 0) return;

        if (maps.Exists(x => x.Chance.Value >= 100))
            maps = maps.Where(x => x.Chance.Value >= 100).ToList();

        var roll = UnityEngine.Random.Range(0f, maps.Sum(x => x.Chance.Value));
        var drawn = maps[^1];
        foreach (var map in maps)
        {
            if (roll < map.Chance.Value)
            {
                drawn = map;
                break;
            }

            roll -= map.Chance.Value;
        }

        var gameOptions = GameOptionsManager.Instance.CurrentGameOptions;
        var preset = options.PresetPerMap.Value ? drawn.Preset.Preset : null;
        if (preset != null && BorPresets.FindPreset(preset) == null)
        {
            Warning($"Random map: preset {preset} of {drawn.Map} not found, settings of the lobby kept.");
            BorChat.AddSystemMessage(string.Format(BetterOtherRolesPlugin.Culture,
                MiraLocaleManager.Get("BetterOtherRoles.RandomMap.Chat.PresetMissing",
                    "Preset \"{0}\" not found: the settings of the lobby are kept."), preset));
            preset = null;
        }

        SaveBackup(gameOptions.MapId, preset != null);
        Info($"Random map: {drawn.Map} drawn{(preset != null ? $", preset {preset}" : string.Empty)}.");

        gameOptions.SetByte(ByteOptionNames.MapId, (byte)drawn.Map);
        if (preset != null)
            BorPresets.LoadWithoutName(BorPresets.GetPath(preset));

        GameManager.Instance.LogicOptions.SyncOptions();
    }

    // Map of the lobby, and its settings when a preset is loaded (left out otherwise: nothing else changes).
    private static void SaveBackup(byte mapId, bool withSettings)
    {
        if (File.Exists(BackupPath))
            File.Delete(BackupPath);

        var config = new ConfigFile(BackupPath, false) { SaveOnConfigSet = false };
        if (withSettings)
            BorPresets.WriteTo(config);

        config.Bind(BackupSection, "MapId", (int)mapId).Value = mapId;
        config.Bind(BackupSection, "Settings", withSettings).Value = withSettings;
        config.Save();
    }

    private static IEnumerator CoRestore()
    {
        while (LobbyBehaviour.Instance && (!PlayerControl.LocalPlayer || !GameManager.Instance || GameManager.Instance.LogicOptions == null))
        {
            yield return null;
        }

        if (!LobbyBehaviour.Instance || !AmongUsClient.Instance.AmHost || !File.Exists(BackupPath)) yield break;

        try
        {
            var config = new ConfigFile(BackupPath, false) { SaveOnConfigSet = false };
            var mapId = config.Bind(BackupSection, "MapId", -1).Value;
            if (config.Bind(BackupSection, "Settings", false).Value)
                BorPresets.LoadWithoutName(BackupPath);

            var gameOptions = GameOptionsManager.Instance!.CurrentGameOptions;
            if (mapId >= 0 && mapId < AmongUsClient.Instance.ShipPrefabs.Count)
                gameOptions.SetByte(ByteOptionNames.MapId, (byte)mapId);

            GameManager.Instance.LogicOptions?.SyncOptions();
            GameOptionsManager.Instance.SaveNormalHostOptions();
            Info($"Random map: settings of the lobby back (map {mapId}).");
        }
        catch (Exception e)
        {
            Error($"Random map: settings of the lobby not restored: {e}");
        }

        File.Delete(BackupPath);
    }
}
