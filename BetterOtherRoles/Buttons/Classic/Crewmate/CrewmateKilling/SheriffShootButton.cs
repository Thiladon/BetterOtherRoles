using BetterOtherRoles.Options.Roles.Crewmate;
using BetterOtherRoles.Roles.Crewmate;
using BetterOtherRoles.Utilities;
using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Keybinds;
using MiraAPI.Networking;
using MiraAPI.Roles;
using MiraAPI.Translation;
using MiraAPI.Utilities;
using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace BetterOtherRoles.Buttons.Crewmate;

/// <summary>
///     Sheriff shoot button (BetterOtherRoles 1.7.1 logic): kills the target if it is an impostor
///     (or a neutral when allowed), otherwise the sheriff dies.
/// </summary>
public sealed class SheriffShootButton : BorTargetButton<PlayerControl>
{
    private LoadableAsset<Sprite>? _sprite;

    public override string Name => MiraLocaleManager.Get("BetterOtherRoles.Role.SheriffKill", "Kill");

    public override float Cooldown => OptionGroupSingleton<SheriffOptions>.Instance.Cooldown;

    // Same as the impostors' kill cooldown at the start of the game. MiraAPI then uses Cooldown after each
    // click and after meetings.
    public override float InitialCooldown => 10f;

    public override ButtonLocation Location => ButtonLocation.BottomRight;

    // Same key as the impostors' kill button (Rewired action Kill), as in BetterOtherRoles 1.7.1.
    public override BaseKeybind Keybind => VanillaKeybinding<KillButton>.Instance;

    protected override bool FollowsKillTimer => true;

    public override Color TextOutlineColor => BorColors.Sheriff;

    // Vanilla kill button sprite, as in BetterOtherRoles 1.7.1.
    public override LoadableAsset<Sprite> Sprite =>
        _sprite ??= new LoadableAssetWrapper<Sprite>(BorSpriteUtils.ToStandalone(HudManager.Instance.KillButton.graphic.sprite));

    // Same distance as the impostors' kill distance.
    public override float Distance => GameManager.Instance.LogicOptions.GetKillDistance();

    public override bool Enabled(RoleBehaviour? role)
    {
        return role is SheriffRole;
    }

    public override PlayerControl? GetTarget()
    {
        return PlayerControl.LocalPlayer.GetClosestPlayer(true, Distance);
    }

    public override void SetOutline(bool active)
    {
        // The previous target may be gone (left the game) when MiraAPI removes its outline.
        if (Target && Target!.cosmetics)
            Target.cosmetics.SetOutline(active, new Il2CppSystem.Nullable<Color>(BorColors.Sheriff));
    }

    protected override void OnClick()
    {
        if (Target == null) return;

        var victim = CanBeShot(Target) ? Target : PlayerControl.LocalPlayer;
        PlayerControl.LocalPlayer.RpcCustomMurder(victim, MeetingCheck.OutsideMeeting);
        ResetTarget();
    }

    private static bool CanBeShot(PlayerControl target)
    {
        var role = target.Data.Role;

        return role.IsImpostor || (role is ICustomRole { Team: ModdedRoleTeams.Custom } &&
            OptionGroupSingleton<SheriffOptions>.Instance.CanKillNeutrals);
        
    }
}
