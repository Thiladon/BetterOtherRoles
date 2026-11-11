using BetterOtherRoles.Assets;
using BetterOtherRoles.Modules.Admin;
using BetterOtherRoles.Modules.Debug;
using BetterOtherRoles.Options;
using HarmonyLib;
using MiraAPI.GameOptions;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Debug Mode (<see cref="BorDevOptions.TestMode"/>), for the host with the admin key:
///     - game rules: start with less than 4 players, and no game end;
///     - chat kept during the game, for the Debug commands (Modules/Commands/Debug);
///     - Debug button under the role guide button, opening the Debug menu (<see cref="BorDebugMenu"/>).
/// </summary>
[HarmonyPatch]
public static class TestModePatches
{
    private static int _vanillaMinPlayers = -1;
    private static bool _forceEndGame;
    private static GameObject? _debugButton;

    // ---------------------------------------------------------------------------------------------------------------
    // Game rules
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>
    ///     Ends the game despite the test mode (/endgame command, Debug menu).
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
            _vanillaMinPlayers = __instance.MinPlayers;

        TurnOffWithoutAdminKey();

        var minPlayers = BorDevOptions.IsTestModeActive ? 1 : _vanillaMinPlayers;
        if (__instance.MinPlayers == minPlayers) return;

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
            option.SetValue(false);
    }

    /// <summary>
    ///     Game end checks that always say "no" in test mode, the game end being blocked:
    ///     - after an ejection, the game only gives the control back if the game is not lost by deaths (impostors as many
    ///       as the crewmates): it then waited for the game end and stayed on a black screen;
    ///     - before opening a meeting, the host checks the task win: alone as an impostor, there is no crewmate task, so
    ///       the tasks counted as done and the meeting never opened ("Waiting for host").
    /// </summary>
    [HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.IsGameOverDueToDeath))]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.CheckTaskCompletion))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static bool NoGameEndCheckPrefix(ref bool __result)
    {
        if (!BorDevOptions.IsTestModeActive) return true;

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

    // ---------------------------------------------------------------------------------------------------------------
    // Chat kept during the game (the commands are in Modules/Commands)
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>
    ///     Shows the chat to the host once the intro is over.
    /// </summary>
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    [HarmonyPostfix]
    public static void HudManagerUpdatePostfix(HudManager __instance)
    {
        if (IntroStatePatches.IntroEnded && BorDebugActions.CanUse && !__instance.Chat.chatButton.gameObject.activeSelf)
            __instance.Chat.SetVisible(true);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Debug button
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>
    ///     Debug button under the role guide button (copy of it): opens the Debug menu. Created once per HUD; shown only
    ///     to the host with the admin key, in game after the intro, with the Debug Mode (<see cref="BorDebugActions.CanUse"/>).
    ///     Called by <see cref="VerticalButtonGroupPatches"/>, which places it.
    /// </summary>
    /// <param name="guide">The role guide button.</param>
    public static void RefreshDebugButton(Transform guide)
    {
        if (!_debugButton)
            _debugButton = CreateDebugButton(guide);

        var visible = BorDebugActions.CanUse && IntroStatePatches.IntroEnded;
        if (_debugButton!.activeSelf != visible)
            _debugButton.SetActive(visible);
    }

    // No Selected sprite: the button is hidden once the menu opens.
    private static GameObject CreateDebugButton(Transform guide) =>
        VerticalButtonGroupPatches.CreateButton(guide, "BorDebugButton", BorAssets.DebugButton, BorAssets.DebugButtonActive, BorDebugMenu.Open);
}
