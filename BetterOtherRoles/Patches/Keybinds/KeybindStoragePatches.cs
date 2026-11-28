using AmongUs.Data.Settings;
using HarmonyLib;

namespace BetterOtherRoles.Patches.Keybinds;

/// <summary>
///     Keys of the mod saved apart from those of the game and of the other mods. Rewired saves the controller maps in
///     the settings of the game, shared by every install, with the ids of the actions; the actions added by a mod get
///     the next free ids, so the same id is another action in another mod (BetterOtherRoles 1.7.0): a map saved by one
///     did not fit the other, which went back to the default keys. The base of the keys of the maps, of their known
///     actions and of the input behaviors gets the id of the mod.
/// </summary>
[HarmonyPatch(typeof(RewiredUserDataStore_PlayerData), "GetBasePlayerPrefsKey")]
public static class KeybindStoragePatches
{
    private const string Suffix = "|mod=" + BetterOtherRolesPlugin.Id;

    [HarmonyPostfix]
    public static void Postfix(ref string __result)
    {
        __result += Suffix;
    }
}
