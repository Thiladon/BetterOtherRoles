namespace BetterOtherRoles.Utilities;

/// <summary>
///     Effects of a shield stopping a kill, for the shields of the mod (first kill shield, the Medic later).
/// </summary>
public static class BorShieldEffects
{
    /// <summary>
    ///     Animation of the game when a kill hits a protection (PlayerControl.ShowFailedMurder, guardian angel), shown on
    ///     this client only: the attacker. Without taking away the protection of a guardian angel, as the game does.
    /// </summary>
    /// <param name="target">The shielded player.</param>
    public static void PlayFailedKill(PlayerControl target)
    {
        if (!target || !RoleManager.Instance || !RoleManager.Instance.protectAnim) return;

        var animation = UnityEngine.Object.Instantiate(RoleManager.Instance.protectAnim, target.gameObject.transform);
        animation.SetMaskLayerBasedOnWhoShouldSee(true);
        animation.Play(target, null, target.cosmetics.FlipX, RoleEffectAnimation.SoundType.Global);
    }
}
