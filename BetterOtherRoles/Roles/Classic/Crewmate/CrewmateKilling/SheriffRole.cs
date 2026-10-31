using BetterOtherRoles.Options.Roles.Crewmate;
using BetterOtherRoles.Utilities;
using MiraAPI.GameOptions;
using MiraAPI.Roles;
using MiraAPI.Translation;
using UnityEngine;

namespace BetterOtherRoles.Roles.Crewmate;

/// <summary>
///     Sheriff: a crewmate who can shoot the impostors (and the neutrals, if allowed).
/// </summary>
public sealed class SheriffRole(IntPtr cppPtr) : CrewmateRole(cppPtr), IBorRole, IHasDependentRole
{
    public ushort? DependentRoleId => OptionGroupSingleton<SheriffOptions>.Instance.HasDeputy ? RoleId.Get<DeputyRole>() : null;

    public int DependentRoleChance => (int)OptionGroupSingleton<SheriffOptions>.Instance.DeputyChance.Value;

    public string IdPart => "Sheriff";

    public Color RoleColor => BorColors.Sheriff;

    public ModdedRoleTeams Team => ModdedRoleTeams.Crewmate;

    public BorRoleAlignment Alignment => BorRoleAlignment.CrewmateKilling;

    public CustomRoleConfiguration Configuration => new(this)
    {
        MaxRoleCount = 1,
        DefaultRoleCount = 1,
        DefaultChance = 0,
    };

    /// <summary>
    ///     Gets the wiki description: the Sheriff options without the Deputy ones (they are on the Deputy page).
    /// </summary>
    public string RoleWikiDescription
    {
        get
        {
            var role = (ICustomRole)this;
            var group = OptionGroupSingleton<SheriffOptions>.Instance;
            return MiraLocaleManager.Get(role.RoleWikiDescriptionLocale, role.RoleLongDescription) +
                   BorWikiUtils.GetOptionsText(group.Children.Except(group.DeputyOptions));
        }
    }
}
