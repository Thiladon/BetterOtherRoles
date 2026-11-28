using System.Collections.Generic;
using System.Globalization;

namespace BorInstaller;

/// <summary>
///     Arguments of the installer: "--update --game &lt;folder&gt; --release &lt;tag&gt; [--wait-pid &lt;id&gt;] [--restart]".
/// </summary>
internal sealed class InstallerOptions
{
    /// <summary>
    ///     Gets a value indicating whether the installer was started by the mod to update it.
    /// </summary>
    public bool Update { get; private set; }

    /// <summary>
    ///     Gets the game folder given, or null.
    /// </summary>
    public string? GamePath { get; private set; }

    /// <summary>
    ///     Gets the tag of the release to install, or null.
    /// </summary>
    public string? ReleaseTag { get; private set; }

    /// <summary>
    ///     Gets the id of the game process to wait for, or null.
    /// </summary>
    public int? WaitPid { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether the game starts again at the end.
    /// </summary>
    public bool Restart { get; private set; }

    /// <summary>
    ///     Reads the arguments.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The options.</returns>
    public static InstallerOptions Parse(string[] args)
    {
        var options = new InstallerOptions();
        var queue = new Queue<string>(args);
        while (queue.Count > 0)
        {
            var arg = queue.Dequeue();
            switch (arg)
            {
                case "--update":
                    options.Update = true;
                    break;
                case "--restart":
                    options.Restart = true;
                    break;
                case "--game" when queue.Count > 0:
                    options.GamePath = queue.Dequeue();
                    break;
                case "--release" when queue.Count > 0:
                    options.ReleaseTag = queue.Dequeue();
                    break;
                case "--wait-pid" when queue.Count > 0:
                    if (int.TryParse(queue.Dequeue(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
                        options.WaitPid = pid;
                    break;
                default:
                    break;
            }
        }

        return options;
    }
}
