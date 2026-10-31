using BetterOtherRoles.Modules.Admin;
using BetterOtherRoles.Options;
using HarmonyLib;
using MiraAPI.GameOptions;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Debug Mode (<see cref="BorDevOptions.TestMode"/>): start with less than 4 players, and no game end.
/// </summary>
[HarmonyPatch]
public static class TestModePatches
{
    private static int _vanillaMinPlayers = -1;
    private static bool _forceEndGame;

    /// <summary>
    ///     Ends the game despite the test mode (/endGame command).
    /// </summary>
    public static void ForceEndGame(GameOverReason reason)
    {
        _forceEndGame = true;
        try
        {
            GameManager.Instance.RpcEndGame(reason, false);
        }
        finally
        {
            _forceEndGame = false;
        }
    }

    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Update))]
    [HarmonyPrefix]
    public static void GameStartManagerUpdatePrefix(GameStartManager __instance)
    {
        if (_vanillaMinPlayers < 0)
        {
            _vanillaMinPlayers = __instance.MinPlayers;
        }

        TurnOffWithoutAdminKey();

        var minPlayers = BorDevOptions.IsTestModeActive ? 1 : _vanillaMinPlayers;
        if (__instance.MinPlayers == minPlayers)
        {
            return;
        }

        // The start button is only refreshed when the player count changes: force it.
        __instance.MinPlayers = minPlayers;
        __instance.LastPlayerCount = -1;
    }

    // A host without the admin key cannot use the Debug Mode: the saved value goes back to false.
    // Once the local player exists: SetValue sends the new value through it.
    private static void TurnOffWithoutAdminKey()
    {
        var option = OptionGroupSingleton<BorDevOptions>.Instance.TestMode;
        if (AmongUsClient.Instance.AmHost && !BorAdmin.IsAdmin && option.Value && PlayerControl.LocalPlayer)
        {
            option.SetValue(false);
        }
    }

    /// <summary>
    ///     After an ejection, the game only gives the control back if the game is not lost by deaths (impostors as many
    ///     as the crewmates): it then waits for the game end, blocked here, and stays on a black screen.
    /// </summary>
    [HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.IsGameOverDueToDeath))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static bool IsGameOverDueToDeathPrefix(ref bool __result)
    {
        if (!BorDevOptions.IsTestModeActive)
        {
            return true;
        }

        __result = false;
        return false;
    }

    /// <summary>
    ///     Before opening a meeting, the host checks the task win: alone as an impostor, there is no crewmate task, so
    ///     the tasks count as done, the game end is blocked and the meeting never opens ("Waiting for host").
    /// </summary>
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.CheckTaskCompletion))]
    [HarmonyPrefix]
    public static bool CheckTaskCompletionPrefix(ref bool __result)
    {
        if (!BorDevOptions.IsTestModeActive)
        {
            return true;
        }

        __result = false;
        return false;
    }

    /// <summary>
    ///     Every game end (votes, kills, tasks, sabotages, MiraAPI game overs) goes through RpcEndGame.
    /// </summary>
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.RpcEndGame))]
    [HarmonyPrefix]
    public static bool RpcEndGamePrefix()
    {
        return _forceEndGame || !BorDevOptions.IsTestModeActive;
    }
}
