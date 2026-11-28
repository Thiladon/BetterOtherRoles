using System.Collections;
using BetterOtherRoles.LocalSettings;
using CorsacCosmetics;
using CorsacCosmetics.Cosmetics;
using MiraAPI.LocalSettings;

namespace BetterOtherRoles.Modules.Cosmetics;

/// <summary>
///     Hats of BetterOtherRoles 1.7.1 (TheOtherHats), as bundles of CorsacCosmetics (TOU-Mira 1.7.9 InitCorsac): the
///     bundles missing from CorsacCosmetics/Bundles are downloaded from the BOR-Cosmetics repository when the game
///     starts, before CorsacCosmetics loads the cosmetics, unless turned off in the local settings
///     (<see cref="BorLocalSettingsTab.AutoDownloadCosmetics"/>). A bundle already there is not downloaded again.
/// </summary>
public static class BorCosmetics
{
    // Fixed address of the public repository, as TOU-Mira does for its own.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S1075:URIs should not be hardcoded", Justification = "Public repository of the bundles.")]
    private const string RepositoryUrl ="https://github.com/Thiladon/BOR-Cosmetics/raw/refs/heads/main/Bundles/";

    private static readonly string[] Bundles = ["BOR.Community.ccb", "BOR.Developer.ccb", "BOR.Eterzx.ccb"];

    /// <summary>
    ///     Queues the download, run by CorsacCosmetics when the game starts (the local settings are there by then).
    /// </summary>
    public static void Initialize()
    {
        PluginCompat.QueueDiscoveryCoroutine(CoDownload());
    }

    private static IEnumerator CoDownload()
    {
        if (!AutoDownload()) yield break;

        Directory.CreateDirectory(CosmeticPaths.BundlePath);
        foreach (var bundle in Bundles)
        {
            var path = Path.Combine(CosmeticPaths.BundlePath, bundle);
            if (File.Exists(path)) continue;

            var task = PluginCompat.DownloadBundle(RepositoryUrl + bundle, path);
            while (!task.IsCompleted)
                yield return null;
        }
    }

    // The local settings may not be there yet: download by default.
    private static bool AutoDownload()
    {
        try
        {
            return LocalSettingsTabSingleton<BorLocalSettingsTab>.Instance.AutoDownloadCosmetics.Value;
        }
        catch (Exception e)
        {
            Warning($"[Cosmetics] Local settings not ready, download by default: {e.Message}");
            return true;
        }
    }
}
