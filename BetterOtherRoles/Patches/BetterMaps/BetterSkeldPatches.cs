using System.Collections;
using BetterOtherRoles.Options.Maps;
using HarmonyLib;
using MiraAPI.GameOptions;
using Reactor.Utilities;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.BetterMaps;

/// <summary>
///     Better Skeld (BetterOtherRoles 1.7.1 Modules/BetterSkeld.cs), with <see cref="BetterSkeldOptions"/>: vents, admin
///     table and vitals changed when the map starts, the same on every client (the options are shared). The Skeld only,
///     not its mirror (dlekS).
///     The vitals are a copy of those of Polus, whose prefab stays loaded until the map is destroyed (the copy uses its
///     minigame).
/// </summary>
[HarmonyPatch]
public static class BetterSkeldPatches
{
    // Place of the vitals (BOR), on the desk against the left wall of Communications, turned upright.
    private static readonly Vector3 VitalsPosition = new(1.9162f, -16.1985f, -2.4142f);
    private static readonly Vector3 VitalsScale = new(0.6636f, 0.7418f, 1f);
    private const float VitalsAngle = 90f;

    private static AsyncOperationHandle<GameObject>? _polus;

    /// <summary>
    ///     Changes The Skeld once all its objects are awake.
    /// </summary>
    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
    [HarmonyPostfix]
    public static void ShipStartPostfix(ShipStatus __instance)
    {
        if (__instance.Type != ShipStatus.MapType.Ship || IsDleks() || !BetterSkeldOptions.IsOn()) return;

        var options = OptionGroupSingleton<BetterSkeldOptions>.Instance;
        if (options.VentNetwork.Value) TryApply("vents", () => LinkVents(__instance));
        if (options.DisableAdminTable.Value) TryApply("admin table", () => DisableAdmin(__instance));
        if (options.Vitals.Value) Coroutines.Start(CoAddVitals(__instance));
        TryApply("theme", () => SetTheme(__instance, (SkeldTheme)options.Theme.Value));
    }

    /// <summary>
    ///     The map destroyed (end of the game): the prefab of Polus is released.
    /// </summary>
    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.OnDestroy))]
    [HarmonyPostfix]
    public static void ShipDestroyPostfix()
    {
        if (_polus is not { } handle) return;

        _polus = null;
        Addressables.Release(handle);
    }

    // One change failing (object renamed by an update of the game) does not stop the others.
    private static void TryApply(string name, Action change)
    {
        try
        {
            change();
        }
        catch (Exception e)
        {
            Error($"Better Skeld: could not change the {name}: {e}");
        }
    }

    private static void LinkVents(ShipStatus ship)
    {
        var admin = FindVent(ship, "AdminVent");
        var cafeteria = FindVent(ship, "CafeVent");
        var navNorth = FindVent(ship, "NavVentNorth");
        var navSouth = FindVent(ship, "NavVentSouth");
        var weapons = FindVent(ship, "WeaponsVent");
        var shields = FindVent(ship, "ShieldsVent");
        var bigY = FindVent(ship, "BigYVent");
        var electrical = FindVent(ship, "ElecVent");
        var upperReactor = FindVent(ship, "UpperReactorVent");
        var lowerReactor = FindVent(ship, "ReactorVent");
        var upperEngine = FindVent(ship, "LEngineVent");
        var lowerEngine = FindVent(ship, "REngineVent");
        var security = FindVent(ship, "SecurityVent");
        var medBay = FindVent(ship, "MedVent");

        weapons.Center = cafeteria;
        cafeteria.Center = weapons;
        navNorth.Right = navSouth;
        navSouth.Right = navNorth;
        navNorth.Center = bigY;
        navSouth.Center = bigY;
        bigY.Center = navNorth;
        weapons.Left = bigY;
        admin.Center = shields;
        shields.Center = admin;

        upperEngine.Center = medBay;
        medBay.Center = upperEngine;
        upperReactor.Center = security;
        security.Center = upperReactor;
        upperReactor.Left = lowerReactor;
        lowerReactor.Left = upperReactor;
        lowerReactor.Center = security;
        electrical.Center = lowerEngine;
        lowerEngine.Center = electrical;
    }

    // The table can no longer be used, and the map on it stops moving.
    private static void DisableAdmin(ShipStatus ship)
    {
        FindObject(ship, "MapRoomConsole").GetComponent<Collider2D>().enabled = false;
        FindObject(ship, "MapAnimation").SetActive(false);
    }

    // The decorations of the dates (MapDecor turns on the one of today in its Awake): the one chosen alone, or none.
    private static void SetTheme(ShipStatus ship, SkeldTheme theme)
    {
        if (theme == SkeldTheme.Auto) return;

        var shown = theme switch
        {
            SkeldTheme.Birthday => "Birthday",
            SkeldTheme.Halloween => "Halloween",
            SkeldTheme.TvShow => "Parasite",
            _ => null,
        };
        foreach (var decor in ship.GetComponentInChildren<MapDecor>(true).AllDecor)
        {
            if (decor)
                decor.gameObject.SetActive(shown != null && decor.name.Contains(shown));
        }
    }

    // Copy of the vitals of Polus, child of the map: destroyed with it after the game.
    private static IEnumerator CoAddVitals(ShipStatus ship)
    {
        var handle = Addressables.LoadAssetAsync<GameObject>(AmongUsClient.Instance.ShipPrefabs[(int)ShipStatus.MapType.Pb].RuntimeKey);
        _polus = handle;
        while (!handle.IsDone) yield return null;

        if (!ship || ShipStatus.Instance != ship)
        {
            ShipDestroyPostfix();
            yield break;
        }

        TryApply("vitals", () =>
        {
            var polus = handle.Result.GetComponent<ShipStatus>();
            var source = polus.GetComponentsInChildren<SystemConsole>(true).First(console => console.name == "panel_vitals");
            var vitals = Object.Instantiate(source.gameObject, ship.transform).transform;
            vitals.name = "panel_vitals";
            vitals.position = VitalsPosition;
            vitals.rotation = Quaternion.Euler(0f, 0f, VitalsAngle);
            vitals.localScale = VitalsScale;
        });
    }

    // The mirrored Skeld has the same type: told apart by the map chosen.
    private static bool IsDleks() => GameOptionsManager.Instance.CurrentGameOptions.MapId == (byte)MapNames.Dleks;

    private static Vent FindVent(ShipStatus ship, string name) => ship.AllVents.First(vent => vent.name == name);

    // Inactive objects included.
    private static GameObject FindObject(ShipStatus ship, string name) =>
        ship.GetComponentsInChildren<Transform>(true).First(child => child.name == name).gameObject;
}
