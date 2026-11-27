using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BepInEx.Configuration;
using BetterOtherRoles.Options;
using MiraAPI.GameOptions;

namespace BetterOtherRoles.Modules.Summary;

/// <summary>
///     Sends the AmongStat file of the lobby to the AmongStat server (a Cloudflare Worker, see AmongStatWorker/ in the
///     repository) after each game, when the host allows it with the lobby option "Send Stats Online" (off by
///     default, seen by every player). The address of the server and the shared host code are built into the DLL from
///     the .env file (<see cref="AmongStatBuildSecrets"/>, AmongStat.targets); a host may put its own address or code in
///     the config file of BepInEx instead. The whole file is sent: the server only adds the games it does not have
///     yet, so a game that could not be sent goes with the next one. Off the main thread; the file stays in AmongStat/
///     on the host's computer either way.
/// </summary>
public static class BorAmongStatUpload
{
    private const string Section = "AmongStat";

    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };

    private static readonly string BuiltInServer = Unmask(AmongStatBuildSecrets.Server);
    private static readonly string BuiltInCode = Unmask(AmongStatBuildSecrets.Code);

    private static ConfigEntry<string>? _serverUrl;
    private static ConfigEntry<string>? _hostCode;

    /// <summary>
    ///     Reads the address of the server and the host code of the config file, when the host has its own.
    /// </summary>
    public static void Initialize(ConfigFile config)
    {
        _serverUrl = config.Bind(Section, "Server", string.Empty,
            "Address of the AmongStat server; empty: the one of the mod.");
        _hostCode = config.Bind(Section, "HostCode", string.Empty,
            "Own host code given by the owner of the AmongStat server; empty: the one of the mod.");
    }

    /// <summary>
    ///     Sends the file of the lobby, when the lobby option allows it and there is a server and a host code.
    /// </summary>
    public static void Send(string fileName, AmongStatFile file, JsonSerializerOptions options)
    {
        if (!OptionGroupSingleton<LobbyOptions>.Instance.SendStatsOnline.Value) return;

        var url = OwnOrBuiltIn(_serverUrl, BuiltInServer);
        var code = OwnOrBuiltIn(_hostCode, BuiltInCode);
        if (url.Length == 0 || code.Length == 0)
        {
            Warning("AmongStat: no server or host code in this build (.env), the statistics stay on this computer.");
            return;
        }

        // Made now: the file changes with the next game.
        var body = JsonSerializer.Serialize(new AmongStatUpload { FileName = fileName, File = file }, options);
        _ = Task.Run(() => SendAsync(url, code, body, fileName));
    }

    private static string OwnOrBuiltIn(ConfigEntry<string>? own, string builtIn)
    {
        var value = own?.Value.Trim();
        return string.IsNullOrEmpty(value) ? builtIn : value;
    }

    private static string Unmask(byte[] data)
    {
        var key = AmongStatBuildSecrets.Key;
        var bytes = new byte[data.Length];
        for (var i = 0; i < data.Length; i++)
            bytes[i] = (byte)(data[i] ^ key[i % key.Length]);
        return Encoding.UTF8.GetString(bytes);
    }

    private static async Task SendAsync(string url, string code, string body, string fileName)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", code);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var response = await Client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
                Info($"AmongStat: {fileName} sent to the server ({text}).");
            else
                Warning($"AmongStat: the server refused {fileName}: {(int)response.StatusCode} {text}");
        }
        catch (Exception e)
        {
            Warning($"AmongStat: {fileName} could not be sent, it will go with the next game: {e.Message}");
        }
    }
}

/// <summary>
///     What is sent to the AmongStat server: the name of the file and its content.
/// </summary>
public sealed class AmongStatUpload
{
    public string FileName { get; set; } = string.Empty;

    public AmongStatFile File { get; set; } = new();
}
