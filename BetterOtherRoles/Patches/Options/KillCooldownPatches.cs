using System.Reflection;
using AmongUs.GameOptions;
using BetterOtherRoles.Options;
using HarmonyLib;
using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Translation;
using UnityEngine;

namespace BetterOtherRoles.Patches.Options;

/// <summary>
///     Kill of the impostors: cooldown of the game from "Instant" (0) to 120 s (10 to 60 in the game), and its start
///     cooldown (<see cref="GameplayOptions.KillStartCooldown"/>, 10 s fixed in the game), shown under it in the vanilla
///     game settings tab. The start cooldown may be longer than the kill cooldown: the game keeps the kill timer under
///     it, so it is kept under the start cooldown until it ends.
/// </summary>
[HarmonyPatch]
public static class KillCooldownPatches
{
    private const string OptionName = "BorKillStartCooldownOption";

    // Height of a row of the vanilla settings (GameOptionsMenu.CreateSettings).
    public const float RowHeight = 0.45f;

    // A kill timer set this much above the current one is a reset (meeting, kill), not a tick of the game.
    private const float ResetGap = 1f;

    private static NumberOption? _option;
    private static FloatGameSetting? _viewData;

    // Control of a MiraAPI option (protected setter).
    private static readonly PropertyInfo OptionBehaviourProperty =
        AccessTools.Property(typeof(ModdedNumberOption), nameof(ModdedNumberOption.OptionBehaviour));

    // Maximum of the kill timer while the start cooldown runs (longer than the kill cooldown), 0 after.
    private static float _startMax;

    private static float KillCooldown => GameOptionsManager.Instance.CurrentGameOptions.GetFloat(FloatOptionNames.KillCooldown);

    private static float StartCooldown => OptionGroupSingleton<GameplayOptions>.Instance.KillStartCooldown.Value;

    /// <summary>
    ///     Kill cooldown of the menu: 0 to 120 s.
    /// </summary>
    [HarmonyPatch(typeof(NumberOption), nameof(NumberOption.SetUpFromData))]
    [HarmonyPostfix]
    public static void SetUpFromDataPostfix(NumberOption __instance)
    {
        if (__instance.floatOptionName != FloatOptionNames.KillCooldown) return;

        __instance.ValidRange = new FloatRange(BorCooldownOptions.Min, BorCooldownOptions.Max);
    }

    /// <summary>
    ///     "Instant" for a kill cooldown of 0 (menu, lobby settings, notifications).
    /// </summary>
    [HarmonyPatch(typeof(FloatGameSetting), nameof(FloatGameSetting.GetValueString))]
    [HarmonyPostfix]
    public static void GetValueStringPostfix(FloatGameSetting __instance, float value, ref string __result)
    {
        var killSetting = __instance.OptionName == FloatOptionNames.KillCooldown || (_viewData && __instance == _viewData);
        if (killSetting && Mathf.Approximately(value, 0f))
            __result = MiraLocaleManager.Get("BetterOtherRoles.Setting.Instant", "Instant");
    }

    /// <summary>
    ///     Start cooldown of the kill, under the kill cooldown of the game (MiraAPI builds the tab, GameOptionsMenuPatch).
    /// </summary>
    [HarmonyPatch(typeof(GameOptionsMenu), nameof(GameOptionsMenu.Initialize))]
    [HarmonyPostfix]
    public static void MenuInitializePostfix(GameOptionsMenu __instance)
    {
        var killCooldown = FindKillCooldown(__instance);
        if (!killCooldown) return;

        var container = killCooldown!.transform.parent;
        var existing = container.Find(OptionName);
        if (existing)
        {
            _option = existing.GetComponent<NumberOption>();
            __instance.Children?.Remove(_option);
            AddToController(__instance);
            return;
        }

        // Everything under the kill cooldown goes one row down.
        var rowY = killCooldown.transform.localPosition.y;
        for (var i = 0; i < container.childCount; i++)
        {
            var child = container.GetChild(i);
            var position = child.localPosition;
            if (position.y < rowY - 0.1f)
                child.localPosition = new Vector3(position.x, position.y - RowHeight, position.z);
        }

        var menu = __instance;
        var option = OptionGroupSingleton<GameplayOptions>.Instance.KillStartCooldown;
        var previous = option.OptionBehaviour;
        var behaviour = option.CreateOption(menu.checkboxOrigin, menu.numberOptionOrigin, menu.stringOptionOrigin, menu.playerOptionOrigin, container);

        // CreateOption makes the row the control of the option: MiraAPI would hide it with the hidden options of its
        // group (UpdateGroup). Its control given back: MiraAPI leaves the row alone.
        OptionBehaviourProperty.SetValue(option, previous);
        behaviour.name = OptionName;
        var killPosition = killCooldown.transform.localPosition;
        behaviour.transform.localPosition = new Vector3(killPosition.x, killPosition.y - RowHeight, killPosition.z);
        behaviour.SetClickMask(menu.ButtonClickMask);
        if (AmongUsClient.Instance && !AmongUsClient.Instance.AmHost)
            behaviour.SetAsPlayer();

        _option = behaviour.TryCast<NumberOption>();
        AddToController(menu);

    }

    /// <summary>
    ///     Shows the value set by the host (MiraAPI only updates the control it knows, not this row).
    /// </summary>
    /// <param name="value">The new value.</param>
    public static void Refresh(float value)
    {
        if (!_option || Mathf.Approximately(_option!.Value, value)) return;

        _option.Value = value;
    }

    /// <summary>
    ///     Start of the game (end of the intro, after the 10 s of the game): kill timer of the impostors.
    /// </summary>
    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.OnDestroy))]
    [HarmonyPostfix]
    public static void IntroDestroyPostfix()
    {
        _startMax = 0f;
        var player = PlayerControl.LocalPlayer;
        if (!player || player.Data == null || !player.Data.Role || !player.Data.Role.CanUseKillButton) return;

        var start = StartCooldown;
        var cooldown = KillCooldown;
        if (start > cooldown)
        {
            // Set here: SetKillTimer keeps it under the kill cooldown.
            _startMax = start;
            player.killTimer = start;
            HudManager.Instance.KillButton.SetCoolDown(start, start);
        }
        else
        {
            player.SetKillTimer(start);
        }
    }

    /// <summary>
    ///     While the start cooldown runs, the kill timer goes down from it, not from the kill cooldown. With an instant
    ///     kill cooldown (0), the game leaves the button as it was (cooling down): set ready here.
    /// </summary>
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetKillTimer))]
    [HarmonyPrefix]
    public static bool SetKillTimerPrefix(PlayerControl __instance, float time)
    {
        if (!__instance.AmOwner || !__instance.Data.Role.CanUseKillButton) return true;

        if (_startMax > 0f)
        {
            // Reset of the game (meeting, kill): the start cooldown is over.
            if (time > __instance.killTimer + ResetGap)
            {
                _startMax = 0f;
            }
            else
            {
                __instance.killTimer = Mathf.Clamp(time, 0f, _startMax);
                HudManager.Instance.KillButton.SetCoolDown(__instance.killTimer, _startMax);
                if (__instance.killTimer <= 0f) _startMax = 0f;
                return false;
            }
        }

        if (KillCooldown > 0f) return true;

        __instance.killTimer = 0f;
        HudManager.Instance.KillButton.SetCoolDown(0f, 1f);
        return false;
    }

    /// <summary>
    ///     Settings view (lobby): the start cooldown after the kill cooldown, in the list of the game while it is drawn.
    /// </summary>
    [HarmonyPatch(typeof(LobbyViewSettingsPane), nameof(LobbyViewSettingsPane.DrawNormalTab))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void DrawNormalTabPrefix(out RulesCategory? __state)
    {
        __state = null;
        if (!GameManager.Instance) return;

        var data = ViewData();

        foreach (var category in GameManager.Instance.GameSettingsList.AllCategories)
        {
            var settings = category.AllGameSettings;
            for (var i = 0; i < settings.Count; i++)
            {
                var setting = settings[i].TryCast<FloatGameSetting>();
                if (!setting || setting!.OptionName != FloatOptionNames.KillCooldown) continue;

                settings.Insert(i + 1, data);
                __state = category;
                return;
            }
        }
    }

    [HarmonyPatch(typeof(LobbyViewSettingsPane), nameof(LobbyViewSettingsPane.DrawNormalTab))]
    [HarmonyFinalizer]
    public static void DrawNormalTabFinalizer(RulesCategory? __state)
    {
        if (__state == null) return;

        __state.AllGameSettings.Remove(ViewData());
    }

    /// <summary>
    ///     Value of the start cooldown for the settings view (not a setting of the game).
    /// </summary>
    [HarmonyPatch(typeof(IGameOptionsExtensions), nameof(IGameOptionsExtensions.GetValue))]
    [HarmonyPostfix]
    public static void GetValuePostfix(BaseGameSetting data, ref float __result)
    {
        if (_viewData && data == _viewData) __result = OptionGroupSingleton<GameplayOptions>.Instance.KillStartCooldown.Value;
    }

    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.OnDestroy))]
    [HarmonyPostfix]
    public static void ShipDestroyPostfix() => _startMax = 0f;

    private static NumberOption? FindKillCooldown(GameOptionsMenu menu)
    {
        foreach (var option in menu.GetComponentsInChildren<NumberOption>(true))
        {
            if (option.floatOptionName == FloatOptionNames.KillCooldown && option.name != OptionName) return option;
        }

        return null;
    }

    /// <summary>
    ///     Title and value of the start cooldown of the kill, for the role guide (list of the settings of the game).
    /// </summary>
    /// <returns>The line.</returns>
    public static (string Title, string Value) StartCooldownLine()
    {
        var data = ViewData();
        return (TranslationController.Instance.GetString(data.Title), data.GetValueString(StartCooldown));
    }

    // Setting of the game for the settings view: the option of MiraAPI only has one with its control.
    private static FloatGameSetting ViewData()
    {
        if (_viewData) return _viewData!;

        var option = OptionGroupSingleton<GameplayOptions>.Instance.KillStartCooldown;
        _viewData = ScriptableObject.CreateInstance<FloatGameSetting>();
        _viewData.name = OptionName;
        _viewData.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        _viewData.Type = OptionTypes.Float;
        _viewData.Title = option.StringName;
        _viewData.Increment = option.Increment;
        _viewData.ValidRange = new FloatRange(option.Min, option.Max);
        _viewData.SuffixType = NumberSuffixes.Seconds;
        _viewData.FormatString = "0.0";
        return _viewData;
    }

    private static void AddToController(GameOptionsMenu menu)
    {
        if (!_option || menu.ControllerSelectable == null) return;

        foreach (var element in _option!.GetComponentsInChildren<UiElement>())
        {
            if (element.gameObject.activeSelf && !menu.ControllerSelectable.Contains(element))
                menu.ControllerSelectable.Add(element);
        }
    }

    /// <summary>
    ///     Gets whether the row is shown (room made for it in the scrolled height, <see cref="SettingsScrollPatches"/>).
    /// </summary>
    public static bool RowShown => _option && _option!.gameObject.activeInHierarchy;
}
