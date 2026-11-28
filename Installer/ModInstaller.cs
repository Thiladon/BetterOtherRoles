using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace BorInstaller;

/// <summary>
///     Installs a release in a game folder: BepInEx when its version is not the one of the release (its zip extracted
///     over the game folder: the plugins and the config are kept), then the plugins of the release in BepInEx/plugins.
/// </summary>
internal sealed class ModInstaller(string gamePath, IProgress<InstallStep> progress)
{
    /// <summary>
    ///     Gets the BepInEx version installed in a game folder, or null.
    /// </summary>
    /// <param name="gamePath">The game folder.</param>
    /// <returns>The product version of BepInEx.Core.dll.</returns>
    public static string? GetBepInExVersion(string gamePath)
    {
        var core = Path.Combine(gamePath, "BepInEx", "core", "BepInEx.Core.dll");
        return File.Exists(core) ? FileVersionInfo.GetVersionInfo(core).ProductVersion : null;
    }

    /// <summary>
    ///     Installs the release.
    /// </summary>
    /// <param name="release">The release.</param>
    /// <returns>The task.</returns>
    public async Task InstallAsync(GithubRelease release)
    {
        progress.Report(new InstallStep(Texts.ReadingRelease, 0));
        var info = await ReleaseSource.GetInfoAsync(release).ConfigureAwait(false);

        if (GetBepInExVersion(gamePath) != info.BepInExVersion)
        {
            var bepinex = await DownloadAsync(info.BepInExUrl, Texts.DownloadingBepInEx, 0, 60).ConfigureAwait(false);
            progress.Report(new InstallStep(Texts.InstallingBepInEx, 60));
            Extract(bepinex, gamePath);
            File.Delete(bepinex);
        }

        var plugins = await DownloadAsync(release.GetAsset(ReleaseSource.PluginsAsset).DownloadUrl, Texts.DownloadingPlugins, 60, 95).ConfigureAwait(false);
        progress.Report(new InstallStep(Texts.InstallingPlugins, 95));
        var pluginsFolder = Path.Combine(gamePath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsFolder);
        Extract(plugins, pluginsFolder);
        File.Delete(plugins);

        progress.Report(new InstallStep(string.Format(Texts.Culture, Texts.Done, release.Tag), 100));
    }

    // Each file of the zip written over the one there (folders made as needed).
    private static void Extract(string zipPath, string target)
    {
        var root = Path.GetFullPath(target);
        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries)
        {
            var path = Path.GetFullPath(Path.Combine(root, entry.FullName));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"{entry.FullName} is out of the target folder.");

            if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
            {
                Directory.CreateDirectory(path);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            entry.ExtractToFile(path, true);
        }
    }

    // Downloads to a temporary file, the progress going from one percent to the other.
    private async Task<string> DownloadAsync(string url, string text, int from, int to)
    {
        var file = Path.Combine(Path.GetTempPath(), "BOR-MiraAPI-" + Path.GetRandomFileName());
        using var client = ReleaseSource.CreateClient();
        client.DownloadProgressChanged += (_, e) => progress.Report(new InstallStep(text, from + ((to - from) * e.ProgressPercentage / 100)));
        progress.Report(new InstallStep(text, from));
        await client.DownloadFileTaskAsync(new Uri(url), file).ConfigureAwait(false);
        return file;
    }
}

/// <summary>
///     A step of the installation: its text and the progress, in percent.
/// </summary>
/// <param name="text">The text.</param>
/// <param name="percent">The progress.</param>
internal sealed class InstallStep(string text, int percent)
{
    /// <summary>
    ///     Gets the text.
    /// </summary>
    public string Text { get; } = text;

    /// <summary>
    ///     Gets the progress, in percent.
    /// </summary>
    public int Percent { get; } = percent;
}
