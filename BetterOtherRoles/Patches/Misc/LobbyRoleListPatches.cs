using System.Text;
using System.Text.RegularExpressions;
using BetterOtherRoles.Options;
using HarmonyLib;
using MiraAPI.GameOptions;
using MiraAPI.Translation;
using TMPro;
using UnityEngine;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Lobby summary of the role assignment, at the top left (TOU-Mira HudManagerPatches RoleList text):
///     the 15 slots of the role list (the ones beyond the player count faded, unused), or the min/max of each faction.
///     Rebuilt every half second, so a language or option change shows up on its own.
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class LobbyRoleListPatches
{
    private const float RefreshInterval = 0.5f;
    private const string TitleColor = "#FFD700";
    // Alpha of the unused slots: their colors faded.
    private const string UnusedAlpha = "60";

    // Every slot label starts at the same place, whatever the width of its number ("1." or "15.").
    private const string LabelPosition = "1.4em";

    private static readonly Regex ColorAlpha = new("<color=(#[0-9A-Fa-f]{6})([0-9A-Fa-f]{2})?>", RegexOptions.Compiled);
    private static readonly StringBuilder Text = new();

    // Distance from the top of the screen: in world units, plus a part of the window height (where the FPS counter
    // used to be, the place is kept).
    private const float TopMargin = 0.1f;
    private const float TopRatio = 0.03f;

    private static TextMeshPro? _text;
    private static AspectPosition? _position;
    private static float _lastRefresh;

    // The part of the window height converted to world units: the screen is 2 × orthographicSize high.
    private static void UpdatePosition()
    {
        var camera = Camera.main;
        if (!_position || !camera) return;

        var offset = TopRatio * camera!.orthographicSize * 2f;
        var distance = _position!.DistanceFromEdge;
        if (Mathf.Abs(distance.y - (TopMargin + offset)) < 0.001f) return;

        distance.y = TopMargin + offset;
        _position.DistanceFromEdge = distance;
        _position.AdjustPosition();
    }

    [HarmonyPostfix]
    public static void UpdatePostfix(HudManager __instance)
    {
        var mode = (RoleAssignmentMode)OptionGroupSingleton<RoleAssignmentOptions>.Instance.Mode.Value;
        var shown = LobbyBehaviour.Instance && !(GameManager.Instance && GameManager.Instance.IsHideAndSeek()) &&
                    mode is RoleAssignmentMode.RoleList or RoleAssignmentMode.MinMaxList;
        if (!shown)
        {
            if (_text)
                _text!.gameObject.SetActive(false);

            return;
        }

        if (!_text && !Create(__instance)) return;

        _text!.gameObject.SetActive(true);
        UpdatePosition();
        if (Time.time - _lastRefresh < RefreshInterval) return;

        _lastRefresh = Time.time;
        var text = mode is RoleAssignmentMode.RoleList ? BuildRoleList() : BuildMinMax();
        if (_text.text != text)
            _text.text = text;
    }

    // A copy of the ping text, at the top left of the screen.
    private static bool Create(HudManager hud)
    {
        var ping = UnityEngine.Object.FindObjectOfType<PingTracker>(true);
        if (!ping) return false;

        var copy = UnityEngine.Object.Instantiate(ping.gameObject, hud.transform);
        UnityEngine.Object.Destroy(copy.GetComponent<PingTracker>());
        copy.name = "BorLobbyRoleList";

        _position = copy.GetComponent<AspectPosition>();
        if (_position)
        {
            _position!.Alignment = AspectPosition.EdgeAlignments.LeftTop;
            _position.DistanceFromEdge = new Vector3(0.43f, TopMargin, 1f);
            _position.AdjustPosition();
        }

        _text = copy.GetComponent<TextMeshPro>();
        _text.alignment = TextAlignmentOptions.TopLeft;
        _text.verticalAlignment = VerticalAlignmentOptions.Top;
        _text.fontSize = _text.fontSizeMin = _text.fontSizeMax = 2.5f;
        _text.text = string.Empty;
        _lastRefresh = 0f;
        return true;
    }

    private static string BuildRoleList()
    {
        var options = OptionGroupSingleton<RoleAssignmentOptions>.Instance;
        var players = GameData.Instance ? GameData.Instance.PlayerCount : 0;

        Text.Clear();
        AppendTitle(MiraLocaleManager.Get("BetterOtherRoles.Lobby.RoleListTitle", "Role List:"));
        for (var i = 0; i < RoleAssignmentOptions.RoleListSlotCount; i++)
        {
            var slot = options.Slots[i].Value;
            var label = MiraLocaleManager.Get($"BetterOtherRoles.RoleListSlot.{slot}", slot.ToString());
            if (i < players)
            {
                Text.Append(BetterOtherRolesPlugin.Culture, $"{i + 1}.<pos={LabelPosition}>{label}\n");
            }
            else
            {
                // Unused with the current player count.
                var faded = ColorAlpha.Replace(label, "<color=${1}" + UnusedAlpha + ">");
                Text.Append(BetterOtherRolesPlugin.Culture, $"<color=#FFFFFF{UnusedAlpha}>{i + 1}.<pos={LabelPosition}>{faded}</color>\n");
            }
        }

        return Text.ToString();
    }

    private static string BuildMinMax()
    {
        var options = OptionGroupSingleton<RoleAssignmentOptions>.Instance;

        Text.Clear();
        AppendTitle(MiraLocaleManager.Get("BetterOtherRoles.Lobby.MinMaxTitle", "Roles per Faction:"));
        AppendMinMax("BetterOtherRoles.Lobby.CrewmateRoles", options.MinCrewmateRoles.Value, options.MaxCrewmateRoles.Value);
        AppendMinMax("BetterOtherRoles.Lobby.NeutralRoles", options.MinNeutralRoles.Value, options.MaxNeutralRoles.Value);
        AppendMinMax("BetterOtherRoles.Lobby.ImpostorRoles", options.MinImpostorRoles.Value, options.MaxImpostorRoles.Value);
        return Text.ToString();
    }

    private static void AppendTitle(string title)
    {
        Text.Append(BetterOtherRolesPlugin.Culture, $"<color={TitleColor}>{title}</color>\n");

        // A small gap under the title.
        Text.Append("<size=50%>\n</size>");
    }

    // "{0} - {1}" line, the min lowered to the max as the assignment does.
    private static void AppendMinMax(string key, float min, float max)
    {
        var format = MiraLocaleManager.Get(key, key + ": {0} - {1}");
        Text.Append(string.Format(BetterOtherRolesPlugin.Culture, format, Math.Min((int)min, (int)max), (int)max)).Append('\n');
    }
}
