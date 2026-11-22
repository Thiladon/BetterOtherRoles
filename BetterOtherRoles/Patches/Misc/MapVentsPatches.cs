using BetterOtherRoles.Assets;
using BetterOtherRoles.LocalSettings;
using BetterOtherRoles.Modules.Vents;
using BetterOtherRoles.Utilities;
using HarmonyLib;
using MiraAPI.LocalSettings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Vents on the map (BetterOtherRoles 1.7.1 MapBehaviourPatch and TOU-Mira 1.7.9 ShowVentsPatch,
///     <see cref="BorLocalSettingsTab.ShowVentsOnMap"/>): one icon per vent, in the color of its network (the vents
///     leading to each other, also through the exits added by the mod: Better Skeld, Better Polus, vent networks), on the
///     normal map, the sabotage map and the admin map. The vent to clean (Vent Cleaning task not done) is smaller.
///     Placed again at each opening of the map.
/// </summary>
[HarmonyPatch]
public static class MapVentsPatches
{
    // In front of the map, behind the player icon (TOU-Mira 1.7.9).
    private const float IconDepth = -0.99f;

    // Sabotage map: in front of the buttons by this much (the clicks still go to them, the icons have no collider).
    private const float FrontMargin = 0.05f;

    // Size of the vent to clean (BetterOtherRoles 1.7.1).
    private const float CleaningScale = 0.6f;

    // Icons of the map shown now, by vent id (children of the map, destroyed with it).
    private static readonly Dictionary<int, SpriteRenderer> Icons = [];
    private static Sprite? _sprite;

    [HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.ShowNormalMap))]
    [HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.ShowSabotageMap))]
    [HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.ShowCountOverlay))]
    [HarmonyPostfix]
    public static void ShowPostfix(MapBehaviour __instance)
    {
        var settings = LocalSettingsTabSingleton<BorLocalSettingsTab>.Instance;
        var shown = settings != null && settings.ShowVentsOnMap.Value;
        if (!shown || !ShipStatus.Instance || !__instance.HerePoint)
        {
            SetAllShown(false);
            return;
        }

        var vents = ShipStatus.Instance.AllVents.Where(vent => vent && vent.gameObject.activeInHierarchy).ToList();
        var networks = Networks(vents);
        var cleaningId = VentToClean();
        var colors = Palette.PlayerColors;
        var frontZ = SabotageFrontZ(__instance);
        SetAllShown(false);

        for (var i = 0; i < networks.Count; i++)
        {
            var color = colors[i % colors.Length];
            foreach (var vent in networks[i])
            {
                var icon = Icon(__instance, vent);
                var position = vent.transform.position / ShipStatus.Instance.MapScale;
                icon.transform.localPosition = new Vector3(position.x, position.y, IconDepth);
                if (frontZ.HasValue)
                    icon.transform.position = new Vector3(icon.transform.position.x, icon.transform.position.y, frontZ.Value);

                icon.transform.localScale = Vector3.one * (vent.Id == cleaningId ? CleaningScale : 1f);
                icon.color = color;
                icon.gameObject.SetActive(true);
            }
        }
    }

    // Groups of vents leading to each other (both ways), in the order of their first vent id: same colors each game.
    private static List<List<Vent>> Networks(List<Vent> vents)
    {
        var byId = vents.ToDictionary(vent => vent.Id);
        var links = vents.ToDictionary(vent => vent.Id, _ => new HashSet<int>());
        foreach (var vent in vents)
        {
            foreach (var exit in Exits(vent))
            {
                if (!byId.ContainsKey(exit.Id)) continue;

                links[vent.Id].Add(exit.Id);
                links[exit.Id].Add(vent.Id);
            }
        }

        var networks = new List<List<Vent>>();
        var seen = new HashSet<int>();
        foreach (var vent in vents.OrderBy(vent => vent.Id))
        {
            if (!seen.Add(vent.Id)) continue;

            var network = new List<Vent>();
            var toVisit = new Queue<int>([vent.Id]);
            while (toVisit.Count > 0)
            {
                var id = toVisit.Dequeue();
                network.Add(byId[id]);
                foreach (var next in links[id].Where(seen.Add))
                {
                    toVisit.Enqueue(next);
                }
            }

            networks.Add(network);
        }

        return networks;
    }

    private static IEnumerable<Vent> Exits(Vent vent)
    {
        if (vent.Left) yield return vent.Left;
        if (vent.Center) yield return vent.Center;
        if (vent.Right) yield return vent.Right;

        foreach (var exit in BorVentLinks.AddedExits(vent))
        {
            if (exit) yield return exit;
        }
    }

    // Vent of the Vent Cleaning task of the local player, not done yet; -1 for none.
    private static int VentToClean()
    {
        var player = PlayerControl.LocalPlayer;
        if (!player) return -1;

        var task = player.myTasks.ToArray().FirstOrDefault(task => task && task.TaskType == TaskTypes.VentCleaning);
        if (!task || task!.IsComplete) return -1;

        var consoles = task.FindConsoles();
        return consoles.Count > 0 ? consoles[0].ConsoleId : -1;
    }

    // Sabotage map shown: depth in front of all its buttons (world z), else null (normal depth).
    private static float? SabotageFrontZ(MapBehaviour map)
    {
        var overlay = map.infectedOverlay;
        if (!overlay || !overlay.gameObject.activeSelf) return null;

        var renderers = overlay.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers.Length == 0) return null;

        return renderers.Min(renderer => renderer.transform.position.z) - FrontMargin;
    }

    // Copy of the player icon of the map (same parent, layer and material), with the vent sprite, without collider:
    // the clicks go through to the sabotage buttons under it.
    private static SpriteRenderer Icon(MapBehaviour map, Vent vent)
    {
        if (Icons.TryGetValue(vent.Id, out var icon) && icon) return icon;

        var herePoint = map.HerePoint;
        icon = Object.Instantiate(herePoint, herePoint.transform.parent);
        icon.name = $"BorMapVent{vent.Id}";
        icon.sprite = VentSprite();
        foreach (var collider in icon.GetComponentsInChildren<Collider2D>(true))
        {
            Object.Destroy(collider);
        }

        Icons[vent.Id] = icon;
        return icon;
    }

    private static void SetAllShown(bool shown)
    {
        foreach (var icon in Icons.Values)
        {
            if (icon) icon.gameObject.SetActive(shown);
        }
    }

    private static Sprite VentSprite()
    {
        if (_sprite) return _sprite!;

        _sprite = BorAssets.MapVent.LoadAsset();
        BorSpriteUtils.KeepLoaded(_sprite);
        return _sprite;
    }
}
