using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Translation;

namespace BetterOtherRoles.Utilities;

/// <summary>
///     Texts of the role guide (Match Info) wiki pages.
/// </summary>
public static class BorWikiUtils
{
    private static readonly Regex TrailingZeroRegex = new(@"\.0+(?!\d)", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    /// <summary>
    ///     Same as MiraAPI Helpers.GetOptionsText, but for a chosen list of options (the Deputy options are in the
    ///     Sheriff group), with a full blank line between the description and the options.
    /// </summary>
    public static string GetOptionsText(IEnumerable<IModdedOption> options)
    {
        var lines = options.Where(x => x.Visible()).Select(x => $"{TranslationController.Instance.GetString(x.StringName)}: {FormatValue(x)}").ToList();
        if (lines.Count == 0) return string.Empty;

        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"\n\n<b><color=#29D7B5>{MiraLocaleManager.Get("Options")}</color></b>\n");
        foreach (var line in lines)
        {
            builder.AppendLine(line);
        }

        return builder.ToString();
    }

    public static string FormatValue(IModdedOption option)
    {
        return option switch
        {
            ModdedToggleOption toggle => TranslationController.Instance.GetString(toggle.Value ? StringNames.SettingsOn : StringNames.SettingsOff),
            ModdedEnumOption enumOption => MiraLocaleManager.Get(enumOption.Values[enumOption.Value], enumOption.Values[enumOption.Value]),
            ModdedNumberOption number => TrailingZeroRegex.Replace(number.Data.GetValueString(number.Value), string.Empty),
            _ => option.Data.GetValueString(option.GetFloatData()),
        };
    }
}
