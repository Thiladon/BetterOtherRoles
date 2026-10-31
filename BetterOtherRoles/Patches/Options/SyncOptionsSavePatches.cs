using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using MiraAPI.GameOptions;

namespace BetterOtherRoles.Patches.Options;

/// <summary>
///     When a client receives the host options or role settings, MiraAPI saves its plugin configs
///     (ModdedOptionsManager.HandleSyncOptions, CustomRoleManager.HandleSyncRoleOptions): the whole file is written
///     from the values in memory. With a shared config (local test instances), this erased the host settings.
///     The clients do not save there.
/// </summary>
[HarmonyPatch]
public static class SyncOptionsSavePatches
{
    internal static bool HandlingSync { get; private set; }

    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(ModdedOptionsManager), "HandleSyncOptions");

        // Through its RPC: patching CustomRoleManager at load time runs its static constructor too early (MiraAPI crash).
        yield return AccessTools.Method(typeof(ModdedOptionsManager).Assembly.GetType("MiraAPI.Networking.SyncRoleOptionsRpc"), "Handle");
    }

    public static void Prefix()
    {
        HandlingSync = true;
    }

    public static void Finalizer()
    {
        HandlingSync = false;
    }
}

/// <summary>
///     Skips the save of a client during the sync (<see cref="SyncOptionsSavePatches"/>).
/// </summary>
[HarmonyPatch(typeof(ConfigFile), nameof(ConfigFile.Save))]
public static class ConfigSavePatch
{
    public static bool Prefix()
    {
        return !SyncOptionsSavePatches.HandlingSync || (AmongUsClient.Instance && AmongUsClient.Instance.AmHost);
    }
}
