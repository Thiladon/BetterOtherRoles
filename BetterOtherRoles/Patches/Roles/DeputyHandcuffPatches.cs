using System.Globalization;
using BetterOtherRoles.Assets;
using BetterOtherRoles.Modules.Roles;
using HarmonyLib;
using MiraAPI.Hud;
using TMPro;
using UnityEngine;

namespace BetterOtherRoles.Patches.Roles;

/// <summary>
///     Deputy handcuffs on the handcuffed player's side (BetterOtherRoles 1.7.1: CustomButton, UsablesPatch, UpdatePatch):
///     - a valid use of an ability, kill or report button triggers the pending handcuff, and the action is cancelled;
///     - while the handcuff is active, these buttons show the handcuffed sprite with the time left, and do nothing.
///     Vents stay usable (choice for BOR-MiraAPI, BetterOtherRoles 1.7.1 blocked them).
/// </summary>
[HarmonyPatch]
public static class DeputyHandcuffPatches
{
    // Buttons currently handcuffed, with their own sprite to put back and the handcuff countdown added on them.
    private static readonly Dictionary<IntPtr, CuffedButton> CuffedButtons = [];

    private static readonly HashSet<IntPtr> FailingButtons = [];

    [HarmonyPatch(typeof(CustomActionButton), nameof(CustomActionButton.ClickHandler))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static bool CustomButtonClickPrefix(CustomActionButton __instance)
    {
        if (DeputyHandcuffs.IsLocalActive)
        {
            return false;
        }

        return !__instance.CanClick() || !DeputyHandcuffs.TryBlockLocalAction();
    }

    [HarmonyPatch(typeof(KillButton), nameof(KillButton.DoClick))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static bool KillButtonDoClickPrefix(KillButton __instance)
    {
        if (!__instance.isActiveAndEnabled || !__instance.currentTarget || __instance.isCoolingDown ||
            !DeputyHandcuffs.TryBlockLocalAction())
        {
            return true;
        }

        // MiraAPI kills in its own DoClick prefix: without a target, it stops there too.
        __instance.SetTarget(null);
        return false;
    }

    [HarmonyPatch(typeof(ReportButton), nameof(ReportButton.DoClick))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static bool ReportButtonDoClickPrefix(ReportButton __instance)
    {
        if (!__instance.isActiveAndEnabled || __instance.graphic.color != Palette.EnabledColor)
        {
            return true;
        }

        return !DeputyHandcuffs.TryBlockLocalAction();
    }

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    [HarmonyPostfix]
    public static void HudManagerUpdatePostfix(HudManager __instance)
    {
        DeputyHandcuffs.Update(Time.deltaTime);

        if (PlayerControl.LocalPlayer && DeputyHandcuffs.Active.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out var timeLeft))
        {
            ShowHandcuffs(__instance, timeLeft);
        }
        else if (CuffedButtons.Count > 0)
        {
            HideHandcuffs();
        }
    }

    private static IEnumerable<ActionButton> GetLocalButtons(HudManager hud)
    {
        yield return hud.KillButton;
        yield return hud.ReportButton;

        foreach (var button in CustomButtonManager.Buttons)
        {
            if (button.Button)
            {
                yield return button.Button!;
            }
        }
    }

    private static void ShowHandcuffs(HudManager hud, float timeLeft)
    {
        var handcuffedSprite = BorAssets.DeputyHandcuffed.LoadAsset();
        var countdownText = Mathf.CeilToInt(timeLeft).ToString(CultureInfo.InvariantCulture);

        foreach (var button in GetLocalButtons(hud))
        {
            if (!button || !button.isActiveAndEnabled || !button.graphic)
            {
                continue;
            }

            if (!CuffedButtons.TryGetValue(button.Pointer, out var cuffed))
            {
                cuffed = new CuffedButton(button, button.graphic.sprite, button.graphic.sharedMaterial, CreateCountdown(hud, button));
                CuffedButtons[button.Pointer] = cuffed;
            }

            // Set again every frame, after the game and MiraAPI updated the button: same look on every button,
            // full colors without the cooldown shadow, and only our countdown.
            button.graphic.sprite = handcuffedSprite;

            // The kill button keeps a lighter tint: give every button the material of the report button.
            if (hud.ReportButton && hud.ReportButton.graphic && button != hud.ReportButton)
            {
                button.graphic.sharedMaterial = hud.ReportButton.graphic.sharedMaterial;
            }
            if (button.buttonLabelText)
            {
                button.buttonLabelText.enabled = false;
            }

            // A cooldown at 0 for the display only (the kill button keeps a tint while its cooldown runs).
            // The real cooldowns are stored elsewhere: the game and MiraAPI show them again after the handcuff.
            // SetCoolDown throws on the report button (no cooldown text). Il2CppInterop logs the error before it
            // reaches any catch: only call it on buttons with a cooldown text.
            TryUpdateButton(button, () =>
            {
                if (button.cooldownTimerText)
                {
                    button.SetCoolDown(0f, 1f);
                }

                button.SetEnabled();
            });

            if (button.cooldownTimerText)
            {
                button.cooldownTimerText.gameObject.SetActive(false);
            }

            if (cuffed.Countdown)
            {
                cuffed.Countdown!.text = countdownText;
            }
        }
    }

    /// <summary>
    ///     The report button has no cooldown text: every handcuffed button gets its own, copied from the kill button.
    /// </summary>
    private static TextMeshPro? CreateCountdown(HudManager hud, ActionButton button)
    {
        var source = hud.KillButton ? hud.KillButton.cooldownTimerText : null;
        if (!source)
        {
            return null;
        }

        var countdown = UnityEngine.Object.Instantiate(source, button.transform);
        countdown!.name = "HandcuffCountdown";
        countdown.transform.localPosition = source!.transform.localPosition;
        countdown.color = Color.white;
        countdown.gameObject.SetActive(true);
        return countdown;
    }

    /// <summary>
    ///     Some ActionButton methods throw on some vanilla buttons: skip them on these buttons,
    ///     without stopping the other buttons nor logging an error every frame.
    /// </summary>
    private static void TryUpdateButton(ActionButton button, Action update)
    {
        if (FailingButtons.Contains(button.Pointer))
        {
            return;
        }

        try
        {
            update();
        }
        catch (Exception exception)
        {
            FailingButtons.Add(button.Pointer);
            Warning($"[Deputy] Cannot update the handcuffed button {button.name}: {exception.Message}");
        }
    }

    private static void HideHandcuffs()
    {
        // The game and MiraAPI set the button state and cooldown display again on their next update.
        foreach (var cuffed in CuffedButtons.Values)
        {
            if (cuffed.Countdown)
            {
                UnityEngine.Object.Destroy(cuffed.Countdown!.gameObject);
            }

            var button = cuffed.Button;
            if (!button)
            {
                continue;
            }

            if (button.graphic)
            {
                button.graphic.sprite = cuffed.Sprite;
                button.graphic.sharedMaterial = cuffed.Material;
            }

            if (button.buttonLabelText)
            {
                button.buttonLabelText.enabled = true;
            }
        }

        CuffedButtons.Clear();
    }

    private sealed record CuffedButton(ActionButton Button, Sprite Sprite, Material Material, TextMeshPro? Countdown);
}
