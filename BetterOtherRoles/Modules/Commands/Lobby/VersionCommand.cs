using System.Text;
using BetterOtherRoles.Modules.Lobby;
using MiraAPI.Translation;

namespace BetterOtherRoles.Modules.Commands.Lobby;

/// <summary>
///     /version: the mod version and build, then each player with its build (<see cref="BorBuildCheck"/>):
///     ✓ same build as the host, ✖ another build, … not received yet.
/// </summary>
public sealed class VersionCommand : BorCommand
{
    public override string Name => "version";

    public override CommandPlace Place => CommandPlace.Lobby;

    public override void Run(string args)
    {
        var builder = new StringBuilder(MiraLocaleManager.Get("BetterOtherRoles.Command.Version.Local", "Version {0}, build {1}.")
            .Replace("{0}", BetterOtherRolesPlugin.Version)
            .Replace("{1}", BorBuildCheck.ToShort(BorBuildCheck.LocalBuild)));

        var hostId = AmongUsClient.Instance.HostId;
        var hasHostBuild = BorBuildCheck.TryGetBuild(hostId, out var hostBuild);
        foreach (var client in AmongUsClient.Instance.allClients.ToArray().Where(x => x.Character))
        {
            string mark;
            string build;
            if (!BorBuildCheck.TryGetBuild(client.Id, out var guid))
            {
                mark = "<color=#BBBBBB>…</color>";
                build = MiraLocaleManager.Get("BetterOtherRoles.Command.Version.Waiting", "not received");
            }
            else
            {
                mark = !hasHostBuild || guid == hostBuild ? "<color=#00C000>✓</color>" : "<color=#FF1919>✖</color>";
                build = BorBuildCheck.ToShort(guid);
            }

            var name = client.Id == hostId
                ? MiraLocaleManager.Get("BetterOtherRoles.Command.Version.Host", "{0} (host)").Replace("{0}", client.PlayerName)
                : client.PlayerName;
            builder.Append('\n').Append(MiraLocaleManager.Get("BetterOtherRoles.Command.Version.Line", "{0} {1}: {2}")
                .Replace("{0}", mark)
                .Replace("{1}", name)
                .Replace("{2}", build));
        }

        BorCommandUtils.Reply(builder.ToString());
    }
}
