using BetterOtherRoles.Utilities;
using HarmonyLib;
using MiraAPI.Translation;
using MiraAPI.Utilities.Assets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.Options;

/// <summary>
///     Role group headers of the lobby roles tab (modded pages): same vanilla look as the game settings headers
///     (<see cref="GameOptionsHeaderPatches"/>). The groups have strong colors (blue crewmates, red impostors):
///     drawn with a white copy of the vanilla sprite, tinted like the MiraAPI one.
///     MiraAPI builds them again on each refresh, in a coroutine: the new ones are handled on the next frame.
///     The column labels under each header (# of roles, % chance) are put at fixed places by MiraAPI, fitting the English
///     texts: they are centered above the count and chance boxes, whatever the language.
/// </summary>
[HarmonyPatch(typeof(RolesSettingsMenu), nameof(RolesSettingsMenu.Update))]
public static class RoleSettingsHeaderPatches
{
    // MiraAPI places the role headers 0.05 more to the right than the game settings ones (0.55 instead of 0.5).
    private const float OffsetX = 0.05f;

    // Column labels of the quota header copied by MiraAPI under each role group header.
    private const string CountLabelName = "# Text";
    private const string ChanceLabelName = "Chance Text";

    private static Sprite? _whiteSprite;

    public static void Postfix(RolesSettingsMenu __instance)
    {
        var origin = GameSettingMenu.Instance ? GameSettingMenu.Instance.GameSettingsTab.categoryHeaderOrigin : null;
        if (origin == null || !origin.Background || !__instance.RoleChancesSettings) return;

        var miraSprite = MiraAssets.CategoryHeader.LoadAsset();
        foreach (var header in __instance.RoleChancesSettings.GetComponentsInChildren<CategoryHeaderMasked>())
        {
            if (!header.Background || header.Background.sprite != miraSprite || header.transform.Find(GameOptionsHeaderPatches.FillName))
                continue;

            // Unity check, not ??=: a destroyed sprite keeps its C# reference.
            if (!_whiteSprite)
                _whiteSprite = BorSpriteUtils.ToWhite(origin.Background.sprite);
            var color = header.Background.color;
            if (TryGetVanillaColors(color, out var background, out var text))
            {
                color = background;
                // The title and "(click to close / open)", not the quota labels below (# of role, % chance).
                foreach (var label in header.GetComponentsInChildren<TMPro.TextMeshPro>(true)
                             .Where(x => header.Title && x.transform.parent == header.Title.transform.parent))
                {
                    label.color = text;
                }
            }

            GameOptionsHeaderPatches.ApplyVanillaLook(header, origin.Background, _whiteSprite!, color, OffsetX);
        }

        CenterColumnLabels(__instance);
    }

    private static void CenterColumnLabels(RolesSettingsMenu menu)
    {
        var row = menu.RoleChancesSettings.GetComponentInChildren<RoleOptionSetting>();
        if (!row || !row.CountMinusBtn || !row.CountPlusBtn || !row.ChanceMinusBtn || !row.ChancePlusBtn) return;

        // Middle of the - and + buttons of each column.
        var countX = (row.CountMinusBtn.transform.position.x + row.CountPlusBtn.transform.position.x) / 2f;
        var chanceX = (row.ChanceMinusBtn.transform.position.x + row.ChancePlusBtn.transform.position.x) / 2f;
        var countLabel = MiraLocaleManager.Get("BetterOtherRoles.Options.RoleCountLabel", "# of Roles");

        foreach (var label in menu.RoleChancesSettings.GetComponentsInChildren<TMPro.TextMeshPro>())
        {
            float x;
            if (label.name == CountLabelName)
            {
                x = countX;

                // Shorter than the vanilla "Number of roles": the translator would write it back.
                var translator = label.GetComponent<TextTranslatorTMP>();
                if (translator)
                    Object.Destroy(translator);

                if (label.text != countLabel)
                    label.text = countLabel;
            }
            else if (label.name == ChanceLabelName)
            {
                x = chanceX;
            }
            else
            {
                continue;
            }

            label.horizontalAlignment = TMPro.HorizontalAlignmentOptions.Center;
            label.rectTransform.pivot = new Vector2(0.5f, label.rectTransform.pivot.y);
            var position = label.transform.position;
            if (!Mathf.Approximately(position.x, x))
                label.transform.position = new Vector3(x, position.y, position.z);
        }
    }

    /// <summary>
    ///     The MiraAPI crewmate and impostor groups use the player colors (cyan, bright red): the vanilla roles tab
    ///     colors instead (its "Crewmate Roles" header).
    /// </summary>
    private static bool TryGetVanillaColors(Color color, out Color background, out Color text)
    {
        if (color == Palette.CrewmateBlue)
        {
            background = Palette.CrewmateRoleHeaderBlue;
            text = Palette.CrewmateRoleHeaderTextBlue;
            return true;
        }

        if (color == Palette.ImpostorRed)
        {
            background = Palette.ImpostorRoleHeaderRed;
            text = Palette.ImpostorRoleHeaderTextRed;
            return true;
        }

        background = color;
        text = Color.white;
        return false;
    }
}
