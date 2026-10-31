using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Configuration;
using HarmonyLib;
using Reactor.Networking.Attributes;

namespace BetterOtherRoles.Modules.Admin;

/// <summary>
///     BetterOtherRoles admins, recognized by a key in the plugin config ([Special Key] Admin), as the Dingus edition
///     of BetterOtherRoles 1.7.1 (DevConfig): only the SHA-256 of the key is in the code.
///     The host tells the other players whether it is an admin (<see cref="IsHostAdmin"/>).
/// </summary>
[HarmonyPatch]
public static class BorAdmin
{
    private const string AdminKeyHash = "98bfb29e0a87f32427f5e0b50e41cd699c4375dcf3201d82203cd9ec63dfceab";

    // Received from the host, false until then.
    private static bool _remoteHostAdmin;

    /// <summary>
    ///     Gets whether the local player is a BetterOtherRoles admin (admin logs: role assignment...).
    /// </summary>
    public static bool IsAdmin { get; private set; }

    /// <summary>
    ///     Gets whether the host of the current lobby is a BetterOtherRoles admin (Debug Mode).
    /// </summary>
    public static bool IsHostAdmin => AmongUsClient.Instance && AmongUsClient.Instance.AmHost ? IsAdmin : _remoteHostAdmin;

    /// <summary>
    ///     Reads the admin key of the config.
    /// </summary>
    public static void Initialize(ConfigFile config)
    {
        var key = config.Bind("Special Key", "Admin", string.Empty, "Key of the BetterOtherRoles admins.");
        IsAdmin = !string.IsNullOrEmpty(key.Value) && Hash(key.Value) == AdminKeyHash;
        if (IsAdmin)
        {
            Info("BetterOtherRoles admin key recognized.");
        }
    }

    /// <summary>
    ///     Tells everyone whether the host is an admin.
    /// </summary>
    [MethodRpc((uint)BorRpc.HostAdmin)]
    public static void RpcSetHostAdmin(PlayerControl host, bool isAdmin)
    {
        if (host && host.OwnerId == AmongUsClient.Instance.HostId)
        {
            _remoteHostAdmin = isAdmin;
        }
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
    [HarmonyPostfix]
    public static void OnGameJoinedPostfix()
    {
        _remoteHostAdmin = false;
    }

    /// <summary>
    ///     Sent by the host each time a player appears (lobby start, join, back from a game): the new player has the host's
    ///     PlayerControl by then.
    /// </summary>
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Start))]
    [HarmonyPostfix]
    public static void PlayerStartPostfix(PlayerControl __instance)
    {
        if (!AmongUsClient.Instance.AmHost || !LobbyBehaviour.Instance)
        {
            return;
        }

        var host = __instance.AmOwner ? __instance : PlayerControl.LocalPlayer;
        if (host)
        {
            RpcSetHostAdmin(host, IsAdmin);
        }
    }

    private static string Hash(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
