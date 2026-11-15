using HarmonyLib;
using Reactor.Networking.Rpc;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Reactor 2.5 warns "Non-immediate RPCs were removed in 2025.5.20!" for each RPC sent without "immediately"
///     (UnsafeCustomRpc.UnsafeSend), then sends it at once anyway. Its own MethodRpc never passes it, nor MiraAPI:
///     every RPC is marked immediate before the check, for BetterOtherRoles, MiraAPI and the next ones. Nothing else
///     changes: they were already sent at once.
/// </summary>
[HarmonyPatch(typeof(UnsafeCustomRpc), nameof(UnsafeCustomRpc.UnsafeSend))]
public static class ReactorRpcPatches
{
    [HarmonyPrefix]
    public static void UnsafeSendPrefix(ref bool immediately) => immediately = true;
}
