using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using BetterOtherRoles.Patches.Misc;
using MiraAPI.Modifiers;
using MiraAPI.Modifiers.Types;
using MiraAPI.Roles;
using MiraAPI.Translation;
using Reactor.Utilities;
using UnityEngine;

namespace BetterOtherRoles.Modules.Chat;

/// <summary>
///     Clickable terms in the chat (TOU-Mira 1.7.9 WikiHyperLinkPatches): "#Sheriff" for a role, "&amp;Lovers" for a
///     modifier, spaces written as "-" ("#Bounty-Hunter"). The name in any language, whatever the language of the game,
///     without case nor accents ("#sherif" finds "Shérif"). The term is shown spelled right in the language it was
///     written in ("#sheriff" shows "Sheriff"), bold, in its color; italic on hover (<see cref="BorChatLinkBehaviour"/>);
///     a click closes the chat and opens the role guide on its page. Crewmate and Impostor are only colored: the guide
///     has no page for them. The game removes the tags of a message when it is sent (PlayerControl.RpcSendChat); a
///     text still in &lt;noparse&gt; (<see cref="ChatInputPatches"/>) has its terms taken out, the rest stays as typed.
/// </summary>
public static class BorChatLinks
{
    private const string NoParseOpen = "<noparse>";
    private const string NoParseClose = "</noparse>";
    private const string LinkPrefix = "borlink";

    // Not after a letter, a digit, "=" (a color in a tag, "<color=#FF0000>"), "#" or "&".
    private static readonly Regex TermRegex = new(
        @"(?<![\p{L}\p{N}_=#&])([#&])([\p{L}\p{M}\p{N}_'-]+)",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    // Link id -> its text, normal and hovered, and what a click opens. Emptied in each new lobby.
    private static readonly Dictionary<string, Link> Links = [];
    private static int _nextId;

    // Names of Crewmate and Impostor in every language of the game.
    private static readonly Dictionary<StringNames, List<string>> VanillaNames = [];

    /// <summary>
    ///     Gets the text with its terms made into links, or the same text when it has none.
    /// </summary>
    public static string Apply(string text)
    {
        if (string.IsNullOrEmpty(text) || (text.IndexOf('#') < 0 && text.IndexOf('&') < 0)) return text;

        var noParse = text.StartsWith(NoParseOpen, StringComparison.Ordinal) && text.EndsWith(NoParseClose, StringComparison.Ordinal);
        var inner = noParse ? text[NoParseOpen.Length..^NoParseClose.Length] : text;

        var builder = new StringBuilder();
        var last = 0;
        foreach (Match match in TermRegex.Matches(inner))
        {
            var term = Resolve(match.Groups[1].Value[0], match.Groups[2].Value);
            if (term == null) continue;

            AppendPlain(builder, inner[last..match.Index], noParse);
            builder.Append(term);
            last = match.Index + match.Length;
        }

        if (last == 0) return text;

        AppendPlain(builder, inner[last..], noParse);
        return builder.ToString();
    }

    /// <summary>
    ///     Gets whether the id is one of a link made here.
    /// </summary>
    public static bool IsLink(string id) => Links.ContainsKey(id);

    /// <summary>
    ///     Shows a link normal or hovered (italic) in a text.
    /// </summary>
    public static string SetHovered(string text, string id, bool hovered)
    {
        if (!Links.TryGetValue(id, out var link)) return text;

        return hovered ? text.Replace(link.Normal, link.Hovered, StringComparison.Ordinal) : text.Replace(link.Hovered, link.Normal, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Opens the page of a link: the chat is closed first, the guide does not open over it.
    /// </summary>
    public static void Open(string id)
    {
        if (Links.TryGetValue(id, out var link))
            Coroutines.Start(CoOpen(link));
    }

    /// <summary>
    ///     Forgets the links of the previous game.
    /// </summary>
    public static void Clear() => Links.Clear();

    private static IEnumerator CoOpen(Link link)
    {
        var hud = HudManager.InstanceExists ? HudManager.Instance : null;
        if (hud && hud!.Chat && hud.Chat.IsOpenOrOpening)
        {
            hud.Chat.Close();
            while (hud && hud.Chat && hud.Chat.IsOpenOrOpening)
                yield return null;
        }

        if (link.Role)
            RoleGuidePatches.LastPage.Show(link.Role!);
        else if (link.Modifier != null)
            RoleGuidePatches.LastPage.Show(link.Modifier);
    }

    // Outside a tag, the text of a player stays as typed.
    private static void AppendPlain(StringBuilder builder, string text, bool noParse)
    {
        if (text.Length == 0) return;

        if (noParse && text.Contains('<'))
            builder.Append(NoParseOpen).Append(text).Append(NoParseClose);
        else
            builder.Append(text);
    }

    // The term in its color, a link when the guide has a page for it; null when nothing has this name. The name is
    // shown as spelled in the language it was written in ("#sheriff" shows "Sheriff", "#sherif" shows "Shérif").
    private static string? Resolve(char kind, string key)
    {
        var name = key.Replace('-', ' ');
        if (kind == '&')
        {
            foreach (var modifier in ModifierManager.Modifiers.OfType<GameModifier>())
            {
                if (FindName(name, modifier.ModifierName, modifier.ModifierNameLocale) is { } found)
                    return MakeLink(found, modifier.FreeplayFileColor, null, modifier);
            }

            return null;
        }

        foreach (var role in CustomRoleManager.CustomRoleBehaviours)
        {
            if (role is ICustomRole custom && FindName(name, custom.RoleName, custom.RoleNameLocale) is { } found)
                return MakeLink(found, custom.RoleColor, role, null);
        }

        if (FindVanillaName(name, StringNames.Crewmate, "Crewmate") is { } crewmate) return Colored(crewmate, BorColors.Crewmate);
        if (FindVanillaName(name, StringNames.Impostor, "Impostor") is { } impostor) return Colored(impostor, BorColors.Impostor);

        return null;
    }

    // The name in any language, whatever the language of the game: a French player in an English game writes in French.
    private static string? FindName(string name, string localName, string localeKey)
    {
        if (RoleGuidePatches.SearchText.Equal(localName, name)) return localName;

        return Enum.GetValues<MiraLanguage>()
            .Select(language => MiraLocaleManager.Get(language, localeKey))
            .FirstOrDefault(x => RoleGuidePatches.SearchText.Equal(x, name));
    }

    private static string? FindVanillaName(string name, StringNames stringName, string english)
    {
        var localName = TranslationController.Instance.GetString(stringName);
        if (RoleGuidePatches.SearchText.Equal(localName, name)) return localName;
        if (RoleGuidePatches.SearchText.Equal(english, name)) return english;

        return GetVanillaNames(stringName).FirstOrDefault(x => RoleGuidePatches.SearchText.Equal(x, name));
    }

    // The game keeps its texts in the chosen language only: the names are read once in the file of each language.
    private static List<string> GetVanillaNames(StringNames stringName)
    {
        if (VanillaNames.TryGetValue(stringName, out var names)) return names;

        names = [];
        var key = stringName.ToString();
        foreach (var language in TranslationController.Instance.Languages.Values)
        {
            if (!language || !language.Data) continue;

            foreach (var line in language.Data.text.Split('\n'))
            {
                if (!line.StartsWith(key, StringComparison.Ordinal) || !LanguageUnit.TryParseTsvLine(line.TrimEnd('\r'), out var lineKey, out var text) || lineKey != key) continue;

                names.Add(text);
                break;
            }
        }

        VanillaNames[stringName] = names;
        return names;
    }

    private static string Colored(string name, Color color) => $"<b><color=#{ColorUtility.ToHtmlStringRGB(color)}>{name}</color></b>";

    private static string MakeLink(string name, Color color, RoleBehaviour? role, GameModifier? modifier)
    {
        var id = $"{LinkPrefix}{_nextId++}";
        var color32 = ColorUtility.ToHtmlStringRGB(color);
        var normal = $"<link=\"{id}\">{name}</link>";
        var hovered = $"<link=\"{id}\"><i>{name}</i></link>";
        Links[id] = new Link(normal, hovered, role, modifier);
        return $"<b><color=#{color32}>{normal}</color></b>";
    }

    private sealed record Link(string Normal, string Hovered, RoleBehaviour? Role, GameModifier? Modifier);
}
