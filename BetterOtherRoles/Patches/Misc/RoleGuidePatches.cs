using System.Collections;
using System.Reflection;
using System.Text;
using AmongUs.GameOptions;
using BetterOtherRoles.Assets;
using BetterOtherRoles.Options.Roles.Crewmate;
using BetterOtherRoles.Roles.Crewmate;
using HarmonyLib;
using MiraAPI.GameOptions;
using MiraAPI.Modifiers.Types;
using MiraAPI.Modifiers;
using MiraAPI.Roles;
using MiraAPI.Translation;
using MiraAPI.Utilities.Assets;
using MiraAPI;
using Reactor.Utilities.Extensions;
using TMPro;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Role guide (Match Info guide of MiraAPI 0.6.0), one nested class per part:
///     - <see cref="RolesTab"/>: roles tab (tabs below the guide on narrow screens, search without accents, Deputy counts);
///     - <see cref="SettingsTab"/>: settings tab ("Roles" and "Modifiers" pages, vanilla settings in the search);
///     - <see cref="RoleBanner"/>: group headers and role banners of the settings tab (spawn chance, open on click);
///     - <see cref="SettingsTitle"/>: titles "{Group} (3/5)" of the settings pages;
///     - <see cref="SearchText"/>: search without case nor accents, shared by both tabs;
///     - <see cref="BackButton"/>: back and close buttons in the title bar of the guide;
///     - <see cref="LastPage"/>: the guide opens again on the last page seen;
///     - <see cref="OverSettings"/>: the lobby settings view is closed before the guide opens.
/// </summary>
public static class RoleGuidePatches
{
    /// <summary>
    ///     Role guide (Match Info) of MiraAPI 0.6.0.
    /// </summary>
    [HarmonyPatch(typeof(MiraAPI.Patches.RoleGuidePatches))]
    public static class RolesTab
    {
        private static readonly FieldInfo RoleEntriesField = AccessTools.Field(typeof(MiraAPI.Patches.RoleGuidePatches), "RoleEntries");
        private static readonly FieldInfo RolesScrollerField = AccessTools.Field(typeof(MiraAPI.Patches.RoleGuidePatches), "_rolesScroller");
        private static readonly FieldInfo SearchBoxField = AccessTools.Field(typeof(MiraAPI.Patches.RoleGuidePatches), "_searchBoxTmp");
        private static readonly MethodInfo ShouldShowPanelMethod = AccessTools.Method(typeof(MiraAPI.Patches.RoleGuidePatches), "ShouldShowPanel");
        private static readonly PropertyInfo PanelProperty =
            AccessTools.Property(AccessTools.Inner(typeof(MiraAPI.Patches.RoleGuidePatches), "DetailedPanel"), "Panel");

        // Below this screen ratio (16:10 = 1.6, 4:3 = 1.33), the tabs on the sides of the guide are off screen.
        private const float MinSideTabsAspectRatio = 1.5f;

        /// <summary>
        ///     On narrow screens, the tabs on the left or right side of the guide are off screen and cannot be clicked:
        ///     put them below the guide, whatever the MiraAPI local setting.
        /// </summary>
        [HarmonyPatch("SetUpTabGrids")]
        [HarmonyPrefix]
        public static void SetUpTabGridsPrefix(ref MatchInfoTabPos position)
        {
            if ((float)Screen.width / Screen.height < MinSideTabsAspectRatio)
            {
                position = MatchInfoTabPos.Below;
            }
        }

        /// <summary>
        ///     The roles tab search compares the names shown with their accents: also accept them without case nor accents
        ///     ("sherif" finds "Shérif"), in the game language only, as the settings tab search (<see cref="SearchText"/>).
        /// </summary>
        [HarmonyPatch("MatchesSearch")]
        [HarmonyPostfix]
        public static void MatchesSearchPostfix(object entry, string searchText, ref bool __result)
        {
            if (__result || searchText.Length == 0)
            {
                return;
            }

            var role = Traverse.Create(entry).Property("Role").GetValue<RoleBehaviour>();
            if (role == null)
            {
                return;
            }

            var name = role is ICustomRole customRole ? customRole.RoleName : role.NiceName;
            __result = SearchText.Matches(name, searchText);
        }

        /// <summary>
        ///     The Deputy is not drawn by MiraAPI (count 0), it comes with the Sheriff: show its real values,
        ///     taken from the Sheriff options (1 at DeputyChance %, when the Sheriff has a Deputy).
        /// </summary>
        [HarmonyPatch("ComputeCounts")]
        [HarmonyPostfix]
        public static void ComputeCountsPostfix(object entry)
        {
            var traverse = Traverse.Create(entry);
            var role = traverse.Property("Role").GetValue<RoleBehaviour>();
            if (role == null || (ushort)role.Role != RoleId.Get<DeputyRole>())
            {
                return;
            }

            var options = OptionGroupSingleton<SheriffOptions>.Instance;
            var amount = options.HasDeputy ? 1 : 0;
            var chance = options.HasDeputy ? (int)options.DeputyChance.Value : 0;
            var canShow = (bool)ShouldShowPanelMethod.Invoke(null, [null, amount == 0 || chance == 0, false])!;

            traverse.Property("Amount").SetValue(amount);
            traverse.Property("Chance").SetValue(chance);
            traverse.Property("Likelihood").SetValue((float)(amount * chance));
            traverse.Property("CanShow").SetValue(canShow);
            traverse.Property("Visible").SetValue(canShow);
        }

        /// <summary>
        ///     Fixes a MiraAPI 0.6.0 bug: after each refresh, RemoveUntrackedRolePanels destroys the panels missing from its
        ///     own list, but compares them by reference. Il2Cpp gives new C# wrappers on each GetComponentsInChildren, so its
        ///     own panels are seen as untracked and destroyed: the list shows up for a moment, then disappears.
        ///     Same method, compared by native pointer.
        /// </summary>
        [HarmonyPatch("RemoveUntrackedRolePanels")]
        [HarmonyPrefix]
        public static bool RemoveUntrackedRolePanelsPrefix(ref bool __result)
        {
            var scroller = RolesScrollerField.GetValue(null) as Scroller;
            if (scroller == null || !scroller.Inner)
            {
                __result = false;
                return false;
            }

            var trackedPanels = new HashSet<IntPtr>();
            foreach (var entry in (IEnumerable)RoleEntriesField.GetValue(null)!)
            {
                if (PanelProperty.GetValue(entry) is MatchInfoRolePanel panel && panel)
                {
                    trackedPanels.Add(panel.Pointer);
                }
            }

            var removed = new List<string>();
            foreach (var panel in scroller.Inner.GetComponentsInChildren<MatchInfoRolePanel>(true))
            {
                if (trackedPanels.Contains(panel.Pointer))
                {
                    continue;
                }

                removed.Add(panel.roleName ? panel.roleName.text : panel.name);
                panel.gameObject.Destroy();
            }

            if (removed.Count > 0)
            {
                Warning($"[RoleGuide] Removed {removed.Count} untracked role panel(s): {string.Join(", ", removed)}");
            }

            __result = true;
            return false;
        }

        /// <summary>
        ///     The search box of the guide (copy of the chat box) puts its caret in the middle while it is empty: at the
        ///     left, as in our other boxes.
        /// </summary>
        [HarmonyPatch("Initialize")]
        [HarmonyPostfix]
        public static void InitializePostfix()
        {
            if (SearchBoxField.GetValue(null) is TextBoxTMP box)
            {
                TextBoxPatches.FixCaret(box);
            }
        }
    }

    /// <summary>
    ///     Settings tab of the MiraAPI 0.6.0 role guide (Match Info). MiraAPI only lists the vanilla settings and the
    ///     option groups not linked to a role, and only searches in these groups. Added here:
    ///     - a "Roles" page (count and chance of each role, then its options) and a "Modifiers" page (when there are any);
    ///     - the vanilla settings in the search results.
    ///     The page and entry types are internal to MiraAPI: they are created by reflection.
    /// </summary>
    [HarmonyPatch]
    public static class SettingsTab
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

                        // Roles and game modifiers get a banner with their spawn chance (RoleBanner).
                        var hasBanner = role != null || plugin.Modifiers.OfType<GameModifier>().Any(x => x.GetType() == group.OptionableType);
                        var groupName = group.GroupName.Translate();
                        var terms = (IReadOnlyList<string>)GetModSearchTermsMethod.Invoke(null, [plugin, group])!;
                        var count = entries.Count;

                        // Folded banner: no options, everything stays searchable.
                        var showOptions = !hasBanner || RoleBanner.IsExpanded(group) || RoleBanner.IsSearching;
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
                            entries.Add(CreateEntry(RoleBanner.PlaceholderTitle, string.Empty, group, groupName, abbreviation, terms));
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
                foreach (var (title, value) in SearchText.GetVanillaSettings())
                {
                    if (SearchText.Matches(title, query) || SearchText.Matches(value, query))
                    {
                        __result.Insert(index++, CreateEntry(title, value, VanillaGroup, groupName, "AU", []));
                    }
                }
            }

            private static int Rank(object entry, string query, Dictionary<Type, ICustomRole> roles)
            {
                var group = (AbstractOptionGroup)EntryGroupProperty.GetValue(entry)!;
                var title = (string)EntryTitleProperty.GetValue(entry)!;
                if (SearchText.Equal(title, query))
                {
                    return 0;
                }

                if (SearchText.Matches(title, query))
                {
                    return 1;
                }

                var groupName = (string)EntryGroupNameProperty.GetValue(entry)!;
                if (SearchText.Matches(groupName, query) ||
                    (group.OptionableType != null && roles.TryGetValue(group.OptionableType, out var role) && SearchText.Matches(role.RoleName, query)))
                {
                    return 2;
                }

                return SearchText.Matches((string)EntryValueProperty.GetValue(entry)!, query) ? 3 : int.MaxValue;
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

    /// <summary>
    ///     Headers of the settings tab of the role guide (<see cref="SettingsTab"/>): every MiraAPI group header
    ///     becomes rectangular, in the group color. On the "Roles" and "Modifiers" pages, the header of each role or
    ///     game modifier is a banner in its color, with its spawn chance and "click to open" / "click to close". Folded (default): the banner only;
    ///     open: the role options too. During a search, everything is shown and the banners cannot be clicked.
    /// </summary>
    [HarmonyPatch]
    public static class RoleBanner
    {
        private static readonly Assembly MiraAssembly = typeof(MiraPluginManager).Assembly;
        private static readonly Type RowType = MiraAssembly.GetType("MiraAPI.Utilities.AdvancedSettingsRow", true)!;
        private static readonly PropertyInfo RowGroupProperty = AccessTools.Property(RowType, "Group");
        private static readonly MethodInfo RefreshSettingsTabMethod = AccessTools.Method(typeof(MiraAPI.Patches.RoleGuidePatches), "RefreshSettingsTab");
        private static readonly MethodInfo GetSearchTextMethod = AccessTools.Method(typeof(MiraAPI.Patches.RoleGuidePatches), "GetSearchText");

        /// <summary>
        ///     Title of the hidden row under a folded banner (MiraAPI draws no header for a group without rows).
        /// </summary>
        public const string PlaceholderTitle = "⁣";

        private const string HintName = "BorBannerHint";

        // Width of the banners, relative to the MiraAPI headers (they end at the second settings column).
        private const float BannerWidthRatio = 0.75f;

        // Space between the right text of a banner and its right edge, in banner local units.
        private const float HintPadding = 0.15f;

        // Gap MiraAPI adds between two groups of a settings page.
        private const float GroupGap = 0.2f;

        // Open banners, kept while the game runs.
        private static readonly HashSet<Type> ExpandedRoles = [];

        private static Sprite? _bannerSprite;

        /// <summary>
        ///     Gets a value indicating whether a search is typed in the guide (all the options are then listed).
        /// </summary>
        public static bool IsSearching => ((string)GetSearchTextMethod.Invoke(null, null)!).Trim().Length > 0;

        public static bool IsExpanded(AbstractOptionGroup group)
        {
            return group.OptionableType != null && ExpandedRoles.Contains(group.OptionableType);
        }

        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(MiraAPI.Utilities.Helpers),
                "CreateAdvancedSettingsPage",
                [typeof(Scroller), typeof(List<>).MakeGenericType(RowType), typeof(int), typeof(bool)]);
        }

        /// <summary>
        ///     MiraAPI creates one header per group, in the order of the rows: restyle them.
        /// </summary>
        public static void Postfix(GameObject __result, IList settings)
        {
            var groups = settings.Cast<object>()
                .Select(x => (AbstractOptionGroup?)RowGroupProperty.GetValue(x))
                .Where(x => x != null)
                .Distinct()
                .ToList();
            var headers = __result.GetComponentsInChildren<CategoryHeaderMasked>(true);
            if (headers.Length != groups.Count)
            {
                return;
            }

            var banners = GetBanners();
            var searching = IsSearching;
            for (var i = 0; i < headers.Length; i++)
            {
                var group = groups[i]!;

                // MiraAPI copies the header of the previous page: always remove what was added to it.
                CleanHeader(headers[i]);
                if (group.OptionableType != null && banners.TryGetValue(group.OptionableType, out var banner))
                {
                    MakeBanner(headers[i], group.OptionableType, banner.Color, banner.Chance, searching);
                }
                else
                {
                    // Other groups (vanilla settings, dev options...): same rectangular look, without spawn chance nor click.
                    MakeRectangular(headers[i], group.GroupColor);
                }
            }

            HidePlaceholderRows(__result.transform);
        }

        /// <summary>
        ///     Hides the placeholder rows of the folded banners (one line each) and moves what is below them up.
        ///     MiraAPI places the headers and rows one line (or more) under the other, with a 0.2 gap between groups.
        /// </summary>
        private static void HidePlaceholderRows(Transform page)
        {
            var children = Enumerable.Range(0, page.childCount).Select(page.GetChild).ToList();
            var headerIndexes = children.Select((x, i) => (x, i)).Where(x => x.x.GetComponent<CategoryHeaderMasked>()).Select(x => x.i).ToList();
            var shift = 0f;
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (shift > 0f)
                {
                    child.localPosition += new Vector3(0f, shift, 0f);
                }

                var row = child.GetComponent<NumberOption>();
                if (!row || row.TitleText.text != PlaceholderTitle)
                {
                    continue;
                }

                child.gameObject.SetActive(false);

                // Header of this row, and the next one: they are 2 lines + the gap apart, 1 line + the gap once hidden.
                var header = headerIndexes.LastOrDefault(x => x < i, -1);
                var next = headerIndexes.FirstOrDefault(x => x > i, -1);
                if (header >= 0 && next >= 0)
                {
                    var line = (children[header].localPosition.y - children[next].localPosition.y - GroupGap) / 2f;
                    shift += Mathf.Max(line, 0f);
                }
            }
        }

        /// <summary>
        ///     MiraAPI only shows the group headers when a page has several groups, side by side: on the "Roles" and "Modifiers"
        ///     pages, always show them, one under the other.
        /// </summary>
        [HarmonyPatch]
        public static class ShowSettingsEntriesPatch
        {
            [HarmonyTargetMethod]
            public static MethodBase ShowSettingsEntriesMethod()
            {
                return AccessTools.Method(typeof(MiraAPI.Patches.RoleGuidePatches), "ShowSettingsEntries");
            }

            public static void Prefix(IList entries, ref bool showGroup, ref bool groupsAsColumns)
            {
                var entryGroupProperty = AccessTools.Property(entries.GetType().GetGenericArguments()[0], "Group");
                var isRolesPage = entries.Count > 0 && entries.Cast<object>().All(x =>
                    ((AbstractOptionGroup)entryGroupProperty.GetValue(x)!).OptionableType is { } type &&
                    (typeof(ICustomRole).IsAssignableFrom(type) || typeof(BaseModifier).IsAssignableFrom(type)));
                if (isRolesPage)
                {
                    showGroup = true;
                    groupsAsColumns = false;
                }
            }
        }

        /// <summary>
        ///     Color and spawn chance of the roles and game modifiers, by type.
        /// </summary>
        private static Dictionary<Type, (Color Color, int Chance)> GetBanners()
        {
            var plugins = (MiraPluginInfo[])AccessTools.Property(typeof(MiraPluginManager), "RegisteredPlugins")
                .GetValue(AccessTools.Property(typeof(MiraPluginManager), "Instance").GetValue(null))!;
            var roles = plugins.SelectMany(x => x.Roles.Values.OfType<ICustomRole>())
                .Select(x => (Type: x.GetType(), Color: x.RoleColor, Chance: x.GetChance() ?? 0));
            var modifiers = plugins.SelectMany(x => x.Modifiers.OfType<GameModifier>())
                .Select(x => (Type: x.GetType(), Color: x.GeneralColor, Chance: x.GetAssignmentChance()));
            return roles.Concat(modifiers)
                .GroupBy(x => x.Type)
                .ToDictionary(x => x.Key, x => (x.First().Color, x.First().Chance));
        }

        private static void MakeBanner(CategoryHeaderMasked header, Type roleType, Color color, int spawnChance, bool searching)
        {
            var background = header.Background;
            if (!background)
            {
                return;
            }

            var textColor = MakeRectangular(header, color);
            var size = background.size;
            var expanded = ExpandedRoles.Contains(roleType);
            if (header.Title)
            {
                // Spawn chance, then (except during a search, where everything is shown) "click to open / close".
                var chance = MiraLocaleManager.Get("BetterOtherRoles.Guide.RoleChanceValue", "{0}%")
                    .Replace("{0}", spawnChance.ToString(System.Globalization.CultureInfo.InvariantCulture));
                var toggleText = MiraLocaleManager.Get(
                    expanded ? "BetterOtherRoles.Guide.ClickToClose" : "BetterOtherRoles.Guide.ClickToOpen",
                    expanded ? "Click to close" : "Click to open");

                var hint = Object.Instantiate(header.Title, header.Title.transform.parent);
                hint.name = HintName;

                // The vanilla translator of the title (only queued for destruction by MiraAPI) would rewrite the text.
                var translator = hint.GetComponent<TextTranslatorTMP>();
                if (translator)
                {
                    Object.DestroyImmediate(translator);
                }

                hint.text = searching ? chance : $"{chance}   ·   {toggleText}";
                hint.horizontalAlignment = HorizontalAlignmentOptions.Right;
                hint.enableWordWrapping = false;
                hint.overflowMode = TextOverflowModes.Overflow;
                hint.enableAutoSizing = false;
                hint.fontSize *= 0.7f;
                hint.fontSizeMax = hint.fontSize;
                hint.color = textColor;
                hint.fontMaterial.SetFloat("_StencilComp", 3f);
                hint.fontMaterial.SetFloat("_Stencil", 50f);
                hint.renderer.sortingOrder = header.Title.renderer.sortingOrder;

                // Right edge of the text on the right edge of the banner, a bit inside (pivot on the right).
                var bannerRight = background.transform.TransformPoint(new Vector3((size.x / 2f) - HintPadding, 0f, 0f));
                var rect = hint.rectTransform;
                var localRight = rect.parent.InverseTransformPoint(bannerRight);
                rect.pivot = new Vector2(1f, rect.pivot.y);
                rect.localPosition = new Vector3(localRight.x, rect.localPosition.y, rect.localPosition.z);
            }

            if (searching)
            {
                return;
            }

            var collider = background.gameObject.GetComponent<BoxCollider2D>() ?? background.gameObject.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.offset = Vector2.zero;

            var button = background.gameObject.AddComponent<PassiveButton>();
            button.Colliders = new[] { collider };
            button.ClickSound = HudManager.Instance ? HudManager.Instance.MapButton.ClickSound : null;
            button.OnMouseOver = new UnityEvent();
            button.OnMouseOut = new UnityEvent();
            button.OnMouseOver.AddListener((Action)(() => background.color = Color.Lerp(color, Color.white, 0.25f)));
            button.OnMouseOut.AddListener((Action)(() => background.color = color));
            button.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            button.OnClick.AddListener((Action)(() => Toggle(roleType)));
        }

        /// <summary>
        ///     Removes the hint and the click of a banner (left on the headers MiraAPI copies).
        /// </summary>
        private static void CleanHeader(CategoryHeaderMasked header)
        {
            foreach (var oldHint in header.GetComponentsInChildren<TextMeshPro>(true).Where(x => x.name == HintName))
            {
                Object.DestroyImmediate(oldHint.gameObject);
            }

            var oldButton = header.Background ? header.Background.gameObject.GetComponent<PassiveButton>() : null;
            if (oldButton)
            {
                Object.DestroyImmediate(oldButton);
            }
        }

        /// <summary>
        ///     Rectangular header in the given color: a plain white sprite, drawn at the size MiraAPI gave to its rounded one.
        /// </summary>
        /// <returns>The title color, dark on light colors (yellow Sheriff...).</returns>
        private static Color MakeRectangular(CategoryHeaderMasked header, Color color)
        {
            var textColor = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f > 0.6f ? Color.black : Color.white;
            var background = header.Background;
            if (background)
            {
                // Shorter than MiraAPI's header (which goes up to the scroll bar), keeping its left edge
                // (MiraAPI sets the size and positions again on each copy). In local units: the page is still hidden.
                var size = background.size;
                var shrink = size.x * (1f - BannerWidthRatio) * background.transform.localScale.x;
                background.sprite = GetBannerSprite();
                background.drawMode = SpriteDrawMode.Sliced;
                background.size = new Vector2(size.x * BannerWidthRatio, size.y);
                background.transform.localPosition -= new Vector3(shrink / 2f, 0f, 0f);
                background.color = color;
            }

            // The vanilla divider line goes past the banner, onto the scroll bar.
            if (header.Divider)
            {
                header.Divider.gameObject.SetActive(false);
            }

            if (header.Title)
            {
                header.Title.color = textColor;
            }

            return textColor;
        }

        private static void Toggle(Type roleType)
        {
            if (!ExpandedRoles.Remove(roleType))
            {
                ExpandedRoles.Add(roleType);
            }

            RefreshSettingsTabMethod.Invoke(null, [false]);
        }

        private static Sprite GetBannerSprite()
        {
            if (_bannerSprite)
            {
                return _bannerSprite!;
            }

            var texture = new Texture2D(4, 4);
            var pixels = Enumerable.Repeat(Color.white, 16).ToArray();
            texture.SetPixels(pixels);
            texture.Apply();
            texture.hideFlags |= HideFlags.HideAndDontSave;

            _bannerSprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f, 0, SpriteMeshType.FullRect);
            _bannerSprite.hideFlags |= HideFlags.HideAndDontSave;
            return _bannerSprite;
        }
    }

    /// <summary>
    ///     Settings tab of the role guide (Match Info): MiraAPI titles each page "{Group} Settings (3/5)", with "Settings"
    ///     written in English in its code. The title is only the group name in every language: "Salon (3/5)".
    /// </summary>
    [HarmonyPatch]
    public static class SettingsTitle
    {
        private const string SettingsWord = " Settings ";

        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(MiraAPI.Patches.RoleGuidePatches), "GetSettingsTitle");
        }

        public static void Postfix(ref string __result)
        {
            __result = __result.Replace(SettingsWord, " ", StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     Text matching of the role guide search, in the game language: without case nor accents ("maj" finds "Màj",
    ///     "role" finds "Rôle"), and every vanilla setting.
    ///     Accents are removed with a table: the .NET normalization may be missing in the game (invariant globalization).
    /// </summary>
    public static class SearchText
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

    /// <summary>
    ///     Buttons added to the title bar of the role guide (Match Info), copied from the MiraAPI sort button:
    ///     - back: a left arrow (MiraAPI "previous page" sprites) on the left of the close button, on the role and modifier pages,
    ///       going back to the roles or modifiers list, hidden on any tab change;
    ///     - close: a cross in the top right corner of the window, always shown, closing the guide (like Escape).
    /// </summary>
    [HarmonyPatch]
    public static class BackButton
    {
        private static readonly FieldInfo WikiScrollerField = AccessTools.Field(typeof(MiraAPI.Patches.RoleGuidePatches), "_wikiScroller");
        private static readonly FieldInfo OrderButtonField = AccessTools.Field(typeof(MiraAPI.Patches.RoleGuidePatches), "_searchSortingOrderButton");
        private static readonly FieldInfo TitleTextField = AccessTools.Field(typeof(MiraAPI.Patches.RoleGuidePatches), "_titleText");

        // Drawn over the wiki page (role descriptions use the guide sorting orders).
        private const int SortingOrder = 500;

        // Distance of the close button center from the outer corner of the window, in button sizes
        // (the top border of the window is thinner than its right one).
        private const float CornerOffsetX = 1.0f;
        private const float CornerOffsetY = 0.95f;

        // When the window is wider than the screen (4:3): distance of the close button center from the screen edge.
        private const float ScreenEdgeOffset = 0.57f;

        // Height of the close button (about the guide button of the HUD: 55 px of a 600 px high screen, 6 units).
        private const float CloseButtonSize = 0.55f;

        // Space between the back arrow and the close button, in button sizes.
        private const float BackButtonGap = 0.29f;

        // Measured on screenshots (800x600 and 1366x768): the drawn arrow is off its sprite bounds by this much,
        // in close button sizes (to the right and downwards).
        private const float BackButtonCorrectionX = 0.13f;
        private const float BackButtonCorrectionY = 0.07f;

        private static PassiveButton? _backButton;
        private static PassiveButton? _closeButton;
        private static bool _backToModifiers;

        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(MiraAPI.Patches.RoleGuidePatches), "DisplayAdvancedWiki", [typeof(MatchInfoGuide), typeof(RoleBehaviour)]);
            yield return AccessTools.Method(typeof(MiraAPI.Patches.RoleGuidePatches), "DisplayAdvancedWiki", [typeof(MatchInfoGuide), typeof(BaseModifier)]);
        }

        [HarmonyPostfix]
        public static void DisplayAdvancedWikiPostfix(MethodBase __originalMethod)
        {
            var wikiScroller = WikiScrollerField.GetValue(null) as Scroller;
            if (wikiScroller == null || !wikiScroller.gameObject.activeSelf)
            {
                // MiraAPI waits for the end of the opening animation: the page is shown by a later call.
                return;
            }

            _backToModifiers = __originalMethod.GetParameters()[1].ParameterType == typeof(BaseModifier);
            var title = TitleTextField.GetValue(null) as TextMeshPro;
            if (title == null)
            {
                return;
            }

            var parent = title.transform.parent;
            if (!_backButton || _backButton!.transform.parent != parent)
            {
                _backButton = CreateButton(
                    parent,
                    "BorGuideBackButton",
                    new Vector3(title.transform.position.x, title.transform.position.y, title.transform.position.z - 1f),
                    MiraAssets.PageLeftIdle.LoadAsset(),
                    MiraAssets.PageLeftHover.LoadAsset(),
                    GoBack);
            }

            if (_backButton)
            {
                PlaceNextToCloseButton(_backButton!);
            }

            if (_backButton)
            {
                _backButton!.gameObject.SetActive(true);
            }
        }

        /// <summary>
        ///     Any tab change (roles list, modifiers, settings, players) leaves the role page.
        ///     In its own class: HarmonyX does not allow TargetMethods with other annotated patches in the same class.
        /// </summary>
        [HarmonyPatch(typeof(MatchInfoGuide), nameof(MatchInfoGuide.SetActiveTab))]
        public static class SetActiveTabPatch
        {
            public static void Postfix()
            {
                if (_backButton)
                {
                    _backButton!.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        ///     Close button, created once the opening animation of the guide is over (positions are wrong during it).
        /// </summary>
        [HarmonyPatch(typeof(MatchInfoGuide))]
        public static class CloseButtonPatch
        {
            private static float _openedAt;

            [HarmonyPatch(nameof(MatchInfoGuide.Open))]
            [HarmonyPostfix]
            public static void OpenPostfix()
            {
                _openedAt = Time.time;
            }

            [HarmonyPatch(nameof(MatchInfoGuide.Update))]
            [HarmonyPostfix]
            public static void UpdatePostfix(MatchInfoGuide __instance)
            {
                if (!__instance.MatchInfoParent.activeSelf || Time.time < _openedAt + __instance.transitionOpen.duration + 0.1f)
                {
                    return;
                }

                var title = TitleTextField.GetValue(null) as TextMeshPro;
                if (title == null || (_closeButton && _closeButton!.transform.parent == title.transform.parent))
                {
                    return;
                }

                // Top right corner of the window frame: the smallest sprite around both the title and the sort buttons
                // (the biggest one is the full screen shade).
                var orderButton = OrderButtonField.GetValue(null) as PassiveButton;
                var titlePosition = title.transform.position;
                var orderPosition = orderButton ? orderButton!.transform.position : titlePosition;
                var background = __instance.MatchInfoParent.GetComponentsInChildren<SpriteRenderer>(true)
                    .Where(x => x.sprite && Contains(x.bounds, titlePosition) && Contains(x.bounds, orderPosition))
                    .OrderBy(x => x.bounds.size.x * x.bounds.size.y)
                    .FirstOrDefault();
                if (!background)
                {
                    // Tried again on the next opening only.
                    Warning("[Guide] No window frame found for the close button");
                    _openedAt = float.MaxValue;
                    return;
                }

                _closeButton = CreateCloseButton(__instance, title, background!.bounds.max);
            }

            /// <summary>
            ///     A close button like the game settings one, at the size of the guide button of the HUD, in the top right corner of the
            ///     window, kept inside the screen (the window is wider than a 4:3 screen).
            /// </summary>
            private static PassiveButton CreateCloseButton(MatchInfoGuide guide, TextMeshPro title, Vector3 corner)
            {
                // Size of the guide button of the HUD, when it can be measured: in the first game, it can still be appearing
                // (scaled down) when the guide opens, and the cross was tiny.
                var size = CloseButtonSize;
                var hudButton = HudManager.Instance ? HudManager.Instance.MatchInfoButton : null;
                var hudSprite = hudButton ? hudButton!.GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(x => x.enabled && x.sprite) : null;
                if (hudSprite && hudSprite!.bounds.size.y >= CloseButtonSize * 0.8f && hudSprite.bounds.size.y <= CloseButtonSize * 1.25f)
                {
                    size = hudSprite.bounds.size.y;
                }

                // Inside the window frame (about 0.4 button thick), in its top right corner.
                corner.x -= size * CornerOffsetX;
                corner.y -= size * CornerOffsetY;
                var camera = HudManager.Instance ? HudManager.Instance.UICamera : Camera.main;
                if (camera)
                {
                    var screenMax = camera!.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));
                    corner.x = Mathf.Min(corner.x, screenMax.x - (size * ScreenEdgeOffset));
                }

                var gameObject = new GameObject("BorGuideCloseButton") { layer = title.gameObject.layer };
                gameObject.transform.SetParent(title.transform.parent, false);
                gameObject.transform.position = new Vector3(corner.x, corner.y, title.transform.position.z - 1f);

                var renderer = gameObject.AddComponent<SpriteRenderer>();
                var idle = Keep(BorAssets.CloseButton.LoadAsset());
                var hover = Keep(BorAssets.CloseButtonHover.LoadAsset());
                renderer.sprite = idle;
                renderer.sortingOrder = SortingOrder;
                gameObject.transform.localScale = Vector3.one;
                var scale = size / renderer.bounds.size.y;
                gameObject.transform.localScale = new Vector3(scale, scale, 1f);

                var collider = gameObject.AddComponent<BoxCollider2D>();
                collider.size = idle.bounds.size;

                var button = gameObject.AddComponent<PassiveButton>();
                button.Colliders = new Collider2D[] { collider };
                button.ClickSound = HudManager.Instance ? HudManager.Instance.MapButton.ClickSound : null;
                button.OnMouseOver = new UnityEngine.Events.UnityEvent();
                button.OnMouseOut = new UnityEngine.Events.UnityEvent();
                button.OnMouseOver.AddListener((Action)(() => renderer.sprite = hover));
                button.OnMouseOut.AddListener((Action)(() => renderer.sprite = idle));
                button.OnClick = new Button.ButtonClickedEvent();
                button.OnClick.AddListener((Action)(() => guide.Close()));
                return button;
            }

            // The game unloads the unused assets when the scene changes: the hover sprite (only used under the mouse) lost its
            // texture after the first game, and the cross disappeared under the mouse.
            private static Sprite Keep(Sprite sprite)
            {
                sprite.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                sprite.texture.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                return sprite;
            }

            private static bool Contains(Bounds bounds, Vector3 position)
            {
                return position.x >= bounds.min.x && position.x <= bounds.max.x && position.y >= bounds.min.y && position.y <= bounds.max.y;
            }
        }

        /// <summary>
        ///     On the left of the close button, centered on it. Positioned with all its drawn sprites
        ///     (the copied sort button draws its background square with another renderer than the arrow).
        /// </summary>
        private static void PlaceNextToCloseButton(PassiveButton button)
        {
            var close = _closeButton ? _closeButton!.GetComponent<SpriteRenderer>() : null;
            var arrow = button.inactiveSprites ? button.inactiveSprites.GetComponent<SpriteRenderer>() : null;
            if (!close || !arrow || arrow!.bounds.size.y <= 0f)
            {
                return;
            }

            button.transform.localScale *= close!.bounds.size.y / arrow.bounds.size.y;

            var drawn = button.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(x => x.enabled && x.gameObject.activeSelf && x.sprite)
                .Select(x => x.bounds)
                .ToList();
            if (drawn.Count == 0)
            {
                return;
            }

            var bounds = drawn[0];
            foreach (var other in drawn.Skip(1))
            {
                bounds.Encapsulate(other);
            }

            var closeBounds = close.bounds;
            var gap = closeBounds.size.y * BackButtonGap;
            button.transform.position += new Vector3(
                closeBounds.min.x - gap - bounds.max.x + (closeBounds.size.y * BackButtonCorrectionX),
                closeBounds.center.y - bounds.center.y - (closeBounds.size.y * BackButtonCorrectionY),
                0f);
            var position = button.transform.position;
            button.transform.position = new Vector3(position.x, position.y, close.transform.position.z);
        }

        private static void GoBack()
        {
            if (_backToModifiers)
            {
                MiraAPI.Patches.RoleGuidePatches.OpenModifiersTab();
            }
            else
            {
                MiraAPI.Patches.RoleGuidePatches.OpenRolesTab();
            }
        }

        /// <summary>
        ///     Copy of the MiraAPI sort button, without its label, with other sprites and action.
        /// </summary>
        private static PassiveButton? CreateButton(Transform parent, string name, Vector3 position, Sprite idle, Sprite hover, Action onClick)
        {
            var orderButton = OrderButtonField.GetValue(null) as PassiveButton;
            if (orderButton == null)
            {
                return null;
            }

            var button = Object.Instantiate(orderButton, parent);
            button.name = name;
            button.gameObject.SetActive(true);
            button.transform.localScale = orderButton.transform.lossyScale.x > 0f
                ? orderButton.transform.lossyScale / parent.lossyScale.x
                : Vector3.one;
            button.transform.position = position;

            // The sort button only takes clicks inside the search bar mask, hidden on the wiki pages.
            button.ClickMask = null;
            foreach (var renderer in button.GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.maskInteraction = SpriteMaskInteraction.None;
                renderer.sortingOrder = SortingOrder;
            }

            foreach (var text in button.GetComponentsInChildren<TextMeshPro>(true))
            {
                Object.Destroy(text.gameObject);
            }

            SetSprite(button.inactiveSprites.GetComponent<SpriteRenderer>(), idle);
            SetSprite(button.activeSprites.GetComponent<SpriteRenderer>(), hover);

            button.OnClick = new Button.ButtonClickedEvent();
            button.OnClick.AddListener(onClick);
            return button;
        }

        /// <summary>
        ///     Same size as the sort button sprite (as MiraAPI does for its "previous" / "next" page buttons).
        /// </summary>
        private static void SetSprite(SpriteRenderer renderer, Sprite sprite)
        {
            if (!renderer)
            {
                return;
            }

            var height = renderer.sprite ? renderer.sprite.bounds.size.y * renderer.transform.localScale.y : 0f;
            var scale = height > 0f && sprite.bounds.size.y > 0f ? height / sprite.bounds.size.y : 1f;
            renderer.sprite = sprite;
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    /// <summary>
    ///     Role guide (Match Info): MiraAPI always opens it on the roles list. It opens on the settings the first time,
    ///     then on the last page shown (tab, settings page, role or modifier page), for as long as the game runs.
    ///     Kept here and not in MiraAPI, which forgets everything when a new guide is created (each lobby and game).
    /// </summary>
    [HarmonyPatch]
    public static class LastPage
    {
        private static readonly FieldInfo SettingsPageField = AccessTools.Field(typeof(MiraAPI.Patches.RoleGuidePatches), "_settingsPage");

        private const int PlayersTabIndex = 0;
        private const int SettingsTabIndex = 1;
        private const int ModifiersTabIndex = 3;

        private static int _lastTab = SettingsTabIndex;
        private static int _lastSettingsPage;
        private static RoleTypes? _lastRole;
        private static BaseModifier? _lastModifier;

        // MiraAPI opens the roles tab itself in Open: not a choice of the player.
        private static bool _opening;

        // Before the MiraAPI prefix (priority First): it returns false, which skips the prefixes after it.
        [HarmonyPatch(typeof(MatchInfoGuide), nameof(MatchInfoGuide.Open))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First + 100)]
        public static void OpenPrefix()
        {
            _opening = true;
        }

        [HarmonyPatch(typeof(MatchInfoGuide), nameof(MatchInfoGuide.Open))]
        [HarmonyPostfix]
        public static void OpenPostfix(MatchInfoGuide __instance)
        {
            try
            {
                if (__instance.MatchInfoParent.activeSelf && GameManager.Instance.TryCast<NormalGameManager>() != null)
                {
                    Restore(__instance);
                }
            }
            finally
            {
                _opening = false;
            }
        }

        private static void Restore(MatchInfoGuide guide)
        {
            SettingsPageField.SetValue(null, _lastSettingsPage);

            switch (_lastTab)
            {
                case ModifiersTabIndex:
                    MiraAPI.Patches.RoleGuidePatches.OpenModifiersTab();
                    break;
                case PlayersTabIndex:
                case SettingsTabIndex:
                    guide.SetActiveTab(_lastTab);
                    break;
            }

            // MiraAPI shows the page once the opening animation is over.
            if (_lastModifier != null)
            {
                MiraAPI.Patches.RoleGuidePatches.DisplayAdvancedWiki(guide, _lastModifier);
            }
            else if (_lastRole is { } roleType &&
                     CustomRoleManager.AllStoredRoleBehaviours.FirstOrDefault(role => role && role.Role == roleType) is { } role)
            {
                MiraAPI.Patches.RoleGuidePatches.DisplayAdvancedWiki(guide, role);
            }
        }

        /// <summary>
        ///     Any tab change leaves the role or modifier page.
        /// </summary>
        [HarmonyPatch(typeof(MatchInfoGuide), nameof(MatchInfoGuide.SetActiveTab))]
        [HarmonyPostfix]
        public static void SetActiveTabPostfix(int tabIndex)
        {
            if (_opening)
            {
                return;
            }

            _lastTab = tabIndex;
            _lastRole = null;
            _lastModifier = null;
        }

        [HarmonyPatch(typeof(MiraAPI.Patches.RoleGuidePatches), "ChangeSettingsPage")]
        [HarmonyPostfix]
        public static void ChangeSettingsPagePostfix()
        {
            _lastSettingsPage = (int)SettingsPageField.GetValue(null)!;
        }

        [HarmonyPatch(typeof(MiraAPI.Patches.RoleGuidePatches), nameof(MiraAPI.Patches.RoleGuidePatches.DisplayAdvancedWiki), typeof(MatchInfoGuide), typeof(RoleBehaviour))]
        [HarmonyPostfix]
        public static void DisplayRoleWikiPostfix(RoleBehaviour role)
        {
            _lastRole = role.Role;
            _lastModifier = null;
        }

        [HarmonyPatch(typeof(MiraAPI.Patches.RoleGuidePatches), nameof(MiraAPI.Patches.RoleGuidePatches.DisplayAdvancedWiki), typeof(MatchInfoGuide), typeof(BaseModifier))]
        [HarmonyPostfix]
        public static void DisplayModifierWikiPostfix(BaseModifier modifier)
        {
            _lastModifier = modifier;
            _lastRole = null;
        }
    }

    /// <summary>
    ///     A client could open the role guide over the game settings view of the lobby (LobbyViewSettingsPane): both
    ///     were drawn mixed together, and the mouse wheel scrolled the settings. The settings view is closed first.
    /// </summary>
    [HarmonyPatch(typeof(MatchInfoGuide), nameof(MatchInfoGuide.Open))]
    public static class OverSettings
    {
        // Before the MiraAPI prefix (priority First): it returns false, which skips the prefixes after it.
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First + 100)]
        public static void OpenPrefix()
        {
            if (!GameStartManager.InstanceExists || !GameStartManager.Instance.LobbyInfoPane)
            {
                return;
            }

            var settingsView = GameStartManager.Instance.LobbyInfoPane.LobbyViewSettingsPane;
            // Its back button, as the player would close it.
            if (settingsView && settingsView.gameObject.activeSelf && settingsView.backButton)
            {
                settingsView.backButton.OnClick.Invoke();
            }
        }
    }
}
