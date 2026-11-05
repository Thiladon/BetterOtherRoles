using System.Text.RegularExpressions;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Modded servers added to the server list (regionInfo.bor.json, see <see cref="SaveFilePatches"/>), after the vanilla
///     ones, as BetterOtherRoles 1.7 did: each one missing from the list (by name) is added once it is read, then the list
///     is saved. The region chosen stays the same.
///     Their names have no color (the BetterOtherRoles 1.7 ones, with a color tag, are replaced). In the server list of the
///     game creation menu:
///     - the buttons are placed in rows of two (the game puts each one lower than the previous one, whatever its column);
///     - the modded servers are white like the vanilla ones and take their own color under the mouse.
///     On the other screens showing the server (chosen server, server box, enter code, game infos...), its name gets its
///     color tag: the game sets the text colors again after, the tag stays.
/// </summary>
[HarmonyPatch]
public static class ServerRegionPatches
{
    // Name, address (all of them over HTTPS, port 443) and color.
    private static readonly (string Name, string Address, Color Color)[] ModdedServers =
    [
        ("MODDED [NA]", "https://www.aumods.xyz", new Color32(0x2C, 0xC7, 0x1E, 0xFF)),
        ("MODDED [EU]", "https://au-eu.duikbo.at", new Color32(0xDE, 0x0D, 0x68, 0xFF)),
        ("MODDED [AS]", "https://au-as.duikbo.at", new Color32(0xC7, 0xB8, 0x16, 0xFF)),
        ("BOR [EU]", "https://amongus-eu.eno.pm", new Color32(0x26, 0x6E, 0xFF, 0xFF)),
    ];

    // Gap between two rows: two steps of the game (0.275 each), the list keeping its height.
    private const float RowSpacing = 0.55f;
    
    // Screens showing the server: game creation (server box and list), enter code, creation confirmation, game infos,
    // region menu. A player, lobby or chat text with the same name is left as it is.
    private static bool IsServerScreen(TMP_Text text) => text.GetComponentInParent<CreateGameOptions>() || text.GetComponentInParent<ServerDropdown>() || text.GetComponentInParent<EnterCodeManager>() || text.GetComponentInParent<ConfirmCreatePopUp>() || text.GetComponentInParent<FindGameMoreInfoPopup>() || text.GetComponentInParent<RegionMenu>();

    private static readonly Regex ColorTags = new("<[^>]+>", RegexOptions.Compiled);

    // The buttons are reused by the game: color under the mouse of each one before ours.
    private static readonly Dictionary<IntPtr, Color> VanillaOverColors = [];

    // Color tag of each modded server name, for the texts showing it.
    private static readonly Dictionary<string, string> ColoredNames = ModdedServers.ToDictionary(
        x => x.Name,
        x => $"<color=#{ColorUtility.ToHtmlStringRGB(x.Color)}>{x.Name}</color>");

    [HarmonyPatch(typeof(ServerManager), nameof(ServerManager.LoadServers))]
    [HarmonyPostfix]
    public static void LoadServersPostfix(ServerManager __instance)
    {
        var regions = __instance.AvailableRegions?.ToList() ?? [];
        var current = __instance.CurrentRegion;
        var changed = false;
        foreach (var (name, address, _) in ModdedServers)
        {
            var index = regions.FindIndex(x => x.Name == name);
            if (index >= 0) continue;

            // Same server written by BetterOtherRoles 1.7 (name with a color tag): replaced in place.
            var region = CreateRegion(name, address);
            var old = regions.FindIndex(x => ColorTags.Replace(x.Name, string.Empty) == name);
            if (old >= 0)
            {
                if (current != null && current.Name == regions[old].Name)
                    current = region;

                regions[old] = region;
            }
            else
            {
                regions.Add(region);
            }

            changed = true;
        }

        if (!changed) return;

        __instance.AvailableRegions = new Il2CppReferenceArray<IRegionInfo>(regions.ToArray());
        if (current != null && current != __instance.CurrentRegion)
            __instance.CurrentRegion = current;

        __instance.SaveServers();
        Info($"Server list: {ModdedServers.Length} modded servers checked, missing ones added.");
    }

    [HarmonyPatch(typeof(ServerDropdown), nameof(ServerDropdown.FillServerOptions))]
    [HarmonyPostfix]
    public static void FillServerOptionsPostfix(ServerDropdown __instance)
    {
        var buttons = __instance.GetComponentsInChildren<ServerListButton>()
            .OrderByDescending(x => x.transform.localPosition.y)
            .ToList();
        PlaceInRows(buttons);

        foreach (var button in buttons)
        {
            var rollover = button.GetComponent<ButtonRolloverHandler>();
            if (!rollover) continue;

            if (!VanillaOverColors.TryGetValue(button.Pointer, out var vanilla))
            {
                vanilla = rollover.OverColor;
                VanillaOverColors[button.Pointer] = vanilla;
            }

            rollover.OverColor = TryGetColor(button.Text.text, out var color) ? color : vanilla;
        }

    }




    // Rows of two from the top of the game layout, centered on the same height; a last one alone stays on the left.
    private static void PlaceInRows(List<ServerListButton> buttons)
    {
        if (buttons.Count < 2) return;

        var xs = buttons.Select(x => x.transform.localPosition.x).Distinct().OrderBy(x => x).ToList();
        if (xs.Count < 2) return;

        var top = buttons[0].transform.localPosition.y;
        var bottom = buttons[^1].transform.localPosition.y;
        var rows = (buttons.Count + 1) / 2;
        var first = ((top + bottom) / 2f) + ((rows - 1) * RowSpacing / 2f);
        for (var i = 0; i < buttons.Count; i++)
        {
            var position = buttons[i].transform.localPosition;
            buttons[i].transform.localPosition = new Vector3(xs[i % 2 == 0 ? 0 : 1], first - ((i / 2) * RowSpacing), position.z);
        }
    }

    /// <summary>
    ///     A text of a screen showing the server, set to the name of a modded server, gets its color; but the buttons of the
    ///     server list (white, their color under the mouse).
    /// </summary>
    [HarmonyPatch(typeof(TMP_Text), nameof(TMP_Text.text), MethodType.Setter)]
    [HarmonyPrefix]
    public static void TextSetterPrefix(TMP_Text __instance, ref string value)
    {
        if (value == null || !ColoredNames.TryGetValue(value, out var colored) || __instance.GetComponentInParent<ServerListButton>() ||
            !IsServerScreen(__instance))
            return;

        value = colored;
    }

    private static bool TryGetColor(string text, out Color color)
    {
        var name = ColorTags.Replace(text, string.Empty).Trim();
        var server = Array.Find(ModdedServers, x => x.Name == name);
        color = server.Color;
        return server.Name != null;
    }

    private static IRegionInfo CreateRegion(string name, string address)
    {
        var host = address.Replace("https://", string.Empty, StringComparison.Ordinal);
        var servers = new Il2CppReferenceArray<ServerInfo>([new ServerInfo("Http-1", address, 443, false)]);
        return new StaticHttpRegionInfo(name, StringNames.NoTranslation, host, servers).Cast<IRegionInfo>();
    }
}
