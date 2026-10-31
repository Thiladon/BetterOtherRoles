using HarmonyLib;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     A client could open the role guide over the game settings view of the lobby (LobbyViewSettingsPane): both
///     were drawn mixed together, and the mouse wheel scrolled the settings. The settings view is closed first.
/// </summary>
[HarmonyPatch(typeof(MatchInfoGuide), nameof(MatchInfoGuide.Open))]
public static class GuideOverSettingsPatches
{
    // Before the MiraAPI prefix (priority First): it returns false, which skips the prefixes after it.
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First + 100)]
    public static void OpenPrefix()
    {
        if (!GameStartManager.InstanceExists || !GameStartManager.Instance.LobbyInfoPane)
        {
            return;
        }

        var settingsView = GameStartManager.Instance.LobbyInfoPane.LobbyViewSettingsPane;
        // Its back button, as the player would close it.
        if (settingsView && settingsView.gameObject.activeSelf && settingsView.backButton)
        {
            settingsView.backButton.OnClick.Invoke();
        }
    }
}
