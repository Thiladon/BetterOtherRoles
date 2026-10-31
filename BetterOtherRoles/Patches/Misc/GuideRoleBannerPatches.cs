using System.Collections;
using System.Reflection;
using HarmonyLib;
using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
using MiraAPI.Modifiers.Types;
using MiraAPI.Roles;
using MiraAPI.Translation;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Headers of the settings tab of the role guide (<see cref="GuideSettingsPatches"/>): every MiraAPI group header
///     becomes rectangular, in the group color. On the "Roles" and "Modifiers" pages, the header of each role or
///     game modifier is a banner in its color, with its spawn chance and "click to open" / "click to close". Folded (default): the banner only;
///     open: the role options too. During a search, everything is shown and the banners cannot be clicked.
/// </summary>
[HarmonyPatch]
public static class GuideRoleBannerPatches
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
