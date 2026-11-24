using BetterOtherRoles.LocalSettings;
using HarmonyLib;
using MiraAPI.LocalSettings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     The dead see all the players on the map (BetterOtherRoles 1.7.1 MapBehaviourPatch,
///     <see cref="BorLocalSettingsTab.GhostsSeePlayersOnMap"/>): one icon per other player, in their colors, moved as they
///     move; the dead players faded. Not for the dead impostors, who can still sabotage. Hidden during the meetings (the
///     players are at the table) and on the map of the admin table (counters only).
/// </summary>
[HarmonyPatch(typeof(MapBehaviour))]
public static class MapGhostPatches
{
    // Just behind the icon of the local player (-1), in front of the vents (-0.99).
    private const float IconDepth = -0.995f;

    // Opacity of the dead players (BetterOtherRoles 1.7.1).
    private const float DeadAlpha = 0.25f;

    // Icons of the other players, by player id (children of the map, destroyed with it).
    private static readonly Dictionary<byte, SpriteRenderer> Icons = [];

    [HarmonyPatch("FixedUpdate")]
    [HarmonyPostfix]
    public static void FixedUpdatePostfix(MapBehaviour __instance)
    {
        if (!CanSee() || !ShipStatus.Instance || !__instance.HerePoint || IsAdminMap(__instance))
        {
            SetAllShown(false);
            return;
        }

        // x as is, also on dlekS: its map is mirrored by hand (DleksMapPatches), as the icon of the local player.
        var mapScale = ShipStatus.Instance.MapScale;
        var shown = new HashSet<byte>();
        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (!player || player.AmOwner || player.Data == null || player.Data.Disconnected) continue;

            var icon = Icon(__instance, player.PlayerId);
            var position = player.transform.position / mapScale;
            icon.transform.localPosition = new Vector3(position.x, position.y, IconDepth);
            player.SetPlayerMaterialColors(icon);
            var color = icon.color;
            color.a = player.Data.IsDead ? DeadAlpha : 1f;
            icon.color = color;
            icon.enabled = true;
            icon.gameObject.SetActive(true);
            shown.Add(player.PlayerId);
        }

        foreach (var (id, icon) in Icons)
        {
            if (icon && !shown.Contains(id)) icon.gameObject.SetActive(false);
        }
    }

    // Dead local player, not impostor, out of the meetings, setting on.
    private static bool CanSee()
    {
        var settings = LocalSettingsTabSingleton<BorLocalSettingsTab>.Instance;
        if (settings == null || !settings.GhostsSeePlayersOnMap.Value || MeetingHud.Instance) return false;

        var local = PlayerControl.LocalPlayer;
        return local && local.Data != null && local.Data.IsDead && local.Data.Role && !local.Data.Role.IsImpostor;
    }

    // Copy of the player icon of the map (same parent, layer and material), without collider.
    private static SpriteRenderer Icon(MapBehaviour map, byte playerId)
    {
        if (Icons.TryGetValue(playerId, out var icon) && icon) return icon;

        var herePoint = map.HerePoint;
        icon = Object.Instantiate(herePoint, herePoint.transform.parent);
        icon.name = $"BorMapPlayer{playerId}";
        foreach (var collider in icon.GetComponentsInChildren<Collider2D>(true))
        {
            Object.Destroy(collider);
        }

        Icons[playerId] = icon;
        return icon;
    }

    // Map of the admin table: only its counters, as for the living.
    private static bool IsAdminMap(MapBehaviour map) => map.countOverlay && map.countOverlay.isActiveAndEnabled;

    private static void SetAllShown(bool shown)
    {
        foreach (var icon in Icons.Values)
        {
            if (icon) icon.gameObject.SetActive(shown);
        }
    }
}
