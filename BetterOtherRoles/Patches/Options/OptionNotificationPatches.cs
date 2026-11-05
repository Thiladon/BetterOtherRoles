using HarmonyLib;
using MiraAPI.GameOptions;
using MiraAPI.Roles;
using MiraAPI.Utilities;
using TMPro;
using UnityEngine;

namespace BetterOtherRoles.Patches.Options;

/// <summary>
///     Notification of a changed option in the lobby ("{option} set to {value}", MiraAPI), with what the option belongs to
///     first, the option keeping its short name in the settings menu:
///     - option of a role (role option group): the role name, automatically ("Sheriff > Kill Cooldown set to 30s");
///     - option of another role kept in that group: <c>Option.SetNotificationRole&lt;DeputyRole&gt;();</c>, with the color
///       and icon of that role ("Deputy > Number Of Handcuffs set to 3");
///     - sub-option: <c>Child.SetNotificationParent(Parent);</c> ("Cooldowns Stop In Minigames > Impostors set to Off").
///     Declare them in the group constructor (only for the options created there, not the attribute ones).
/// </summary>
[HarmonyPatch]
public static class OptionNotificationPatches
{
    private const string Font = "<font=\"Barlow-Black SDF\" material=\"Barlow-Black Outline\">";

    private static readonly List<(IModdedOption Child, IModdedOption Parent)> Parents = [];
    private static readonly List<(IModdedOption Option, Type Role)> Roles = [];

    // Every option registered by MiraAPI (internal there), to find the group of an option from its key.
    private static Dictionary<uint, IModdedOption>? _allOptions;

    /// <summary>
    ///     Shows the parent option before this option in its change notification.
    /// </summary>
    /// <param name="child">The sub-option.</param>
    /// <param name="parent">The option it depends on.</param>
    public static void SetNotificationParent(this IModdedOption child, IModdedOption parent)
    {
        Parents.Add((child, parent));
    }

    /// <summary>
    ///     Shows this role (name, color, icon) before the option in its change notification, instead of the role of its group.
    /// </summary>
    /// <typeparam name="TRole">The role the option is about.</typeparam>
    /// <param name="option">The option.</param>
    public static void SetNotificationRole<TRole>(this IModdedOption option)
        where TRole : ICustomRole
    {
        Roles.Add((option, typeof(TRole)));
    }

    /// <summary>
    ///     Same message as MiraAPI (game font, color, optional icon), with what the option belongs to first.
    /// </summary>
    [HarmonyPatch(typeof(ModdedOptionsManager), nameof(ModdedOptionsManager.AddSettingsChangeMessage))]
    [HarmonyPrefix]
    public static bool AddSettingsChangeMessagePrefix(
        NotificationPopper notif,
        StringNames key,
        string value,
        Color textColor,
        TMP_SpriteAsset? sprite,
        bool playSound)
    {
        var translation = TranslationController.Instance;
        string? owner = null;

        var parent = Parents.FirstOrDefault(x => x.Child.StringName == key).Parent;
        if (parent != null)
        {
            owner = translation.GetString(parent.StringName);
        }
        else if (GetRole(key) is { } role)
        {
            owner = role.RoleName;
            textColor = role.RoleColor;
            sprite = role.Configuration.IconTmp;
        }

        if (owner == null) return true;

        var icon = sprite ? $"<sprite name=\"{sprite!.name}\">" : string.Empty;
        var message = translation.GetString(
            StringNames.LobbyChangeSettingNotification,
            new Il2CppSystem.Object[]
            {
                $"{icon}{Font}{textColor.ToTextColor()}{owner} > {translation.GetString(key)}</color></font>",
                $"{Font}{value}</font>",
            });
        notif.SettingsChangeMessageLogic(key, message, playSound);
        return false;
    }

    // The role declared for the option, or the role of its group (role option group), or null.
    private static ICustomRole? GetRole(StringNames key)
    {
        var roleType = Roles.FirstOrDefault(x => x.Option.StringName == key).Role;
        if (roleType == null)
        {
            _allOptions ??= (Dictionary<uint, IModdedOption>?)AccessTools.Field(typeof(ModdedOptionsManager), "ModdedOptions")?.GetValue(null);
            var option = _allOptions?.Values.FirstOrDefault(x => x.StringName == key);
            roleType = option?.ParentGroup?.OptionableType;
        }

        return roleType != null && typeof(ICustomRole).IsAssignableFrom(roleType)
            ? CustomRoleManager.CustomMiraRoles.FirstOrDefault(x => x.GetType() == roleType)
            : null;
    }
}
