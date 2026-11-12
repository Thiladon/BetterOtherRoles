using System.Reflection;
using HarmonyLib;
using MiraAPI.Hud;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     The top right buttons stay usable in the haunt menu of a dead player (map, role guide and its group: Debug,
///     zoom), the menu staying open and following its target:
///     - the game hides the map button when the menu opens (HauntMenuMinigame.Begin), MiraAPI the guide button while
///       any minigame is open (MiraHudHelper.FixedUpdate): shown again;
///     - the map only opens when the player can move, never during a minigame (MapBehaviour.Show, ShowNormalMap):
///       opened over the haunt menu;
///     - the guide closes the open minigame (MiraAPI RoleGuidePatches.Open): the haunt menu is kept.
///     The real tasks keep the behaviour of the game.
/// </summary>
[HarmonyPatch]
public static class HauntMenuButtonsPatches
{
    private static readonly Color MapColor = new(0.05f, 0.2f, 1f, 1f);

    // While the guide opens: the haunt menu is not closed.
    private static bool _openingGuide;

    /// <summary>
    ///     Gets whether the haunt menu is open.
    /// </summary>
    public static bool IsHauntMenuOpen => Minigame.Instance && Minigame.Instance.TryCast<HauntMenuMinigame>();

    /// <summary>
    ///     The game puts the haunt menu under the ability button (CrewmateGhostRole.UseAbility), on its left. Moved under
    ///     the HUD, centered at the same height: the map hides the ability button (SetHudActive), which would stop the
    ///     menu and its target following. The opening animation goes on to the new place (Minigame.TargetPosition).
    /// </summary>
    [HarmonyPatch(typeof(HauntMenuMinigame), nameof(HauntMenuMinigame.Begin))]
    [HarmonyPostfix]
    public static void BeginPostfix(HauntMenuMinigame __instance)
    {
        if (!HudManager.InstanceExists) return;

        var hud = HudManager.Instance;
        hud.ToggleMapButton(true);

        var menu = __instance.transform;
        var parent = menu.parent;
        if (!parent || parent == hud.transform) return;

        var target = hud.transform.InverseTransformPoint(parent.TransformPoint(__instance.TargetPosition));
        menu.SetParent(hud.transform, true);
        __instance.TargetPosition = new Vector2(0f, target.y);
        var position = menu.localPosition;
        menu.localPosition = new Vector3(0f, position.y, position.z);
    }

    // The normal map of the game (MapBehaviour.ShowNormalMap), without the check that the player can move.
    [HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.Show))]
    [HarmonyPrefix]
    public static bool MapShowPrefix(MapBehaviour __instance, MapOptions opts)
    {
        if (!IsHauntMenuOpen || opts.Mode != MapOptions.Modes.Normal) return true;

        if (__instance.IsOpen)
        {
            __instance.Close();
            return false;
        }

        var player = PlayerControl.LocalPlayer;
        player.SetPlayerMaterialColors(__instance.HerePoint);
        if (player.isTrackingPlayer && player.trackedPlayer)
        {
            var disconnected = player.trackedPlayer.Data.Disconnected;
            __instance.TrackedHerePoint.gameObject.SetActive(!disconnected);
            if (!disconnected)
            {
                __instance.SetTrackedHerePointColor(player.trackedPlayerColorID);
                __instance.UpdateTrackedPosition();
            }
        }

        __instance.GenericShow();
        __instance.taskOverlay.Show();
        __instance.ColorControl.SetColor(MapColor);
        HudManager.Instance.SetHudActive(false);
        return false;
    }

    [HarmonyPatch(typeof(MatchInfoGuide), nameof(MatchInfoGuide.Open))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First + 100)]
    public static void GuideOpenPrefix() => _openingGuide = IsHauntMenuOpen;

    [HarmonyPatch(typeof(MatchInfoGuide), nameof(MatchInfoGuide.Open))]
    [HarmonyFinalizer]
    public static void GuideOpenFinalizer() => _openingGuide = false;

    [HarmonyPatch(typeof(HauntMenuMinigame), nameof(HauntMenuMinigame.Close))]
    [HarmonyPrefix]
    public static bool HauntClosePrefix() => !_openingGuide;

    /// <summary>
    ///     MiraAPI hides the guide button (and its group: Debug, zoom) while a minigame is open
    ///     (<c>SetActive(!GameSettingMenu.Instance &amp;&amp; !Minigame.Instance)</c>, the only use of
    ///     Minigame.Instance in this method). Showing it again right after made it lose its clicks: a disabled button
    ///     is reset at each FixedUpdate. The haunt menu is hidden from MiraAPI during this call only. In its own class:
    ///     HarmonyX does not allow TargetMethod with other annotated patches in the same class.
    /// </summary>
    [HarmonyPatch]
    public static class GuideButtonPatch
    {
        private static Minigame? _hidden;

        public static MethodBase TargetMethod() => AccessTools.Method(typeof(MiraHudHelper), "FixedUpdate");

        public static void Prefix()
        {
            if (!IsHauntMenuOpen) return;

            _hidden = Minigame.Instance;
            Minigame.Instance = null;
        }

        public static void Finalizer()
        {
            if (_hidden == null) return;

            Minigame.Instance = _hidden;
            _hidden = null;
        }
    }
}
