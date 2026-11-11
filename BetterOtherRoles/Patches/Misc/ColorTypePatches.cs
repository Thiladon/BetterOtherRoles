using BetterOtherRoles.LocalSettings;
using BetterOtherRoles.Utilities;
using HarmonyLib;
using MiraAPI.LocalSettings;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     "(L)" or "(D)" after the name of every player in meetings, from its real color (<see cref="BorColorType"/>), with
///     the local setting <see cref="BorLocalSettingsTab.ShowColorType"/> (BetterOtherRoles 1.7.1 UpdatePatch,
///     "Show Lighter / Darker Color"). The game writes the name once (PlayerVoteArea.SetCosmetics): written again here,
///     so that the setting applies at once.
/// </summary>
[HarmonyPatch]
public static class ColorTypePatches
{
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
    [HarmonyPostfix]
    public static void MeetingUpdatePostfix(MeetingHud __instance)
    {
        if (__instance.playerStates == null || !GameData.Instance) return;

        var show = LocalSettingsTabSingleton<BorLocalSettingsTab>.Instance.ShowColorType.Value;
        foreach (var area in __instance.playerStates)
        {
            if (!area || !area.NameText) continue;

            var data = GameData.Instance.GetPlayerById(area.PlayerId);
            if (!data) continue;

            var name = show ? $"{data.PlayerName} ({BorColorType.GetLetter(data)})" : data.PlayerName;
            if (area.NameText.text != name)
                area.NameText.text = name;
        }
    }
}
