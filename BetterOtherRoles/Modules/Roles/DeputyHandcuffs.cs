using BetterOtherRoles.Assets;
using BetterOtherRoles.Options.Roles.Crewmate;
using HarmonyLib;
using MiraAPI.GameOptions;
using Reactor.Networking.Attributes;

namespace BetterOtherRoles.Modules.Roles;

/// <summary>
///     Deputy handcuffs, as in BetterOtherRoles 1.7.1: a handcuff does nothing when it is put on a player.
///     It triggers the next time this player tries to use a button, and blocks their buttons for the handcuff duration.
/// </summary>
[HarmonyPatch]
public static class DeputyHandcuffs
{
    /// <summary>
    ///     Gets the players wearing a handcuff that has not triggered yet (BOR: Deputy.handcuffedPlayers).
    /// </summary>
    public static HashSet<byte> Pending { get; } = [];

    /// <summary>
    ///     Gets the players whose handcuff has triggered, with the time left (BOR: Deputy.handcuffedKnows).
    /// </summary>
    public static Dictionary<byte, float> Active { get; } = [];

    public static float Duration => OptionGroupSingleton<SheriffOptions>.Instance.DeputyHandcuffDuration.Value;

    public static bool IsLocalActive => PlayerControl.LocalPlayer && Active.ContainsKey(PlayerControl.LocalPlayer.PlayerId);

    /// <summary>
    ///     Called when the local player tries a valid action with a button (ability, kill, report).
    ///     Triggers their pending handcuff, if any.
    /// </summary>
    /// <returns><see langword="true"/> if the action must be cancelled (handcuff triggered now, or already active).</returns>
    public static bool TryBlockLocalAction()
    {
        if (IsLocalActive) return true;

        if (!PlayerControl.LocalPlayer || !Pending.Contains(PlayerControl.LocalPlayer.PlayerId)) return false;

        RpcTriggerHandcuff(PlayerControl.LocalPlayer);
        return true;
    }

    /// <summary>
    ///     Puts a handcuff on the target, for everyone.
    /// </summary>
    [MethodRpc((uint)BorRpc.DeputyHandcuff)]
    public static void RpcHandcuff(PlayerControl deputy, PlayerControl target)
    {
        Pending.Add(target.PlayerId);
        Info($"[Deputy] {deputy.Data.PlayerName} handcuffed {target.Data.PlayerName}");
    }

    /// <summary>
    ///     The handcuff of the player triggers: their buttons are blocked for <see cref="Duration"/>, for everyone.
    /// </summary>
    [MethodRpc((uint)BorRpc.DeputyHandcuffTriggered)]
    public static void RpcTriggerHandcuff(PlayerControl player)
    {
        Pending.Remove(player.PlayerId);
        Active[player.PlayerId] = Duration;
        Info($"[Deputy] Handcuff of {player.Data.PlayerName} triggered ({Duration} s)");

        if (player.AmOwner)
            BorSounds.Play("deputyHandcuff");
    }

    /// <summary>
    ///     Counts down the active handcuffs on every client (BOR: HudManagerUpdatePatch).
    ///     The countdown is frozen while the handcuffed player is in a vent (synced by the game on every client).
    /// </summary>
    public static void Update(float deltaTime)
    {
        foreach (var playerId in Active.Keys.ToArray())
        {
            var player = GameData.Instance ? GameData.Instance.GetPlayerById(playerId)?.Object : null;
            if (player && player!.inVent) continue;

            Active[playerId] -= deltaTime;
            if (Active[playerId] <= 0f)
            {
                Active.Remove(playerId);
                Info($"[Deputy] Handcuff of player {playerId} is over");
            }
        }
    }

    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.OnDestroy))]
    [HarmonyPostfix]
    public static void IntroCutsceneOnDestroyPostfix()
    {
        Pending.Clear();
        Active.Clear();
    }
}
