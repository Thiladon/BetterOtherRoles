using BetterOtherRoles.Modules.Shield;
using BetterOtherRoles.Options;
using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
using MiraAPI.Translation;
using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace BetterOtherRoles.Modifiers.Universal;

/// <summary>
///     Shield of the first player murdered in the previous game (<see cref="BorFirstKillShield"/>): given by the host when
///     the game starts, taken back when it ends. Rainbow outline seen by everyone (BetterOtherRoles 1.7.1), icon and
///     description for the shielded player (TOU-Mira 1.7.9 FirstDeadShield, <see cref="ShieldOptions.FirstKillShieldInfo"/>).
/// </summary>
public sealed class FirstKillShieldModifier : BaseModifier
{
    private static readonly LoadableAsset<Sprite> Icon = new LoadableResourceAsset("BetterOtherRoles.Resources.Modifiers.FirstKillShield.png");

    public override string ModifierName => MiraLocaleManager.Get("BetterOtherRoles.Modifier.FirstKillShield", "First Kill Shield");

    public override LoadableAsset<Sprite>? ModifierIcon => Icon;

    public override bool HideOnUi => !OptionGroupSingleton<ShieldOptions>.Instance.FirstKillShieldInfo.Value;

    public override string GetDescription() =>
        MiraLocaleManager.Get("BetterOtherRoles.Modifier.FirstKillShieldDescription", "You are shielded because you were the first one killed last game.");

    public override void Update()
    {
        if (Player && Player.cosmetics) BorFirstKillShield.ShowOutline(Player);
    }

    public override void OnDeactivate()
    {
        if (Player && Player.cosmetics) Player.cosmetics.SetOutline(false, new Il2CppSystem.Nullable<Color>(Color.clear));
    }

    public override void OnDeath(DeathReason reason)
    {
        Player.RemoveModifier<FirstKillShieldModifier>();
    }
}
