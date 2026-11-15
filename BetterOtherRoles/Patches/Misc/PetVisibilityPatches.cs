using BetterOtherRoles.Options;
using HarmonyLib;
using MiraAPI.GameOptions;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Pets in game (TOU-Mira 1.7.9 ShowPetsMode, <see cref="GameplayOptions.PetVisibility"/>):
///     <list type="bullet">
///         <item>everyone: as in the game (the pet of a dead player mourns by the body);</item>
///         <item>own only: each player only sees their own pet;</item>
///         <item>removed on death: the pet of a dead or disconnected player is hidden for everyone, the player included,
///         and back when revived.</item>
///     </list>
///     Checked each frame: the game shows the pets again by itself (meetings, outfit changes). Only the pets hidden
///     here are shown again, when the option allows it or after the game; none in the lobby or in Hide and Seek.
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class PetVisibilityPatches
{
    private static readonly HashSet<byte> Hidden = [];

    public static void Postfix()
    {
        var inGame = ShipStatus.Instance && !LobbyBehaviour.Instance &&
                     !(GameManager.Instance && GameManager.Instance.IsHideAndSeek());
        if (!inGame)
        {
            if (Hidden.Count > 0) ShowAll();
            return;
        }

        var mode = (PetVisibility)OptionGroupSingleton<GameplayOptions>.Instance.PetVisibility.Value;
        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (!player || player.Data == null) continue;

            var pet = player.cosmetics.currentPet;
            if (!pet) continue;

            var hide = mode switch
            {
                PetVisibility.OwnOnly => !player.AmOwner,
                PetVisibility.RemovedOnDeath => player.Data.IsDead || player.Data.Disconnected,
                _ => false,
            };

            if (hide)
            {
                if (pet.gameObject.activeSelf)
                    player.cosmetics.TogglePet(false);
                Hidden.Add(player.PlayerId);
            }
            else if (Hidden.Remove(player.PlayerId))
            {
                player.cosmetics.TogglePet(true);
            }
        }
    }

    // After the game: the players keep their pet in the lobby.
    private static void ShowAll()
    {
        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (player && Hidden.Contains(player.PlayerId))
                player.cosmetics.TogglePet(true);
        }

        Hidden.Clear();
    }
}
