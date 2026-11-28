using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Configuration;
using CorsacCosmetics.Cosmetics;

namespace BetterOtherRoles.Modules.Editions;

/// <summary>
///     Special editions of BetterOtherRoles 1.7.1 (DevConfig): unlocked by a password in the config of the mod
///     ([Special Edition Flags] Dingus and Eterzx), its SHA-256 compared to the one of BOR 1.7.1, so a key that worked
///     in BOR 1.7.0 works here too (the passwords are not in the code).
///     - Dingus: "Dingus special edition" under the version (main menu, in game), and the Undertale specimen of Polus
///       replaced (<see cref="Patches.Misc.DingusSpecimenPatches"/>);
///     - Eterzx: the Eterzx hats in the hats tab; without it, their page is left out, the hats staying loaded so that
///       a player of that edition is seen with them.
/// </summary>
public static class BorEditions
{
    /// <summary>
    ///     Text of the Dingus edition (BetterOtherRoles 1.7.1 CredentialsPatch.DingusRelease).
    /// </summary>
    public const string DingusText = "<color=#f779efff><b>Dingus special edition</b></color>";

    private const string DingusHash = "310fa3717f2b746dc22bb95470ae458e2a07322ee8e631007b670fb34baa8913";
    private const string EterzxHash = "e65b7301fa14b742b9bb94431b43911cf30b432a67e56d4ca0b2fbdc58d637d5";

    // Group of the Eterzx hats in their bundle (BOR-Cosmetics, BOR.Eterzx.ccb).
    private const string EterzxGroup = "BOR Eterzx";

    // CorsacCosmetics has no way to leave a group out: the list of its groups, private.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3011:Reflection should not be used to increase accessibility", Justification = "CorsacCosmetics has no way to leave a group out.")]
    private static readonly FieldInfo? GroupIds =typeof(Group).GetField("_groupIds", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    ///     Gets a value indicating whether the Dingus edition is unlocked.
    /// </summary>
    public static bool IsDingus { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether the Eterzx edition is unlocked.
    /// </summary>
    public static bool IsEterzx { get; private set; }

    /// <summary>
    ///     Reads the keys of the editions (as BetterOtherRoles 1.7.1: same section, names and default value).
    /// </summary>
    /// <param name="config">The config of the mod.</param>
    public static void Initialize(ConfigFile config)
    {
        var dingus = config.Bind("Special Edition Flags", "Dingus", "Unknown", "Password to unlock Dingus special edition");
        var eterzx = config.Bind("Special Edition Flags", "Eterzx", "Unknown", "Password to unlock Eterzx special hats");
        IsDingus = Hash(dingus.Value) == DingusHash;
        IsEterzx = Hash(eterzx.Value) == EterzxHash;
        if (IsDingus || IsEterzx)
            Info($"Special editions: Dingus {IsDingus}, Eterzx {IsEterzx}");
    }

    /// <summary>
    ///     Leaves the page of the Eterzx hats out of the hats tab without that edition (once CorsacCosmetics made its
    ///     groups; again each time: nothing when already done).
    /// </summary>
    public static void HideEterzxPage()
    {
        if (IsEterzx || CosmeticsCatalog.Instance == null) return;

        // The id of a group is a GUID made by CorsacCosmetics: the group found by its name.
        var groups = CosmeticsCatalog.Instance.HatGroups;
        if (GroupIds?.GetValue(groups) is not List<string> ids) return;

        for (var i = ids.Count - 1; i >= 0; i--)
        {
            if (groups.GetGroupNameByIndex(i) == EterzxGroup)
                ids.RemoveAt(i);
        }
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
