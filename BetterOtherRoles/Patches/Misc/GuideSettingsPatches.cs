using System.Collections;
using System.Reflection;
using HarmonyLib;
using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
using MiraAPI.Modifiers.Types;
using MiraAPI.Roles;
using MiraAPI.Translation;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Settings tab of the MiraAPI 0.6.0 role guide (Match Info). MiraAPI only lists the vanilla settings and the
///     option groups not linked to a role, and only searches in these groups. Added here:
///     - a "Roles" page (count and chance of each role, then its options) and a "Modifiers" page (when there are any);
///     - the vanilla settings in the search results.
///     The page and entry types are internal to MiraAPI: they are created by reflection.
/// </summary>
[HarmonyPatch]
public static class GuideSettingsPatches
{
    private static readonly Assembly MiraAssembly = typeof(MiraPluginManager).Assembly;
    private static readonly Type PagesType = MiraAssembly.GetType("MiraAPI.Utilities.GuideOptionPages", true)!;
    private static readonly Type PageType = MiraAssembly.GetType("MiraAPI.Utilities.GuideOptionPage", true)!;
    private static readonly Type EntryType = MiraAssembly.GetType("MiraAPI.Utilities.GuideOptionEntry", true)!;
    private static readonly MethodInfo TryCreateEntryMethod = AccessTools.Method(PagesType, "TryCreateEntry");
    private static readonly MethodInfo GetModSearchTermsMethod = AccessTools.Method(PagesType, "GetModSearchTerms");
    private static readonly PropertyInfo PluginManagerInstanceProperty = AccessTools.Property(typeof(MiraPluginManager), "Instance");
    private static readonly PropertyInfo RegisteredPluginsProperty = AccessTools.Property(typeof(MiraPluginManager), "RegisteredPlugins");

    private static readonly PropertyInfo PageEntriesProperty = AccessTools.Property(PageType, "Entries");
    private static readonly PropertyInfo EntryGroupProperty = AccessTools.Property(EntryType, "Group");
    private static readonly PropertyInfo EntryTitleProperty = AccessTools.Property(EntryType, "Title");
    private static readonly PropertyInfo EntryGroupNameProperty = AccessTools.Property(EntryType, "GroupName");
    private static readonly PropertyInfo EntryValueProperty = AccessTools.Property(EntryType, "Value");

    private static readonly VanillaGuideGroup VanillaGroup = new();


    private static object CreateEntry(string title, string value, AbstractOptionGroup group, string groupName, string abbreviation, IReadOnlyList<string> terms)
    {
        return Activator.CreateInstance(EntryType, title, value, group, groupName, abbreviation, terms)!;
    }

    [HarmonyPatch]
    public static class BuildPagesPatch
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(PagesType, "BuildPages");
        }

        public static void Postfix(IList __result, Func<bool, string> formatBool)
        {
            var plugins = (MiraPluginInfo[])RegisteredPluginsProperty.GetValue(PluginManagerInstanceProperty.GetValue(null))!;

            var rolesPage = BuildPage(
                plugins,
                group => group.OptionableType != null && typeof(ICustomRole).IsAssignableFrom(group.OptionableType),
                MiraLocaleManager.Get("BetterOtherRoles.Guide.RolesPage", "Roles"),
                formatBool,
                withRoleSpawn: true);
            if (rolesPage != null)
            {
                __result.Add(rolesPage);
            }

            var modifiersPage = BuildPage(
                plugins,
                group => group.OptionableType != null && typeof(BaseModifier).IsAssignableFrom(group.OptionableType),
                MiraLocaleManager.Get("BetterOtherRoles.Guide.ModifiersPage", "Modifiers"),
                formatBool,
                withRoleSpawn: false);
            if (modifiersPage != null)
            {
                __result.Add(modifiersPage);
            }
        }

        private static object? BuildPage(
            IEnumerable<MiraPluginInfo> plugins,
            Func<AbstractOptionGroup, bool> filter,
            string title,
            Func<bool, string> formatBool,
            bool withRoleSpawn)
        {
            var entries = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(EntryType))!;
            var groups = new List<AbstractOptionGroup>();
            var searchTerms = new List<string>();
            MiraPluginInfo? firstPlugin = null;

            foreach (var plugin in plugins)
            {
                var abbreviation = plugin.MiraPlugin.GetAbbreviatedModName();
                foreach (var group in plugin.OptionGroups.Where(filter).Where(SafeGroupVisible))
                {
                    var role = withRoleSpawn ? plugin.Roles.Values.OfType<ICustomRole>().FirstOrDefault(x => x.GetType() == group.OptionableType) : null;
                    if (withRoleSpawn && (role == null || role.Configuration.HideSettings))
                    {
                        continue;
                    }

                    // Roles and game modifiers get a banner with their spawn chance (GuideRoleBannerPatches).
                    var hasBanner = role != null || plugin.Modifiers.OfType<GameModifier>().Any(x => x.GetType() == group.OptionableType);
                    var groupName = group.GroupName.Translate();
                    var terms = (IReadOnlyList<string>)GetModSearchTermsMethod.Invoke(null, [plugin, group])!;
                    var count = entries.Count;

                    // Folded banner: no options, everything stays searchable.
                    var showOptions = !hasBanner || GuideRoleBannerPatches.IsExpanded(group) || GuideRoleBannerPatches.IsSearching;
                    foreach (var option in group.Children.Where(x => showOptions && SafeOptionVisible(x)))
                    {
                        var entry = TryCreateEntryMethod.Invoke(null, [option, group, groupName, abbreviation, terms, formatBool]);
                        if (entry != null)
                        {
                            entries.Add(entry);
                        }
                    }

                    // MiraAPI only draws a group header above rows: a hidden placeholder row keeps the banner.
                    if (hasBanner && entries.Count == count)
                    {
                        entries.Add(CreateEntry(GuideRoleBannerPatches.PlaceholderTitle, string.Empty, group, groupName, abbreviation, terms));
                    }

                    if (entries.Count > count)
                    {
                        groups.Add(group);
                        searchTerms.AddRange(terms);
                        firstPlugin ??= plugin;
                    }
                }
            }

            if (groups.Count == 0)
            {
                return null;
            }

            var page = Activator.CreateInstance(
                PageType,
                groups[0],
                title,
                int.MaxValue,
                int.MaxValue,
                groups[0].GroupPriority,
                2,
                firstPlugin!.MiraPlugin.GetAbbreviatedModName(),
                (IReadOnlyList<string>)searchTerms.Distinct().ToList(),
                entries)!;

            // Several groups: MiraAPI then shows the group of each entry, as a header.
            var pageGroups = (List<AbstractOptionGroup>)AccessTools.Property(PageType, "Groups").GetValue(page)!;
            pageGroups.AddRange(groups.Skip(1));
            return page;
        }

        private static bool SafeGroupVisible(AbstractOptionGroup group)
        {
            try
            {
                return group.GroupVisible();
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool SafeOptionVisible(IModdedOption option)
        {
            try
            {
                return option.Visible();
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    [HarmonyPatch]
    public static class SearchPatch
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(PagesType, "Search");
        }

        /// <summary>
        ///     Search of our own instead of MiraAPI's: MiraAPI also matches the mod "search terms" (plugin name, code
        ///     namespaces: "role" finds "BetterOtherRoles") and then lists every option of the page. Here, as MiraAPI
        ///     ranks them: same title, then title, then group or role name, then value; without case nor accents, in the
        ///     game language only. The vanilla settings matching the search (all of them, not only MiraAPI's) come first.
        /// </summary>
        public static void Postfix(IList __result, IEnumerable pages, string query)
        {
            query = query.Trim();
            var guide = MatchInfoGuide.Instance;
            __result.Clear();
            if (query.Length == 0 || !guide)
            {
                return;
            }

            var plugins = (MiraPluginInfo[])RegisteredPluginsProperty.GetValue(PluginManagerInstanceProperty.GetValue(null))!;
            var roles = plugins.SelectMany(x => x.Roles.Values.OfType<ICustomRole>())
                .GroupBy(x => x.GetType())
                .ToDictionary(x => x.Key, x => x.First());

            var ranked = new List<(object Entry, int Rank)>();
            foreach (var page in pages)
            {
                foreach (var entry in (IEnumerable)PageEntriesProperty.GetValue(page)!)
                {
                    var rank = Rank(entry, query, roles);
                    if (rank < int.MaxValue)
                    {
                        ranked.Add((entry, rank));
                    }
                }
            }

            foreach (var (entry, _) in ranked.OrderBy(x => x.Rank))
            {
                __result.Add(entry);
            }

            var groupName = VanillaGroup.GroupName;
            var index = 0;
            foreach (var (title, value) in GuideSearchText.GetVanillaSettings())
            {
                if (GuideSearchText.Matches(title, query) || GuideSearchText.Matches(value, query))
                {
                    __result.Insert(index++, CreateEntry(title, value, VanillaGroup, groupName, "AU", []));
                }
            }
        }

        private static int Rank(object entry, string query, Dictionary<Type, ICustomRole> roles)
        {
            var group = (AbstractOptionGroup)EntryGroupProperty.GetValue(entry)!;
            var title = (string)EntryTitleProperty.GetValue(entry)!;
            if (GuideSearchText.Equal(title, query))
            {
                return 0;
            }

            if (GuideSearchText.Matches(title, query))
            {
                return 1;
            }

            var groupName = (string)EntryGroupNameProperty.GetValue(entry)!;
            if (GuideSearchText.Matches(groupName, query) ||
                (group.OptionableType != null && roles.TryGetValue(group.OptionableType, out var role) && GuideSearchText.Matches(role.RoleName, query)))
            {
                return 2;
            }

            return GuideSearchText.Matches((string)EntryValueProperty.GetValue(entry)!, query) ? 3 : int.MaxValue;
        }
    }

    /// <summary>
    ///     Group shown as header of the vanilla settings in the search results. Not registered in MiraAPI.
    /// </summary>
    [MiraIgnore]
    private sealed class VanillaGuideGroup : AbstractOptionGroup
    {
        public override string GroupName => MiraLocaleManager.Get("BetterOtherRoles.Guide.VanillaGroup", "Among Us");
    }
}
