using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BetterOtherRoles.Modules.Updater;

/// <summary>
///     Releases of BOR-MiraAPI on GitHub, as the installer reads them (Installer/ReleaseSource): the releases of the
///     repository with the mod DLL (the older releases of BetterOtherRoles have another one), each with its
///     release.json (BepInEx and version of Among Us).
/// </summary>
public static class BorReleases
{
    /// <summary>
    ///     Name of the mod DLL, the mark of a release of BOR-MiraAPI.
    /// </summary>
    public const string ModAsset = "BetterOtherRolesMiraAPI.dll";

    /// <summary>
    ///     Name of the zip of the plugins (the mod and its dependencies).
    /// </summary>
    public const string PluginsAsset = "BOR-MiraAPI-plugins.zip";

    private const string InfoAsset = "release.json";

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S1075:URIs should not be hardcoded", Justification = "Public repository of the releases.")]
    private const string ReleasesUrl = "https://api.github.com/repos/Thiladon/BetterOtherRoles/releases?per_page=100";

    /// <summary>
    ///     Gets the releases of BOR-MiraAPI with their information, newest first (those without a readable
    ///     release.json left out).
    /// </summary>
    /// <returns>The releases.</returns>
    public static async Task<List<BorRelease>> GetAsync()
    {
        using var client = CreateClient();
        var json = await client.GetStringAsync(new Uri(ReleasesUrl)).ConfigureAwait(false);
        var releases = (JsonSerializer.Deserialize<List<GithubRelease>>(json) ?? [])
            .Where(x => !x.Draft && x.Assets.Exists(a => a.Name == ModAsset))
            .ToList();

        var infos = await Task.WhenAll(releases.Select(x => ReadInfoAsync(client, x))).ConfigureAwait(false);
        return releases.Zip(infos, (release, info) => info == null ? null : new BorRelease(release, info))
            .OfType<BorRelease>()
            .ToList();
    }

    /// <summary>
    ///     Makes an HTTP client with the user agent GitHub asks for.
    /// </summary>
    /// <returns>The client.</returns>
    public static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("BOR-MiraAPI-Updater");
        return client;
    }

    /// <summary>
    ///     Compares two versions of the mod ("0.0.1-alpha", "v1.0.0"): the numbers, then a release after its betas, then
    ///     the beta names in order.
    /// </summary>
    /// <param name="a">A version.</param>
    /// <param name="b">The other version.</param>
    /// <returns>Below 0 when a is older, 0 when the same, above 0 when newer.</returns>
    public static int Compare(string a, string b)
    {
        var (numbersA, suffixA) = Split(a);
        var (numbersB, suffixB) = Split(b);
        var numbers = numbersA.CompareTo(numbersB);
        if (numbers != 0) return numbers;
        if (suffixA.Length == 0 || suffixB.Length == 0) return suffixB.Length.CompareTo(suffixA.Length);

        return string.CompareOrdinal(suffixA, suffixB);
    }

    private static (Version Numbers, string Suffix) Split(string version)
    {
        var text = version.TrimStart('v', 'V');
        var dash = text.IndexOf('-', StringComparison.Ordinal);
        var numbers = dash < 0 ? text : text[..dash];
        var suffix = dash < 0 ? string.Empty : text[(dash + 1)..];

        // Without the build metadata ("+commit").
        var plus = suffix.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
            suffix = suffix[..plus];
        plus = numbers.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
            numbers = numbers[..plus];

        return (Version.TryParse(numbers, out var parsed) ? parsed : new Version(0, 0), suffix.ToLower(CultureInfo.InvariantCulture));
    }

    private static async Task<ReleaseInfo?> ReadInfoAsync(HttpClient client, GithubRelease release)
    {
        var asset = release.Assets.Find(x => x.Name == InfoAsset);
        if (asset == null) return null;

        try
        {
            var json = await client.GetStringAsync(new Uri(asset.DownloadUrl)).ConfigureAwait(false);
            return JsonSerializer.Deserialize<ReleaseInfo>(json);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException)
        {
            Warning($"[Updater] {InfoAsset} of {release.Tag} cannot be read: {e.Message}");
            return null;
        }
    }
}

/// <summary>
///     A release of BOR-MiraAPI with its information.
/// </summary>
/// <param name="Github">The release on GitHub.</param>
/// <param name="Info">Its release.json.</param>
public sealed record BorRelease(GithubRelease Github, ReleaseInfo Info)
{
    /// <summary>
    ///     Gets the version, without the "v" of the tag.
    /// </summary>
    public string Version => Github.Tag.TrimStart('v', 'V');

    /// <summary>
    ///     Gets the address of a file of the release, or null.
    /// </summary>
    /// <param name="name">The name of the file.</param>
    /// <returns>The address.</returns>
    public string? GetUrl(string name) => Github.Assets.Find(x => x.Name == name)?.DownloadUrl;
}

/// <summary>
///     A release on GitHub.
/// </summary>
public sealed class GithubRelease
{
    /// <summary>
    ///     Gets or sets the tag.
    /// </summary>
    [JsonPropertyName("tag_name")]
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets a value indicating whether it is a draft.
    /// </summary>
    [JsonPropertyName("draft")]
    public bool Draft { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether it is a beta.
    /// </summary>
    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; set; }

    /// <summary>
    ///     Gets or sets the files.
    /// </summary>
    [JsonPropertyName("assets")]
    public List<GithubAsset> Assets { get; set; } = [];
}

/// <summary>
///     A file of a release.
/// </summary>
public sealed class GithubAsset
{
    /// <summary>
    ///     Gets or sets the name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the download address.
    /// </summary>
    [JsonPropertyName("browser_download_url")]
    public string DownloadUrl { get; set; } = string.Empty;
}

/// <summary>
///     Information of a release (release.json, tools/Prepare-Release.ps1).
/// </summary>
public sealed class ReleaseInfo
{
    /// <summary>
    ///     Gets or sets the BepInEx version (product version of BepInEx.Core.dll).
    /// </summary>
    [JsonPropertyName("bepinexVersion")]
    public string BepInExVersion { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the version of Among Us the release is made for.
    /// </summary>
    [JsonPropertyName("gameVersion")]
    public string GameVersion { get; set; } = string.Empty;
}
