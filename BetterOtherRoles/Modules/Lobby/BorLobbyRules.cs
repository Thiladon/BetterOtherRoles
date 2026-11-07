using System.Collections;
using BepInEx;
using BetterOtherRoles.Options;
using BetterOtherRoles.Utilities;
using HarmonyLib;
using Hazel;
using MiraAPI.GameOptions;
using MiraAPI.Translation;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using Reactor.Utilities;
using UnityEngine;

namespace BetterOtherRoles.Modules.Lobby;

/// <summary>
///     Rules of the lobby (TOU-Mira 1.7.9 /rules): written by the host in <see cref="FileName"/>, in BepInEx/plugins,
///     read again at each use (a change counts at once); the text formatting of the game works in it (colors, bold).
///     A player asks the host for them (/rules), the host sends them to that player only; /rules show sends them to
///     everyone (host only); with <see cref="LobbyOptions.ShowRulesOnJoin"/>, each player joining the lobby gets them once.
/// </summary>
[HarmonyPatch]
public static class BorLobbyRules
{
    /// <summary>
    ///     Gets the name of the file of the rules, in BepInEx/plugins.
    /// </summary>
    public const string FileName = "LobbyRules.txt";

    // Longer, the message could be too big to be sent: cut, and the host is told.
    private const int MaxLength = 1000;

    // Time between two requests of a player, and wait before sending the rules to a player joining (fully spawned).
    private const float RequestDelay = 3f;
    private const float JoinDelay = 1.5f;

    // Clients of the lobby who got the rules on joining (once each, not again after a game).
    private static readonly HashSet<int> SentOnJoin = [];

    private static float _lastRequest = float.MinValue;

    /// <summary>
    ///     Gets the path of the file of the rules.
    /// </summary>
    public static string FilePath => Path.Combine(Paths.PluginPath, FileName);

    /// <summary>
    ///     Reads the rules of the local player (the host): empty when there are none.
    /// </summary>
    /// <param name="cut">Whether the rules were longer than <see cref="MaxLength"/> characters and were cut.</param>
    /// <returns>The rules.</returns>
    public static string ReadRules(out bool cut)
    {
        cut = false;
        string text;
        try
        {
            text = File.Exists(FilePath) ? File.ReadAllText(FilePath) : string.Empty;
        }
        catch (Exception e)
        {
            Warning($"Cannot read {FilePath}: {e.Message}");
            return string.Empty;
        }

        text = text.Replace("\r\n", "\n").Trim();
        if (text.Length <= MaxLength) return text;

        cut = true;
        return text[..MaxLength];
    }

    /// <summary>
    ///     /rules: the host shows its rules (or where to write them); another player asks the host for them.
    /// </summary>
    public static void ShowOwn()
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            if (Time.unscaledTime - _lastRequest < RequestDelay)
            {
                BorChat.AddSystemMessage(MiraLocaleManager.Get("BetterOtherRoles.Rules.Wait", "Wait a little before asking for the rules again."));
                return;
            }

            _lastRequest = Time.unscaledTime;
            Rpc<RequestRpc>.Instance.SendTo(PlayerControl.LocalPlayer, AmongUsClient.Instance.HostId, string.Empty);
            return;
        }

        var rules = ReadRules(out var cut);
        if (rules.Length == 0)
        {
            BorChat.AddSystemMessage(MiraLocaleManager.Get("BetterOtherRoles.Rules.NoFile", "No rules: write them in {0}.")
                .Replace("{0}", $"<noparse>{FilePath}</noparse>"));
            return;
        }

        Show(rules);
        if (cut)
            TellCut();
    }

    /// <summary>
    ///     /rules show: the host sends its rules to everyone.
    /// </summary>
    public static void ShowToAll()
    {
        var rules = ReadRules(out var cut);
        if (rules.Length == 0)
        {
            BorChat.AddSystemMessage(MiraLocaleManager.Get("BetterOtherRoles.Rules.NoFile", "No rules: write them in {0}.")
                .Replace("{0}", $"<noparse>{FilePath}</noparse>"));
            return;
        }

        Rpc<RulesRpc>.Instance.Send(PlayerControl.LocalPlayer, rules);
        Show(rules);
        if (cut)
            TellCut();
    }

    private static void Show(string rules)
    {
        if (rules.Length == 0)
        {
            BorChat.AddSystemMessage(MiraLocaleManager.Get("BetterOtherRoles.Rules.None", "The host has no rules."));
            return;
        }

        BorChat.AddSystemMessage($"{MiraLocaleManager.Get("BetterOtherRoles.Rules.Title", "Lobby rules:")}\n<size=75%>{rules}</size>");
    }

    private static void TellCut()
    {
        BorChat.AddSystemMessage(MiraLocaleManager.Get("BetterOtherRoles.Rules.Cut", "The rules were cut after {0} characters.")
            .Replace("{0}", MaxLength.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
    [HarmonyPostfix]
    public static void OnGameJoinedPostfix()
    {
        SentOnJoin.Clear();
    }

    // A player joining the lobby: the host sends it the rules once, if it has some.
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Start))]
    [HarmonyPostfix]
    public static void PlayerStartPostfix(PlayerControl __instance)
    {
        if (!AmongUsClient.Instance || !AmongUsClient.Instance.AmHost || __instance.AmOwner || !LobbyBehaviour.Instance) return;
        if (!OptionGroupSingleton<LobbyOptions>.Instance.ShowRulesOnJoin.Value || !SentOnJoin.Add(__instance.OwnerId)) return;

        Coroutines.Start(CoSendOnJoin(__instance));
    }

    private static IEnumerator CoSendOnJoin(PlayerControl player)
    {
        yield return new WaitForSecondsRealtime(JoinDelay);
        if (!player || !AmongUsClient.Instance || !AmongUsClient.Instance.AmHost || !PlayerControl.LocalPlayer) yield break;

        var rules = ReadRules(out _);
        if (rules.Length > 0)
            Rpc<RulesRpc>.Instance.SendTo(PlayerControl.LocalPlayer, player.OwnerId, rules);
    }

    /// <summary>
    ///     A player asks the host for the rules (sent to the host only): its sender is the player object of the RPC.
    /// </summary>
    [RegisterCustomRpc((uint)BorRpc.RulesRequest)]
    public sealed class RequestRpc(BetterOtherRolesPlugin plugin, uint id) : PlayerCustomRpc<BetterOtherRolesPlugin, string>(plugin, id)
    {
        public override RpcLocalHandling LocalHandling => RpcLocalHandling.None;

        public override void Write(MessageWriter writer, string? data) => writer.Write(data ?? string.Empty);

        public override string Read(MessageReader reader) => reader.ReadString();

        public override void Handle(PlayerControl innerNetObject, string? data)
        {
            if (!AmongUsClient.Instance.AmHost || !innerNetObject || !PlayerControl.LocalPlayer) return;

            Rpc<RulesRpc>.Instance.SendTo(PlayerControl.LocalPlayer, innerNetObject.OwnerId, ReadRules(out _));
        }
    }

    /// <summary>
    ///     The rules of the host (empty: none), to one player or to everyone; shown only when sent by the host.
    /// </summary>
    [RegisterCustomRpc((uint)BorRpc.Rules)]
    public sealed class RulesRpc(BetterOtherRolesPlugin plugin, uint id) : PlayerCustomRpc<BetterOtherRolesPlugin, string>(plugin, id)
    {
        public override RpcLocalHandling LocalHandling => RpcLocalHandling.None;

        public override void Write(MessageWriter writer, string? data) => writer.Write(data ?? string.Empty);

        public override string Read(MessageReader reader) => reader.ReadString();

        public override void Handle(PlayerControl innerNetObject, string? data)
        {
            if (!innerNetObject || innerNetObject.OwnerId != AmongUsClient.Instance.HostId) return;

            Show(data ?? string.Empty);
        }
    }
}
