using HarmonyLib;

namespace BetterOtherRoles.Patches.Misc;

[HarmonyPatch(typeof(MeetingHud))]
public static class MeetingHudPatches
{
    private static int _proceededMeetingId;
    private static MeetingHud.MeetingStates _lastState;

    /// <summary>
    ///     In local games, the vanilla meeting shows a "Proceed" button after the votes and waits for a click.
    ///     Dev builds hide it and click it automatically, so tests with several instances are not slowed down.
    /// </summary>
    [HarmonyPatch(nameof(MeetingHud.Update))]
    [HarmonyPostfix]
    public static void UpdatePostfix(MeetingHud __instance)
    {
        if (!BetterOtherRolesPlugin.IsDevBuild || AmongUsClient.Instance.NetworkMode != NetworkModes.LocalGame)
        {
            return;
        }

        if (__instance.state != _lastState)
        {
            _lastState = __instance.state;
            Info($"[Dev] Meeting state: {__instance.state}, Proceed button active: {__instance.ProceedButton.gameObject.activeInHierarchy}");
        }

        if (__instance.state is not (MeetingHud.MeetingStates.Results or MeetingHud.MeetingStates.Proceeding) ||
            !__instance.ProceedButton.gameObject.activeInHierarchy ||
            _proceededMeetingId == __instance.GetInstanceID())
        {
            return;
        }

        _proceededMeetingId = __instance.GetInstanceID();
        Info($"[Dev] Auto Proceed (state: {__instance.state})");
        __instance.ProceedButton.OnClick.Invoke();
        __instance.ProceedButton.gameObject.SetActive(false);
    }
}
