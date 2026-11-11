using HarmonyLib;
using TMPro;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     The host shown in meetings, in online games (BetterOtherRoles 1.7.1 MeetingPatch.ShowHost, TOU-Mira 1.7.9
///     ShowHostMeetingPatch): the "Proceed" button of the game, used by local games only (the host ends the meeting with
///     it), is shown at the top left without its background nor click, with the icon in the color of the host and
///     "Host: name", the word translated by the game.
/// </summary>
[HarmonyPatch]
public static class ShowHostPatches
{
    private static readonly Vector3 Place = new(-2.5f, 2.2f, 0f);

    private static TextMeshPro? hostText;

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    [HarmonyPostfix]
    public static void MeetingStartPostfix(MeetingHud __instance)
    {
        hostText = null;
        if (AmongUsClient.Instance.NetworkMode != NetworkModes.OnlineGame) return;

        var button = __instance.ProceedButton;
        button.transform.localPosition = Place;
        button.GetComponent<SpriteRenderer>().enabled = false;
        button.enabled = false;
        __instance.HostIcon.enabled = true;
        __instance.HostIcon.gameObject.SetActive(true);
        button.gameObject.SetActive(true);
        hostText = button.GetComponentInChildren<TextMeshPro>();
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
    [HarmonyPostfix]
    public static void MeetingUpdatePostfix(MeetingHud __instance)
    {
        if (!hostText || !GameData.Instance) return;

        var host = GameData.Instance.GetHost();
        if (!host) return;

        PlayerMaterial.SetColors(host.DefaultOutfit.ColorId, __instance.HostIcon);
        var text = $"{TranslationController.Instance.GetString(StringNames.HostNounEmpty)}: {host.PlayerName}";
        if (hostText!.text != text)
            hostText.text = text;
    }
}
