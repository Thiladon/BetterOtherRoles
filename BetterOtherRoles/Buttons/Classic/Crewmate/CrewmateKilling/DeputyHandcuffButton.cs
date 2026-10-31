using BetterOtherRoles.Assets;
using BetterOtherRoles.Keybinds;
using BetterOtherRoles.Modules.Roles;
using BetterOtherRoles.Options.Roles.Crewmate;
using BetterOtherRoles.Roles.Crewmate;
using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Keybinds;
using MiraAPI.Translation;
using MiraAPI.Utilities;
using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace BetterOtherRoles.Buttons.Crewmate;

/// <summary>
///     Deputy handcuff button (BetterOtherRoles 1.7.1 logic): puts a handcuff on the closest player,
///     a limited number of times per game.
/// </summary>
public sealed class DeputyHandcuffButton : BorTargetButton<PlayerControl>
{
    public override string Name => MiraLocaleManager.Get("BetterOtherRoles.Role.DeputyHandcuff", "Handcuff");

    public override float Cooldown => OptionGroupSingleton<SheriffOptions>.Instance.DeputyHandcuffCooldown.Value;

    public override int MaxUses => (int)OptionGroupSingleton<SheriffOptions>.Instance.DeputyHandcuffs.Value;

    public override ButtonLocation Location => ButtonLocation.BottomRight;

    public override BaseKeybind Keybind => BorKeybinds.DeputyHandcuff;

    public override Color TextOutlineColor => BorColors.Deputy;

    public override LoadableAsset<Sprite> Sprite => BorAssets.DeputyHandcuffButton;

    // Same distance as the impostors' kill distance, like the Sheriff.
    public override float Distance => GameManager.Instance.LogicOptions.GetKillDistance();

    /// <summary>
    ///     The Deputy, or the Deputy promoted to Sheriff when they keep their handcuffs.
    /// </summary>
    public override bool Enabled(RoleBehaviour? role)
    {
        return role is DeputyRole ||
               (role is SheriffRole && DeputyPromotion.FormerDeputy && DeputyPromotion.FormerDeputy!.AmOwner &&
                OptionGroupSingleton<SheriffOptions>.Instance.DeputyKeepsHandcuffs.Value);
    }

    public override PlayerControl? GetTarget()
    {
        return PlayerControl.LocalPlayer.GetClosestPlayer(true, Distance);
    }

    public override void SetOutline(bool active)
    {
        // The previous target may be gone (left the game) when MiraAPI removes its outline.
        if (Target && Target!.cosmetics)
        {
            Target.cosmetics.SetOutline(active, new Il2CppSystem.Nullable<Color>(BorColors.Deputy));
        }
    }

    protected override void OnClick()
    {
        if (Target == null)
        {
            return;
        }

        DeputyHandcuffs.RpcHandcuff(PlayerControl.LocalPlayer, Target);
        BorSounds.Play("deputyHandcuff");
        ResetTarget();
    }
}
