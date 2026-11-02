using System.Globalization;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using BetterOtherRoles.Modules.Admin;
using BetterOtherRoles.Modules.Localization;
using BetterOtherRoles.Modules.Presets;
using BetterOtherRoles.Patches.Misc;
using HarmonyLib;
using MiraAPI;
using Reactor;
using Reactor.Networking;
using Reactor.Networking.Attributes;
using Reactor.Utilities;

namespace BetterOtherRoles;

/// <summary>
///     Plugin class for BetterOtherRole: MiraAPI.
/// </summary>
[BepInAutoPlugin("thilladon.bor.mira", "BetterOtherRoles - MiraAPI")]
[BepInProcess("Among Us.exe")]
[BepInDependency(ReactorPlugin.Id)]
[BepInDependency(MiraApiPlugin.Id)]
[ReactorModFlags(ModFlags.RequireOnAllClients)]
public partial class BetterOtherRolesPlugin : BasePlugin, IMiraPlugin
{
    public static bool IsMobile => Constants.GetPlatformType() is Platforms.Android or Platforms.IPhone;
    /// <summary>
    ///     Gets the culture used for string manipulations.
    /// </summary>
    public static CultureInfo Culture { get; } = new("en-US");

    /// <summary>
    ///     Gets a value indicating whether the current build is a dev build (beta or wip).
    /// </summary>
    public static bool IsDevBuild => IsBetaBuild || IsWipBuild;

    /// <summary>
    ///     Gets a value indicating whether the current build is a beta build.
    /// </summary>
    public static bool IsBetaBuild => Version.Contains("beta", StringComparison.OrdinalIgnoreCase) || Version.Contains("prerelease", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Gets a value indicating whether the current build is a work in progress build.
    /// </summary>
    public static bool IsWipBuild => Version.Contains("alpha", StringComparison.OrdinalIgnoreCase) || Version.Contains("dev", StringComparison.OrdinalIgnoreCase) || Version.Contains("ci", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Gets the Harmony instance for patching.
    /// </summary>
    public Harmony Harmony { get; } = new(Id);

    /// <inheritdoc />
    public string OptionsTitleText => "BOR Mira";

    /// <inheritdoc />
    public string CustomOptionMenuNameOne => "Roles"; // Work around later
    public string CustomOptionMenuNameTwo => "Modifiers";
    public string CustomOptionMenuOneDescription => "Role-specific options.";

    /// <inheritdoc />
    public string CustomOptionMenuTwoDescription => "Modifier-specific options.";

    /// <inheritdoc />
    public string ModifierMenuDescription => "Modifiers that can be added to any role.";

    /// <inheritdoc />
    public ConfigFile GetConfigFile() => Config;

    /// <summary>
    ///     The Load method for the plugin.
    /// </summary>
    public override void Load()
    {
        Info($"{Name} v{Version} is loading...");

        SaveFilePatches.Initialize();
        ReactorCredits.Register(Name, Version, IsDevBuild, ReactorCredits.AlwaysShow);
        BorLocale.Initialize();
        BorAdmin.Initialize(Config);
        BorPresets.Initialize(Config);

        Harmony.PatchAll();
        SaveFilePatches.ReloadData();

        Info($"{Name} loaded!");
    }
}
