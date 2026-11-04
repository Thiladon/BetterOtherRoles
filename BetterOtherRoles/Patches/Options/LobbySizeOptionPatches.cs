using System.Reflection;
using BetterOtherRoles.Options;
using HarmonyLib;
using MiraAPI.GameOptions;
using UnityEngine;

namespace BetterOtherRoles.Patches.Options;

/// <summary>
///     Lobby size option (<see cref="LobbyOptions.MaxPlayers"/>) shown in the vanilla game settings tab (Main), under the
///     game mode and its banner, instead of the Lobby group of the BetterOtherRoles tab. MiraAPI builds this tab itself
///     (GameOptionsMenuPatch.CreateSettings): the vanilla categories below the banner are moved down to make room.
/// </summary>
[HarmonyPatch]
public static class LobbySizeOptionPatches
{
    private const string OptionName = "BorLobbySizeOption";

    // Name of the game mode banner made by MiraAPI.
    private const string BannerName = "GamemodeInfo";

    // Top of the first vanilla category in MiraAPI, right under the banner.
    private const float FirstCategoryY = -1.217f;

    // Position of the option (x of the vanilla options in MiraAPI) and room made for it.
    private const float OptionX = 0.952f;
    private const float OptionY = -1.32f;
    private const float Shift = 0.55f;

    private static NumberOption? _option;

    [HarmonyPatch(typeof(GameOptionsMenu), nameof(GameOptionsMenu.Initialize))]
    [HarmonyPostfix]
    public static void InitializePostfix(GameOptionsMenu __instance)
    {
        var banner = FindBanner(__instance);
        if (!banner)
        {
            return;
        }

        var container = banner!.parent;
        var existing = container.Find(OptionName);
        if (existing)
        {
            _option = existing.GetComponent<NumberOption>();

            // MiraAPI takes back every option of the tab as a vanilla one: ours is a MiraAPI option.
            __instance.Children?.Remove(_option);
            AddToController(__instance);
            return;
        }

        // Everything under the banner goes down (vanilla categories and the game mode groups).
        for (var i = 0; i < container.childCount; i++)
        {
            var child = container.GetChild(i);
            var position = child.localPosition;
            if (position.y < FirstCategoryY + 0.1f)
            {
                child.localPosition = new Vector3(position.x, position.y - Shift, position.z);
            }
        }

        var menu = __instance;
        var option = OptionGroupSingleton<LobbyOptions>.Instance.MaxPlayers;
        var behaviour = option.CreateOption(menu.checkboxOrigin, menu.numberOptionOrigin, menu.stringOptionOrigin, menu.playerOptionOrigin, container);
        behaviour.name = OptionName;
        behaviour.transform.localPosition = new Vector3(OptionX, OptionY, -2f);
        behaviour.SetClickMask(menu.ButtonClickMask);
        if (AmongUsClient.Instance && !AmongUsClient.Instance.AmHost)
        {
            behaviour.SetAsPlayer();
        }

        // The scrolled height already has the room for the option (ToggleGameModePatch, called while MiraAPI builds the tab).
        _option = behaviour.TryCast<NumberOption>();
        AddToController(menu);
    }

    /// <summary>
    ///     Shows the value set elsewhere (/size, the host for the players): MiraAPI only updates the last view it made.
    /// </summary>
    public static void Refresh(float value)
    {
        if (_option && !Mathf.Approximately(_option!.Value, value))
        {
            _option.Value = value;
            _option.UpdateValue();
        }
    }

    private static Transform? FindBanner(GameOptionsMenu menu)
    {
        foreach (var transform in menu.GetComponentsInChildren<Transform>(true))
        {
            if (transform.name == BannerName && transform.gameObject.activeInHierarchy)
            {
                return transform;
            }
        }

        return null;
    }

    private static void AddToController(GameOptionsMenu menu)
    {
        if (!_option || menu.ControllerSelectable == null)
        {
            return;
        }

        foreach (var element in _option!.GetComponentsInChildren<UiElement>())
        {
            if (element.gameObject.activeSelf && !menu.ControllerSelectable.Contains(element))
            {
                menu.ControllerSelectable.Add(element);
            }
        }
    }

    /// <summary>
    ///     MiraAPI sets the scrolled height again when the game mode changes, without the room made for the option.
    /// </summary>
    [HarmonyPatch]
    public static class ToggleGameModePatch
    {
        // Internal class of MiraAPI.
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(MiraPluginManager).Assembly.GetType("MiraAPI.Patches.Options.GameOptionsMenuPatch", true), "ToggleGamemodeOptions");
        }

        public static void Postfix(GameOptionsMenu instance)
        {
            if (instance && instance.scrollBar)
            {
                instance.scrollBar.SetYBoundsMax(instance.scrollBar.ContentYBounds.max + Shift);
            }
        }
    }
}
