using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace BorInstaller;

/// <summary>
///     Releases of BOR-MiraAPI on GitHub: those of the repository with the mod DLL (the older releases of
///     BetterOtherRoles have another one), newest first.
/// </summary>
internal static class ReleaseSource
{
    /// <summary>
    ///     Name of the mod DLL, the mark of a release of BOR-MiraAPI.
    /// </summary>
    public const string ModAsset = "BetterOtherRolesMiraAPI.dll";

    /// <summary>
    ///     Name of the zip of the plugins (the mod and its dependencies).
    /// </summary>
    public const string PluginsAsset = "BOR-MiraAPI-plugins.zip";

    /// <summary>
    ///     Name of the release information (BepInEx version and download address).
    /// </summary>
    public const string InfoAsset = "release.json";

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S1075:URIs should not be hardcoded", Justification = "Public repository of the releases.")]
    private const string ReleasesUrl ="https://api.github.com/repos/Thiladon/BetterOtherRoles/releases?per_page=100";

    /// <summary>
    ///     Gets the releases of BOR-MiraAPI, newest first.
    /// </summary>
    /// <returns>The releases.</returns>
    public static async Task<List<GithubRelease>> GetReleasesAsync()
    {
        var json = await DownloadStringAsync(ReleasesUrl).ConfigureAwait(false);
        var releases = Deserialize<List<GithubRelease>>(json) ?? [];
        return releases.Where(x => !x.Draft && x.Assets.Any(a => a.Name == ModAsset)).ToList();
    }

    /// <summary>
    ///     Gets the information of a release (release.json).
    /// </summary>
    /// <param name="release">The release.</param>
    /// <returns>The information.</returns>
    public static async Task<ReleaseInfo> GetInfoAsync(GithubRelease release)
    {
        var asset = release.Assets.FirstOrDefault(x => x.Name == InfoAsset)
                    ?? throw new InvalidOperationException($"{InfoAsset} is missing from {release.Tag}.");
        var json = await DownloadStringAsync(asset.DownloadUrl).ConfigureAwait(false);
        return Deserialize<ReleaseInfo>(json) ?? throw new InvalidOperationException($"{InfoAsset} of {release.Tag} cannot be read.");
    }

    /// <summary>
    ///     Makes a web client with the user agent GitHub asks for.
    /// </summary>
    /// <returns>The client.</returns>
    public static WebClient CreateClient()
    {
        var client = new WebClient { Encoding = Encoding.UTF8 };
        client.Headers[HttpRequestHeader.UserAgent] = "BOR-MiraAPI-Installer";
        return client;
    }

    private static async Task<string> DownloadStringAsync(string url)
    {
        using var client = CreateClient();
        return await client.DownloadStringTaskAsync(new Uri(url)).ConfigureAwait(false);
    }

    private static T? Deserialize<T>(string json)
        where T : class
    {
        var serializer = new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return serializer.ReadObject(stream) as T;
    }
}

/// <summary>
///     A release on GitHub.
/// </summary>
[DataContract]
internal sealed class GithubRelease
{
    /// <summary>
    ///     Gets or sets the tag (version).
    /// </summary>
    [DataMember(Name = "tag_name")]
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets a value indicating whether it is a draft.
    /// </summary>
    [DataMember(Name = "draft")]
    public bool Draft { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether it is a beta.
    /// </summary>
    [DataMember(Name = "prerelease")]
    public bool Prerelease { get; set; }

    /// <summary>
    ///     Gets or sets the files of the release.
    /// </summary>
    [DataMember(Name = "assets")]
    public List<GithubAsset> Assets { get; set; } = [];

    /// <inheritdoc/>
    public override string ToString() => Tag;

    /// <summary>
    ///     Gets the file of the release with this name.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The file.</returns>
    public GithubAsset GetAsset(string name) =>
        Assets.FirstOrDefault(x => x.Name == name) ?? throw new InvalidOperationException($"{name} is missing from {Tag}.");
}

/// <summary>
///     A file of a release.
/// </summary>
[DataContract]
internal sealed class GithubAsset
{
    /// <summary>
    ///     Gets or sets the file name.
    /// </summary>
    [DataMember(Name = "name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the download address.
    /// </summary>
    [DataMember(Name = "browser_download_url")]
    public string DownloadUrl { get; set; } = string.Empty;
}

/// <summary>
///     Information of a release (release.json): the BepInEx it needs.
/// </summary>
[DataContract]
internal sealed class ReleaseInfo
{
    /// <summary>
    ///     Gets or sets the BepInEx version (product version of BepInEx.Core.dll, "6.0.0-be.735+5fef357...").
    /// </summary>
    [DataMember(Name = "bepinexVersion")]
    public string BepInExVersion { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the download address of the BepInEx zip.
    /// </summary>
    [DataMember(Name = "bepinexUrl")]
    public string BepInExUrl { get; set; } = string.Empty;
}
