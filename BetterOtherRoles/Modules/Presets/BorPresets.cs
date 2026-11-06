using System.IO.Compression;
using System.Text;
using AmongUs.GameOptions;
using BepInEx.Configuration;
using MiraAPI.Presets;
using MiraAPI.Roles;

namespace BetterOtherRoles.Modules.Presets;

/// <summary>
///     Presets of BetterOtherRoles, on top of the MiraAPI ones (one .cfg file per preset in mira_presets):
///     - a preset also holds the vanilla game settings (section "Vanilla", the map left out);
///     - the active preset (last one loaded or saved) is kept across restarts, and marked as changed when the settings
///       differ from its file (never for "Default": it is then no longer active);
///     - a preset can be exported as a code ("BOR-" + the preset values, compressed) and a code imported (applied at once).
/// </summary>
public static class BorPresets
{
    /// <summary>
    ///     Name of the preset rebuilt by MiraAPI at each start with the default values (never changed nor deleted).
    /// </summary>
    public const string DefaultName = "Default";

    /// <summary>
    ///     Name of an exported preset changed since loaded, or of exported settings of no preset.
    /// </summary>
    public const string ImportedName = "ImportedPreset";

    private const string VanillaSection = "Vanilla";
    private const string CodePrefix = "BOR-";

    // First line of a code, before the preset values.
    private const string NameLine = "Name=";

    private static ConfigEntry<string>? _activePreset;

    /// <summary>
    ///     Gets the name of the active preset (last one loaded or saved), or an empty string.
    /// </summary>
    public static string ActivePreset => _activePreset?.Value ?? string.Empty;

    /// <summary>
    ///     Binds the saved active preset.
    /// </summary>
    public static void Initialize(ConfigFile config)
    {
        _activePreset = config.Bind("Presets", "ActivePreset", string.Empty, "Last preset loaded or saved.");
        LowerFileNames();
    }

    /// <summary>
    ///     Gets the name a preset is saved under: lower case (Windows does not tell file names apart by case), without
    ///     the spaces around.
    /// </summary>
    public static string ToPresetName(string name) => name.Trim().ToLowerInvariant();

    // Presets saved before the names were in lower case ("Default" stays as MiraAPI writes it). The file is first
    // moved to a temporary name: Windows takes a change of case only as the same name.
    private static void LowerFileNames()
    {
        foreach (var name in GetPresetNames())
        {
            var lower = ToPresetName(name);
            if (lower == name || name.Equals(DefaultName, StringComparison.OrdinalIgnoreCase)) continue;

            try
            {
                var temporary = GetPath(name) + ".rename";
                File.Move(GetPath(name), temporary);
                File.Move(temporary, GetPath(lower));
                if (ActivePreset == name)
                    SetActive(lower);
            }
            catch (IOException e)
            {
                Warning($"Preset {name} not renamed to {lower}: {e.Message}");
            }
        }
    }

    /// <summary>
    ///     Records the active preset.
    /// </summary>
    public static void SetActive(string name)
    {
        if (_activePreset != null && _activePreset.Value != name)
            _activePreset.Value = name;
    }

    /// <summary>
    ///     Gets the BetterOtherRoles plugin info of MiraAPI.
    /// </summary>
    public static MiraPluginInfo? Plugin => MiraPluginManager.GetPluginByGuid(BetterOtherRolesPlugin.Id);

    /// <summary>
    ///     Gets the folder of the presets.
    /// </summary>
    public static string Folder => Path.Combine(PresetManager.PresetDirectory, BetterOtherRolesPlugin.Id);

    /// <summary>
    ///     Gets the path of a preset file.
    /// </summary>
    public static string GetPath(string name) => Path.Combine(Folder, name + ".cfg");

    /// <summary>
    ///     Writes the current settings to a preset file (created or replaced), as MiraAPI does, with the vanilla settings.
    /// </summary>
    public static void Save(string name)
    {
        Directory.CreateDirectory(Folder);
        var path = GetPath(name);
        if (File.Exists(path))
            File.Delete(path);

        var config = new ConfigFile(path, false) { SaveOnConfigSet = false };
        WriteCurrent(config);
        config.Save();
        SetActive(name);
    }

    /// <summary>
    ///     Deletes a preset file (never "Default").
    /// </summary>
    public static void Delete(string name)
    {
        if (name == DefaultName) return;

        var path = GetPath(name);
        if (File.Exists(path))
            File.Delete(path);

        if (ActivePreset == name)
            SetActive(string.Empty);
    }

    /// <summary>
    ///     Renames a preset file (never "Default"), the active preset included.
    /// </summary>
    public static void Rename(string name, string newName)
    {
        if (name == DefaultName || !IsFreeName(newName) || !File.Exists(GetPath(name))) return;

        File.Move(GetPath(name), GetPath(newName));
        if (ActivePreset == name)
            SetActive(newName);
    }

    /// <summary>
    ///     Gets whether a name can be given to a new preset: not empty, valid as a file name, and not used yet.
    /// </summary>
    public static bool IsFreeName(string name)
    {
        return IsValidName(name) && FindPreset(name) == null;
    }

    /// <summary>
    ///     Tells whether a name can be the one of a saved preset (existing or not): not empty, a valid file name, and
    ///     not "Default" (written again at each start).
    /// </summary>
    public static bool IsValidName(string name)
    {
        // Windows drops a final "." from file names.
        return name.Length > 0 && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !name.EndsWith('.') &&
               !string.Equals(name, DefaultName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Gets the existing preset with this name, whatever the case (file names are not case sensitive on Windows).
    /// </summary>
    public static string? FindPreset(string name)
    {
        return GetPresetNames().FirstOrDefault(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Gets the name to show for the active preset: "BOR", "BOR*" when the settings changed since, or an empty string
    ///     when no preset is active ("Default" changed is no longer active).
    /// </summary>
    public static bool TryGetActive(out string name, out bool changed)
    {
        name = ActivePreset;
        changed = false;
        var presets = GetPresetNames();

        // Nothing recorded (first start, preset deleted): a preset with the same values.
        if (!presets.Contains(name))
        {
            var current = GetCurrentValues();
            name = presets.FirstOrDefault(x => Differs(current, x) == false) ?? string.Empty;
            if (name.Length == 0) return false;

            SetActive(name);
            return true;
        }

        changed = Differs(GetCurrentValues(), name) != false;
        return name != DefaultName || !changed;
    }

    /// <summary>
    ///     Writes the vanilla settings of a preset to the game, then sends them to the players.
    /// </summary>
    public static void ApplyVanilla(ConfigFile preset)
    {
        var options = GameOptionsManager.Instance?.CurrentGameOptions;
        if (options == null) return;

        var values = ReadValues(preset.ConfigFilePath);
        var any = false;
        foreach (var (key, setting) in GetVanillaSettings())
        {
            if (values.TryGetValue($"{VanillaSection}.{key}", out var value))
                any |= SetVanilla(options, setting, value);
        }

        if (any && GameManager.Instance && GameManager.Instance.LogicOptions != null)
        {
            GameManager.Instance.LogicOptions.SyncOptions();
            GameOptionsManager.Instance!.SaveNormalHostOptions();
        }
    }

    /// <summary>
    ///     Gets the code of the current settings: "BOR-" then the preset name and values, compressed (the name is not
    ///     readable in the code).
    /// </summary>
    /// <param name="name">Name shown when the code is imported.</param>
    public static string Export(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), "BorPresetExport.cfg");
        File.Delete(path);
        var config = new ConfigFile(path, false) { SaveOnConfigSet = false };
        WriteCurrent(config);
        config.Save();

        // Only the values: the comments of the file are not needed.
        var lines = File.ReadAllLines(path).Where(x => x.Length > 0 && !x.StartsWith('#'));
        File.Delete(path);
        var text = $"{NameLine}{name.ReplaceLineEndings(" ")}\n" + string.Join('\n', lines);
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal))
        {
            deflate.Write(Encoding.UTF8.GetBytes(text));
        }

        return CodePrefix + Convert.ToBase64String(output.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>
    ///     Reads a code made by <see cref="Export"/>.
    /// </summary>
    /// <param name="code">The code (spaces around it are ignored).</param>
    /// <param name="name">Name of the exported preset.</param>
    /// <param name="values">Preset values, as the text of a preset file.</param>
    /// <returns>False if the text is not a BetterOtherRoles code.</returns>
    public static bool TryReadCode(string? code, out string name, out string values)
    {
        name = string.Empty;
        values = string.Empty;
        code = code?.Trim() ?? string.Empty;
        if (!code.StartsWith(CodePrefix, StringComparison.Ordinal)) return false;

        string text;
        try
        {
            var base64 = code[CodePrefix.Length..].Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
            using var input = new MemoryStream(Convert.FromBase64String(base64));
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(deflate, Encoding.UTF8);
            text = reader.ReadToEnd();
        }
        catch (Exception e) when (e is FormatException or InvalidDataException)
        {
            return false;
        }

        if (!text.StartsWith(NameLine, StringComparison.Ordinal) || !text.Contains('[')) return false;

        var end = text.IndexOf('\n');
        name = text[NameLine.Length..end].Trim();
        values = text[(end + 1)..];
        return true;
    }

    /// <summary>
    ///     Applies the values of a code as a preset (unknown values are left out, missing values get their default value),
    ///     then saves them as a preset, which becomes the active one.
    /// </summary>
    /// <param name="values">Values read by <see cref="TryReadCode"/>.</param>
    /// <param name="name">Name of the new preset.</param>
    public static void Import(string values, string name)
    {
        var plugin = Plugin;
        if (plugin == null) return;

        // Loaded as a preset file, so MiraAPI applies and sends it.
        var path = Path.Combine(Path.GetTempPath(), "BorPresetImport.cfg");
        File.WriteAllText(path, values);
        var config = new ConfigFile(path, false) { SaveOnConfigSet = false };
        BindAll(plugin, config);
        new OptionPreset(string.Empty, plugin, config).LoadPreset();

        var imported = ReadValues(path);
        var current = GetCurrentValues();
        var different = imported.Where(x => current.TryGetValue(x.Key, out var value) && value != x.Value).Select(x => $"{x.Key}: {current[x.Key]} instead of {x.Value}").ToList();
        Info($"Code imported: {imported.Count} values, {different.Count} not applied{(different.Count > 0 ? ": " + string.Join(", ", different) : string.Empty)}");

        File.Delete(path);
        Save(name);
    }

    /// <summary>
    ///     Binds the options and roles of the mod to a preset file, as MiraAPI does when it reads the presets (the values
    ///     missing in the file get their default value), without writing it.
    /// </summary>
    public static void BindAll(MiraPluginInfo plugin, ConfigFile config)
    {
        foreach (var option in plugin.Options.Where(x => x.IncludeInPreset))
        {
            option.Bind(config);
        }

        foreach (var role in plugin.Roles.Values.OfType<ICustomRole>().Where(x => !x.Configuration.HideSettings))
        {
            role.BindConfig(config);
        }
    }

    /// <summary>
    ///     Gets the names of the preset files.
    /// </summary>
    public static List<string> GetPresetNames()
    {
        return Directory.Exists(Folder)
            ? Directory.GetFiles(Folder, "*.cfg").Select(Path.GetFileNameWithoutExtension).OfType<string>().ToList()
            : [];
    }

    // Options and roles (as MiraAPI saves them), then the vanilla settings.
    private static void WriteCurrent(ConfigFile config)
    {
        var plugin = Plugin;
        if (plugin != null)
        {
            foreach (var option in plugin.Options.Where(x => x.IncludeInPreset))
            {
                option.SaveToPreset(config);
            }

            foreach (var role in plugin.Roles.Values.OfType<ICustomRole>().Where(x => !x.Configuration.HideSettings))
            {
                role.SaveToPreset(config);
            }
        }

        var options = GameOptionsManager.Instance?.CurrentGameOptions;
        if (options == null) return;

        foreach (var (key, setting) in GetVanillaSettings())
        {
            var value = GetVanilla(options, setting);
            if (value != null)
                config.Bind(VanillaSection, key, value).Value = value;
        }
    }

    // True when the current values differ from the preset file (the values missing in the file are left out), null when
    // the file cannot be read.
    private static bool? Differs(Dictionary<string, string> current, string preset)
    {
        var path = GetPath(preset);
        if (!File.Exists(path)) return null;

        var saved = ReadValues(path);
        return saved.Any(x => current.TryGetValue(x.Key, out var value) && value != x.Value);
    }

    private static Dictionary<string, string> GetCurrentValues()
    {
        var path = Path.Combine(Path.GetTempPath(), "BorPresetCurrent.cfg");
        File.Delete(path);
        var config = new ConfigFile(path, false) { SaveOnConfigSet = false };
        WriteCurrent(config);
        config.Save();
        var values = ReadValues(path);
        File.Delete(path);
        return values;
    }

    // "Section.Key" -> value, read from the text of a .cfg file.
    private static Dictionary<string, string> ReadValues(string path)
    {
        var values = new Dictionary<string, string>();
        if (!File.Exists(path)) return values;

        var section = string.Empty;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1];
                continue;
            }

            var equal = line.IndexOf('=');
            if (equal > 0)
                values[$"{section}.{line[..equal].Trim()}"] = line[(equal + 1)..].Trim();
        }

        return values;
    }

    // Every vanilla game setting of the normal mode, the map left out: "Float.KillCooldown" -> setting.
    private static IEnumerable<(string Key, BaseGameSetting Setting)> GetVanillaSettings()
    {
        var categories = GameManager.Instance ? GameManager.Instance.GameSettingsList?.AllCategories : null;
        if (categories == null) yield break;

        foreach (var category in categories)
        {
            foreach (var setting in category.AllGameSettings)
            {
                var key = GetKey(setting);
                if (key != null && !key.Contains("Map", StringComparison.OrdinalIgnoreCase))
                    yield return (key, setting);
            }
        }
    }

    private static string? GetKey(BaseGameSetting setting)
    {
        if (setting.TryCast<FloatGameSetting>() is { } floatSetting) return $"Float.{floatSetting.OptionName}";

        if (setting.TryCast<IntGameSetting>() is { } intSetting) return $"Int.{intSetting.OptionName}";

        if (setting.TryCast<CheckboxGameSetting>() is { } boolSetting) return $"Bool.{boolSetting.OptionName}";

        return setting.TryCast<StringGameSetting>() is { } stringSetting ? $"Int.{stringSetting.OptionName}" : null;
    }

    private static string? GetVanilla(IGameOptions options, BaseGameSetting setting)
    {
        try
        {
            if (setting.TryCast<FloatGameSetting>() is { } floatSetting)
                return options.GetFloat(floatSetting.OptionName).ToString(BetterOtherRolesPlugin.Culture);

            if (setting.TryCast<IntGameSetting>() is { } intSetting)
                return options.GetInt(intSetting.OptionName).ToString(BetterOtherRolesPlugin.Culture);

            if (setting.TryCast<CheckboxGameSetting>() is { } boolSetting)
                return options.GetBool(boolSetting.OptionName) ? "true" : "false";

            if (setting.TryCast<StringGameSetting>() is { } stringSetting)
                return options.GetInt(stringSetting.OptionName).ToString(BetterOtherRolesPlugin.Culture);
        }
        catch (Exception)
        {
            // Option not used by the current mode.
        }

        return null;
    }

    private static bool SetVanilla(IGameOptions options, BaseGameSetting setting, string value)
    {
        try
        {
            if (setting.TryCast<FloatGameSetting>() is { } floatSetting &&
                float.TryParse(value, System.Globalization.NumberStyles.Float, BetterOtherRolesPlugin.Culture, out var number))
            {
                options.SetFloat(floatSetting.OptionName, number);
                return true;
            }

            if (setting.TryCast<IntGameSetting>() is { } intSetting && int.TryParse(value, out var count))
            {
                options.SetInt(intSetting.OptionName, count);
                return true;
            }

            if (setting.TryCast<CheckboxGameSetting>() is { } boolSetting && bool.TryParse(value, out var flag))
            {
                options.SetBool(boolSetting.OptionName, flag);
                return true;
            }

            if (setting.TryCast<StringGameSetting>() is { } stringSetting && int.TryParse(value, out var index))
            {
                options.SetInt(stringSetting.OptionName, index);
                return true;
            }
        }
        catch (Exception)
        {
            // Option not used by the current mode.
        }

        return false;
    }
}
