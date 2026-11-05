using HarmonyLib;
using MiraAPI.Translation;

namespace BetterOtherRoles.Modules.Lobby;

/// <summary>
///     Kick of the players whose name holds rich text tags (TOU-Mira 1.7.9 AntiRichTextNamePatch): the game never lets
///     a player type &lt; or &gt; in a name (TextBoxTMP.IsCharAllowed), so such a name comes from a modified client or
///     profile. Checked by the host in PlayerControl.SetName, reached both by the normal way (CheckName, then RpcSetName)
///     and by a SetName sent directly by a modified client. The kicked player sees the reason in its language
///     (<see cref="BorKick"/>).
/// </summary>
[HarmonyPatch]
public static class BorNameCheck
{
    // Clients already kicked, so that the next names they send do not kick them again.
    private static readonly HashSet<int> Kicked = [];

    /// <summary>
    ///     Gets a value indicating whether a name can be used: no &lt; nor &gt; (rich text tags).
    ///     Also for the names given by the mod (/setname).
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>True when the name has no tag character.</returns>
    public static bool IsAllowed(string name) => name.IndexOfAny(['<', '>']) < 0;

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
    [HarmonyPostfix]
    public static void OnGameJoinedPostfix()
    {
        Kicked.Clear();
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetName))]
    [HarmonyPrefix]
    public static void SetNamePrefix(PlayerControl __instance, [HarmonyArgument(0)] string playerName)
    {
        if (!AmongUsClient.Instance || !AmongUsClient.Instance.AmHost || __instance.AmOwner || string.IsNullOrEmpty(playerName) ||
            IsAllowed(playerName) || !Kicked.Add(__instance.OwnerId))
            return;

        // <noparse>: the tags are shown in the host's chat, not applied.
        var shown = $"<noparse>{playerName.Replace("</noparse>", string.Empty)}</noparse>";
        BorKick.Kick(__instance, "BetterOtherRoles.Lobby.Name.KickReason",
            MiraLocaleManager.Get("BetterOtherRoles.Lobby.Name.Kicked", "{0} was kicked: tags in the name.").Replace("{0}", shown));
    }
}
