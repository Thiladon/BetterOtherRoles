using System.Reflection;
using HarmonyLib;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Settings tab of the role guide (Match Info): MiraAPI titles each page "{Group} Settings (3/5)", with "Settings"
///     written in English in its code. The title is only the group name in every language: "Salon (3/5)".
/// </summary>
[HarmonyPatch]
public static class GuideSettingsTitlePatches
{
    private const string SettingsWord = " Settings ";

    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(MiraAPI.Patches.RoleGuidePatches), "GetSettingsTitle");
    }

    public static void Postfix(ref string __result)
    {
        __result = __result.Replace(SettingsWord, " ", StringComparison.Ordinal);
    }
}
