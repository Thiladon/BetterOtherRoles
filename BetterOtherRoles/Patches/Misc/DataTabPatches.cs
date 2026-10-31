using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     The Data tab (game language, privacy policy, terms of use, account...) only exists in the main menu options.
///     It is copied there, then added to the lobby and game options menu as a third tab (General, Graphics, Data).
/// </summary>
[HarmonyPatch(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.Start))]
public static class DataTabPatches
{
    private const string DataTabName = "DataTab";

    // Tab buttons of the lobby menu, laid out like the main menu (1.7 apart, centered).
    private const float TabSpacing = 1.7f;

    // Copy of the main menu Data tab, kept across scenes (inactive, its components do not run).
    private static GameObject? _template;
    private static StringNames _tabName = StringNames.None;

    [HarmonyPostfix]
    public static void StartPostfix(OptionsMenuBehaviour __instance)
    {
        if (__instance.transform.Find(DataTabName) != null)
        {
            CaptureTemplate(__instance);
        }
        else if (_template != null)
        {
            AddDataTab(__instance);
        }
    }

    /// <summary>
    ///     Copies the Data tab of a main menu options menu. Also called when the main menu starts
    ///     (<see cref="DataTabTemplatePatches"/>): the options menu only starts once opened by the player.
    /// </summary>
    public static void CaptureTemplate(OptionsMenuBehaviour menu)
    {
        var dataTabTransform = menu.transform.Find(DataTabName);
        if (_template != null || dataTabTransform == null)
        {
            return;
        }

        var dataTab = dataTabTransform.gameObject;
        var tabButton = menu.Tabs.FirstOrDefault(x => x.Content == dataTab);
        var translator = tabButton != null ? tabButton.GetComponentInChildren<TextTranslatorTMP>(true) : null;
        if (translator == null)
        {
            Warning("Data tab: the tab button of the main menu was not found.");
            return;
        }

        _tabName = translator.TargetText;
        var holder = new GameObject("BorDataTabTemplate");
        holder.SetActive(false);
        Object.DontDestroyOnLoad(holder);
        _template = Object.Instantiate(dataTab, holder.transform);
        _template.name = DataTabName;
    }

    private static void AddDataTab(OptionsMenuBehaviour menu)
    {
        var tabs = menu.Tabs.ToList();
        var graphics = tabs.FirstOrDefault(x => x.name == "GraphicsButton");
        var general = tabs.FirstOrDefault(x => x.name == "GeneralButton");
        if (graphics == null || general == null)
        {
            Warning("Data tab: the General and Graphics tabs were not found.");
            return;
        }

        var content = Object.Instantiate(_template!, menu.transform);
        content.name = DataTabName;
        content.SetActive(false);
        SetLayer(content.transform, LayerMask.NameToLayer("UI"));
        RewireMenuButtons(content.transform, menu);

        var button = Object.Instantiate(graphics, graphics.transform.parent);
        button.name = "DataButton";
        button.Content = content;
        var translator = button.GetComponentInChildren<TextTranslatorTMP>(true);
        if (translator != null)
        {
            translator.TargetText = _tabName;
            translator.ResetText();
        }

        var index = tabs.Count;
        var passiveButton = button.GetComponent<PassiveButton>();
        passiveButton.OnClick = new Button.ButtonClickedEvent();
        passiveButton.OnClick.AddListener((Action)(() => menu.OpenTabGroup(index)));
        button.Close();

        var y = graphics.transform.localPosition.y;
        var z = graphics.transform.localPosition.z;
        general.transform.localPosition = new Vector3(-TabSpacing, y, z);
        graphics.transform.localPosition = new Vector3(0f, y, z);
        button.transform.localPosition = new Vector3(TabSpacing, y, z);

        tabs.Add(button);
        menu.Tabs = new Il2CppReferenceArray<TabGroup>(tabs.ToArray());
    }

    // These buttons call the main menu options menu, destroyed with its scene: they call this one instead.
    private static void RewireMenuButtons(Transform content, OptionsMenuBehaviour menu)
    {
        Rewire(content.Find("PrivacyPolicy"), menu.OpenPrivacyPolicy);
        Rewire(content.Find("TermsOfUse"), menu.OpenTermsOfUse);
        Rewire(content.Find("TermsOfUse/PlayStationTOUPopup/OpenTermsOfUse"), menu.ReallyOpenTermsOfUse);
        Rewire(content.Find("PersonalizedAdsButton"), menu.TogglePersonalizedAd);
    }

    private static void Rewire(Transform? target, Action action)
    {
        var button = target != null ? target.GetComponent<PassiveButton>() : null;
        if (button == null)
        {
            return;
        }

        button.OnClick = new Button.ButtonClickedEvent();
        button.OnClick.AddListener(action);
    }

    // Some vanilla objects are on the Default layer: seen in the main menu, not by the UI camera of the lobby and the game.
    private static void SetLayer(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (var i = 0; i < root.childCount; i++)
        {
            SetLayer(root.GetChild(i), layer);
        }
    }
}

/// <summary>
///     Copies the Data tab when the main menu starts, before the player opens the settings in a lobby
///     (<see cref="DataTabPatches"/>).
/// </summary>
[HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
public static class DataTabTemplatePatches
{
    [HarmonyPostfix]
    public static void StartPostfix()
    {
        foreach (var menu in Resources.FindObjectsOfTypeAll(Il2CppType.From(typeof(OptionsMenuBehaviour))))
        {
            DataTabPatches.CaptureTemplate(menu.Cast<OptionsMenuBehaviour>());
        }
    }
}
