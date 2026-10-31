using System.Text;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Text matching of the role guide search, in the game language: without case nor accents ("maj" finds "Màj",
///     "role" finds "Rôle"), and every vanilla setting.
///     Accents are removed with a table: the .NET normalization may be missing in the game (invariant globalization).
/// </summary>
public static class GuideSearchText
{
    private static readonly Dictionary<char, string> Folded = BuildFolded();

    /// <summary>
    ///     Checks whether a text contains the query, without case nor accents.
    /// </summary>
    public static bool Matches(string? text, string query)
    {
        return !string.IsNullOrEmpty(text) && Fold(text).Contains(Fold(query), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Checks whether a text is the query, without case nor accents.
    /// </summary>
    public static bool Equal(string? text, string query)
    {
        return !string.IsNullOrEmpty(text) && Fold(text) == Fold(query);
    }

    /// <summary>
    ///     Gets every vanilla game setting of the current mode (MiraAPI only knows the 7 of the lobby summary):
    ///     its title in the game language and its current value.
    /// </summary>
    public static List<(string Title, string Value)> GetVanillaSettings()
    {
        var settings = new List<(string Title, string Value)>();
        var categories = GameManager.Instance ? GameManager.Instance.GameSettingsList?.AllCategories : null;
        var options = GameOptionsManager.Instance?.CurrentGameOptions;
        var translation = TranslationController.Instance;
        if (categories == null || options == null || !translation)
        {
            return settings;
        }

        foreach (var category in categories)
        {
            foreach (var setting in category.AllGameSettings)
            {
                settings.Add((translation.GetString(setting.Title), GetValue(setting, options)));
            }
        }

        return settings;
    }

    private static string GetValue(BaseGameSetting setting, AmongUs.GameOptions.IGameOptions options)
    {
        try
        {
            if (setting.TryCast<FloatGameSetting>() is { } floatSetting)
            {
                return floatSetting.GetValueString(options.GetFloat(floatSetting.OptionName));
            }

            if (setting.TryCast<IntGameSetting>() is { } intSetting)
            {
                return intSetting.GetValueString(options.GetInt(intSetting.OptionName));
            }

            if (setting.TryCast<CheckboxGameSetting>() is { } boolSetting)
            {
                return boolSetting.GetValueString(options.GetBool(boolSetting.OptionName) ? 1f : 0f);
            }

            if (setting.TryCast<StringGameSetting>() is { } stringSetting)
            {
                return stringSetting.GetValueString(options.GetInt(stringSetting.OptionName));
            }
        }
        catch (Exception)
        {
            // Option not used by the current mode.
        }

        return string.Empty;
    }

    private static string Fold(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.ToLowerInvariant())
        {
            if (Folded.TryGetValue(c, out var folded))
            {
                builder.Append(folded);
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private static Dictionary<char, string> BuildFolded()
    {
        var table = new Dictionary<char, string>();
        Add("àâäáãåā", "a");
        Add("çćč", "c");
        Add("éèêëēė", "e");
        Add("îïíìī", "i");
        Add("ñń", "n");
        Add("ôöóòõøō", "o");
        Add("ùûüúū", "u");
        Add("ÿý", "y");
        Add("ß", "ss");
        Add("æ", "ae");
        Add("œ", "oe");
        return table;

        void Add(string chars, string folded)
        {
            foreach (var c in chars)
            {
                table[c] = folded;
            }
        }
    }
}
