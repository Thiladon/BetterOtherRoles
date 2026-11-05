using Il2CppInterop.Runtime;
using MiraAPI.Hud;
using MiraAPI.Translation;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Modules.Localization;

/// <summary>
///     Handles the localization of BetterOtherRoles through MiraAPI's locale manager.
///     The mod follows the game language (Data tab of the options); a language without a locale file falls back to English.
/// </summary>
public static class BorLocale
{
    /// <summary>
    ///     Registers the embedded locale files (Resources/Locale/*.xml) to MiraAPI.
    /// </summary>
    public static void Initialize()
    {
        MiraLocaleManager.Register(BetterOtherRolesPlugin.Id, "BetterOtherRoles");
    }

    /// <summary>
    ///     Translates again the texts already displayed, after a game language change.
    ///     Texts translated through StringNames (vanilla TextTranslatorTMP, MiraAPI locale strings) are only translated
    ///     when shown: they are reset so the new language applies at once. Menus built later use it anyway.
    ///     The role button labels are written once by MiraAPI (CustomActionButton.CreateButton): written again here.
    /// </summary>
    public static void RefreshTexts()
    {
        foreach (var translator in Object.FindObjectsOfType(Il2CppType.Of<TextTranslatorTMP>()))
        {
            translator.Cast<TextTranslatorTMP>().ResetText();
        }

        foreach (var button in CustomButtonManager.Buttons)
        {
            if (button.Button != null && button.GetType().Assembly == typeof(BorLocale).Assembly)
                button.OverrideName(button.Name.ToUpperInvariant());
        }
    }
}
