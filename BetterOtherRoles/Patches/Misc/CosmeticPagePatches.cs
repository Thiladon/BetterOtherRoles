using System.Reflection;
using BetterOtherRoles.Modules.Editions;
using CorsacCosmetics.Components;
using CorsacCosmetics.Cosmetics;
using HarmonyLib;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Page of the hats, visors and pets tabs (CorsacCosmetics groups) kept until the game closes: the page component
///     of CorsacCosmetics goes away with the customization menu, so the tab opened again showed the first page. When
///     the tab opens without it, the component is added with the page kept, before CorsacCosmetics fills the tab (it
///     uses the one already there).
/// </summary>
[HarmonyPatch]
public static class CosmeticPagePatches
{
    // Page kept per tab.
    private static readonly Dictionary<string, int> Pages = [];

    private static readonly PropertyInfo CurrentTab = AccessTools.Property(typeof(InventoryTabPaginationBehaviour), nameof(InventoryTabPaginationBehaviour.CurrentTab));

    /// <summary>
    ///     OnEnable of the three tabs (a class of its own: Harmony does not mix TargetMethods with annotated methods).
    /// </summary>
    [HarmonyPatch]
    public static class TabOnEnable
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(HatsTab), nameof(HatsTab.OnEnable));
            yield return AccessTools.Method(typeof(VisorsTab), nameof(VisorsTab.OnEnable));
            yield return AccessTools.Method(typeof(PetsTab), nameof(PetsTab.OnEnable));
        }

        // Before CorsacCosmetics, which replaces OnEnable. Only when the menu was opened again: a change of page
        // enables the tab again with the component still there.
        [HarmonyPriority(Priority.First)]
        public static void Prefix(InventoryTab __instance)
        {
            var key = GetKey(__instance);
            if (key == nameof(HatsTab))
                BorEditions.HideEterzxPage();
            if (key == null || !Pages.TryGetValue(key, out var page) || __instance.GetComponent<InventoryTabPaginationBehaviour>()) return;

            // A page no longer there (the Eterzx one left out): the first page.
            if (key == nameof(HatsTab) && CosmeticsCatalog.Instance != null && page > CosmeticsCatalog.Instance.HatGroups.Count)
                page = 0;

            var pagination = __instance.gameObject.AddComponent<InventoryTabPaginationBehaviour>();
            CurrentTab.SetValue(pagination, page);
        }
    }

    [HarmonyPatch(typeof(InventoryTabPaginationBehaviour), nameof(InventoryTabPaginationBehaviour.NextPage))]
    [HarmonyPatch(typeof(InventoryTabPaginationBehaviour), nameof(InventoryTabPaginationBehaviour.PreviousPage))]
    [HarmonyPostfix]
    public static void PageChangedPostfix(InventoryTabPaginationBehaviour __instance)
    {
        var key = __instance.tab ? GetKey(__instance.tab) : null;
        if (key != null)
            Pages[key] = __instance.CurrentTab;
    }

    // The tab, from its real type (GetType() of a game object can give the base type).
    private static string? GetKey(InventoryTab tab)
    {
        if (tab.TryCast<HatsTab>()) return nameof(HatsTab);
        if (tab.TryCast<VisorsTab>()) return nameof(VisorsTab);
        return tab.TryCast<PetsTab>() ? nameof(PetsTab) : null;
    }
}
