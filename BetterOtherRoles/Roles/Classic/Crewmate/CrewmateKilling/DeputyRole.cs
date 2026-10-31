using BetterOtherRoles.Options.Roles.Crewmate;
using BetterOtherRoles.Utilities;
using MiraAPI.GameOptions;
using MiraAPI.Roles;
using MiraAPI.Translation;
using UnityEngine;

namespace BetterOtherRoles.Roles.Crewmate;

/// <summary>
///     Deputy: the Sheriff's assistant, who can handcuff players.
///     Not drawn by MiraAPI: it is given alongside the Sheriff (<see cref="SheriffOptions.HasDeputy"/>),
///     and its options live in <see cref="SheriffOptions"/>.
/// </summary>
public sealed class DeputyRole(IntPtr cppPtr) : CrewmateRole(cppPtr), IBorRole
{
    public string IdPart => "Deputy";

    public Color RoleColor => BorColors.Deputy;

    public ModdedRoleTeams Team => ModdedRoleTeams.Crewmate;

    public BorRoleAlignment Alignment => BorRoleAlignment.CrewmateKilling;

    public CustomRoleConfiguration Configuration => new(this)
    {
        HideSettings = true,
        MaxRoleCount = 0,
        DefaultRoleCount = 0,
        DefaultChance = 0,
        CanModifyChance = false,
    };

    /// <summary>
    ///     Gets the wiki description, with the Deputy options (they are in the Sheriff group).
    /// </summary>
    public string RoleWikiDescription
    {
        get
        {
            var role = (ICustomRole)this;
            return MiraLocaleManager.Get(role.RoleWikiDescriptionLocale, role.RoleLongDescription) +
                   BorWikiUtils.GetOptionsText(OptionGroupSingleton<SheriffOptions>.Instance.DeputyOptions);
        }
    }
}
