using System.Collections;
using BetterOtherRoles.Modules.Localization;
using HarmonyLib;
using Reactor.Utilities;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     The mod follows the game language: when it changes (Data tab of the options), the texts already displayed are
///     translated again. The game settings menu builds its texts once: it is closed and opened again (on the last page
///     seen), and the settings view of the clients is drawn again.
/// </summary>
[HarmonyPatch(typeof(TranslationController), nameof(TranslationController.SetLanguage))]
public static class GameLanguagePatches
{
    [HarmonyPostfix]
    public static void SetLanguagePostfix()
    {
        BorLocale.RefreshTexts();

        var view = Object.FindObjectOfType<LobbyViewSettingsPane>();
        if (view && view.isActiveAndEnabled)
            view.RefreshTab();

        if (GameSettingMenu.Instance && GameStartManager.InstanceExists)
        {
            GameSettingMenu.Instance.Close();
            Coroutines.Start(CoReopenSettings());
        }
    }

    // One frame later: the menu is destroyed at the end of the frame.
    private static IEnumerator CoReopenSettings()
    {
        yield return null;
        if (!GameSettingMenu.Instance && GameStartManager.InstanceExists && LobbyBehaviour.Instance)
            GameStartManager.Instance.ClickEdit();
    }
}
