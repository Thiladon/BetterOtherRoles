using System;
using System.Net;
using System.Windows.Forms;

namespace BorInstaller;

/// <summary>
///     Installer of BOR-MiraAPI. Without arguments: a window to choose the game folder and the version, then installs
///     BepInEx (when its version is not the one of the release) and the plugins. With --update (started by the mod): the
///     same, for the given game folder and release, after the game closes, then starts the game again.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // GitHub and the BepInEx builds need TLS 1.2, off by default on .NET Framework.
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new InstallerForm(InstallerOptions.Parse(args)));
    }
}
