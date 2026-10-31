using System.Collections;
using System.Reflection;
using BetterOtherRoles.Options.Roles.Crewmate;
using BetterOtherRoles.Roles.Crewmate;
using HarmonyLib;
using MiraAPI;
using MiraAPI.GameOptions;
using MiraAPI.Roles;
using Reactor.Utilities.Extensions;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Role guide (Match Info) of MiraAPI 0.6.0.
/// </summary>
[HarmonyPatch(typeof(MiraAPI.Patches.RoleGuidePatches))]
public static class RoleGuidePatches
{
    private static readonly FieldInfo RoleEntriesField = AccessTools.Field(typeof(MiraAPI.Patches.RoleGuidePatches), "RoleEntries");
    private static readonly FieldInfo RolesScrollerField = AccessTools.Field(typeof(MiraAPI.Patches.RoleGuidePatches), "_rolesScroller");
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
    ///     ("sherif" finds "Shérif"), in the game language only, as the settings tab search (<see cref="GuideSearchText"/>).
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
        __result = GuideSearchText.Matches(name, searchText);
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
}
