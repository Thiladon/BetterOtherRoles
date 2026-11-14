using System.Reflection;
using BetterOtherRoles.Options;
using HarmonyLib;
using MiraAPI.GameOptions;
using MiraAPI.Utilities;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BetterOtherRoles.Patches.GameTweaks;

/// <summary>
///     MedBay scans (BetterOtherRoles 1.7.1 MedScanMinigameFixedUpdatePatch and MedScanMinigameWalkToPadPatches,
///     <see cref="GameTweaksOptions"/>): several players scanned at the same time, and a random place on the scanner.
/// </summary>
[HarmonyPatch]
public static class MedScanPatches
{
    // Objects drawing the platform of the scanner: Polus, The Skeld and Mira HQ, Submerged.
    private static readonly string[] PlatformNames = ["panel_medplatform", "MedScanner", "console_medscan"];

    // Where the player may stand: an ellipse around the place of the game, up to 25 % of the width of the platform
    // left or right and 20 % of its height up or down (BOR: a rectangle, never above, whose corners left the platform).
    private const float WidthPart = 0.25f;
    private const float HeightPart = 0.2f;

    private static Vector3 _offset;

    private static GameTweaksOptions Options => OptionGroupSingleton<GameTweaksOptions>.Instance;

    /// <summary>
    ///     Several scans at the same time: the scanner always takes the local player, without a waiting list.
    /// </summary>
    [HarmonyPatch(typeof(MedScanMinigame), nameof(MedScanMinigame.FixedUpdate))]
    [HarmonyPrefix]
    public static void FixedUpdatePrefix(MedScanMinigame __instance)
    {
        if (!Options.ParallelMedScans.Value) return;

        __instance.medscan.CurrentUser = PlayerControl.LocalPlayer.PlayerId;
        __instance.medscan.UsersList.Clear();
    }

    /// <summary>
    ///     Random place on the platform, chosen when the scan opens.
    /// </summary>
    [HarmonyPatch(typeof(MedScanMinigame), nameof(MedScanMinigame.Begin))]
    [HarmonyPostfix]
    public static void BeginPostfix()
    {
        _offset = Vector3.zero;
        if (!Options.RandomScanPosition.Value || !ShipStatus.Instance || !ShipStatus.Instance.MedScanner) return;

        var platform = FindPlatform();
        if (!platform) return;

        // Uniform in the ellipse: square root of the radius.
        var size = platform!.bounds.size;
        var angle = Random.Range(0f, 2f * Mathf.PI);
        var radius = Mathf.Sqrt(Random.value);
        _offset = new Vector3(Mathf.Cos(angle) * radius * size.x * WidthPart, Mathf.Sin(angle) * radius * size.y * HeightPart, 0f);
    }

    /// <summary>
    ///     Walk to the scanner: the coroutine reads the place of the scanner (MedScannerBehaviour.Position, its
    ///     position + Offset) in its first step. The offset of the scanner is moved during each step, then given back:
    ///     the place where the player waits its turn stays the one of the game. The getter itself cannot be patched:
    ///     one line, copied by IL2CPP into its callers.
    /// </summary>
    [HarmonyPatch]
    public static class WalkToPadPatch
    {
        public static MethodBase TargetMethod()
        {
            return Helpers.GetStateMachineMoveNext<MedScanMinigame>(nameof(MedScanMinigame.WalkToPad))!;
        }

        public static void Prefix(out Vector3? __state)
        {
            __state = null;
            var scanner = ShipStatus.Instance ? ShipStatus.Instance.MedScanner : null;
            if (!scanner || _offset == Vector3.zero) return;

            __state = scanner!.Offset;
            scanner.Offset += _offset;
        }

        public static void Postfix(Vector3? __state)
        {
            if (__state == null || !ShipStatus.Instance || !ShipStatus.Instance.MedScanner) return;

            ShipStatus.Instance.MedScanner.Offset = __state.Value;
        }
    }

    private static SpriteRenderer? FindPlatform()
    {
        var scanner = ShipStatus.Instance.MedScanner;
        foreach (var renderer in ShipStatus.Instance.GetComponentsInChildren<SpriteRenderer>())
        {
            if (PlatformNames.Contains(renderer.name)) return renderer;
        }

        return scanner.GetComponent<SpriteRenderer>();
    }
}
