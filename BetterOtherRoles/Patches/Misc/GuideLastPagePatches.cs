using System.Reflection;
using AmongUs.GameOptions;
using HarmonyLib;
using MiraAPI.Modifiers;
using MiraAPI.Roles;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Role guide (Match Info): MiraAPI always opens it on the roles list. It opens on the settings the first time,
///     then on the last page shown (tab, settings page, role or modifier page), for as long as the game runs.
///     Kept here and not in MiraAPI, which forgets everything when a new guide is created (each lobby and game).
/// </summary>
[HarmonyPatch]
public static class GuideLastPagePatches
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
