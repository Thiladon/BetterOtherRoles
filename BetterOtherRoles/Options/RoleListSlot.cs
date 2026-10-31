namespace BetterOtherRoles.Options;

/// <summary>
///     Category of a role list slot (TOU-Mira RoleListOption). The value is saved as a number: new values go at the end.
/// </summary>
public enum RoleListSlot
{
    CrewInvestigative,
    CrewKilling,
    CrewProtective,
    CrewPower,
    CrewSupport,

    /// <summary>Investigative, Protective or Support.</summary>
    CrewCommon,

    /// <summary>Killing or Power.</summary>
    CrewSpecial,
    CrewRandom,

    NeutralBenign,
    NeutralEvil,
    NeutralKilling,
    NeutralOutlier,

    /// <summary>Benign or Evil.</summary>
    NeutralCommon,

    /// <summary>Outlier or Killing.</summary>
    NeutralSpecial,

    /// <summary>Benign, Evil or Outlier.</summary>
    NeutralWildcard,
    NeutralRandom,

    ImpostorConcealing,
    ImpostorKilling,
    ImpostorPower,
    ImpostorSupport,

    /// <summary>Concealing or Support.</summary>
    ImpostorCommon,

    /// <summary>Killing or Power.</summary>
    ImpostorSpecial,
    ImpostorRandom,

    /// <summary>Any crewmate or neutral role.</summary>
    NonImpostor,

    /// <summary>An impostor or a non-impostor role, drawn for the impostor count.</summary>
    Any,
}
