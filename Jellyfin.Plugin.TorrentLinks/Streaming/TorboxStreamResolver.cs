using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Jellyfin.Plugin.TorrentLinks.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrentLinks.Streaming;

/// <summary>
/// Resolves stream URLs via the Torbox debrid API (search by IMDB → add torrent → request download link).
/// Requires a Torbox account and API key from https://torbox.app
/// </summary>
public class TorboxStreamResolver : IStreamResolver
{
    private const string ApiBase = "https://api.torbox.app";
    private const string SearchApiBase = "https://search-api.torbox.app";
    private const string DefaultApiVersion = "v1";

    private readonly HttpClient _httpClient;
    private readonly ILogger<TorboxStreamResolver> _logger;

    public TorboxStreamResolver(HttpClient httpClient, ILogger<TorboxStreamResolver> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string?> GetStreamUrlAsync(
        string title,
        int? year,
        string? imdbId,
        int? season,
        int? episode,
        CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration as PluginConfiguration;
        if (config == null || !config.EnableStreamPlayback || !config.UseTorbox || string.IsNullOrWhiteSpace(config.TorboxApiKey))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(imdbId) || !imdbId.TrimStart().StartsWith("tt", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Torbox requires IMDb ID for search; none available for {Title}.", title);
            return null;
        }

        var apiVersion = string.IsNullOrWhiteSpace(config.TorboxApiVersion) ? DefaultApiVersion : config.TorboxApiVersion.Trim();
        var key = config.TorboxApiKey.Trim();

        try
        {
            var magnet = await GetMagnetFromSearchAsync(imdbId.Trim(), key, season, episode, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(magnet))
            {
                return null;
            }

            var hash = ExtractHashFromMagnet(magnet);
            if (string.IsNullOrEmpty(hash))
            {
                return null;
            }

            var torrentId = await EnsureTorrentAddedAsync(magnet, hash, key, apiVersion, cancellationToken).ConfigureAwait(false);
            if (torrentId == null)
            {
                return null;
            }

            var isEpisode = season.HasValue && episode.HasValue;
            var fileId = await GetBestVideoFileIdAsync(torrentId.Value, key, apiVersion, isEpisode, cancellationToken).ConfigureAwait(false);
            if (fileId == null)
            {
                return null;
            }

            return await RequestDownloadLinkAsync(torrentId.Value, fileId.Value, key, apiVersion, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Torbox stream resolution failed for {Title} (IMDb {Imdb}).", title, imdbId);
            return null;
        }
    }

    private async Task<string?> GetMagnetFromSearchAsync(string imdbId, string apiKey, int? season, int? episode, CancellationToken ct)
    {
        var source = "imdb:" + imdbId.Trim();
        if (season.HasValue && episode.HasValue)
        {
            source += ":S" + season.Value + "E" + episode.Value;
        }

        var url = $"{SearchApiBase}/torrents/{Uri.EscapeDataString(source)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.UserAgent.ParseAdd("Jellyfin-TorrentLinks/1.0");

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogDebug("Torbox search returned {StatusCode} for {Source}.", response.StatusCode, source);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return ParseFirstMagnetFromSearchJson(json);
    }

    private static string? ParseFirstMagnetFromSearchJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            JsonElement array = default;
            if (root.ValueKind == JsonValueKind.Array)
            {
                array = root;
            }
            else if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                array = data;
            }
            else if (root.TryGetProperty("torrents", out var torrents) && torrents.ValueKind == JsonValueKind.Array)
            {
                array = torrents;
            }
            else
            {
                return null;
            }

            foreach (var item in array.EnumerateArray())
            {
                if (item.TryGetProperty("magnet", out var m) && m.ValueKind == JsonValueKind.String)
                {
                    var magnet = m.GetString();
                    if (!string.IsNullOrWhiteSpace(magnet))
                    {
                        return magnet;
                    }
                }

                if (item.TryGetProperty("hash", out var h) && h.ValueKind == JsonValueKind.String)
                {
                    var hash = h.GetString();
                    if (!string.IsNullOrWhiteSpace(hash))
                    {
                        return "magnet:?xt=urn:btih:" + hash.Trim();
                    }
                }
            }
        }
        catch
        {
            // ignore parse errors
        }

        return null;
    }

    private static string? ExtractHashFromMagnet(string magnet)
    {
        if (string.IsNullOrWhiteSpace(magnet))
        {
            return null;
        }

        var idx = magnet.IndexOf("btih:", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return null;
        }

        idx += 5;
        var end = magnet.IndexOf('&', idx);
        var hash = end < 0 ? magnet[idx..].Trim() : magnet[idx..end].Trim();
        return hash.Length > 0 ? hash : null;
    }

    private async Task<int?> EnsureTorrentAddedAsync(string magnet, string hash, string apiKey, string apiVersion, CancellationToken ct)
    {
        using var checkRequest = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/{apiVersion}/api/torrents/checkcached?hash={Uri.EscapeDataString(hash)}&format=list");
        checkRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        checkRequest.Headers.UserAgent.ParseAdd("Jellyfin-TorrentLinks/1.0");

        using var checkResponse = await _httpClient.SendAsync(checkRequest, ct).ConfigureAwait(false);
        if (checkResponse.IsSuccessStatusCode)
        {
            var checkJson = await checkResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var cachedId = ParseCachedTorrentId(checkJson, hash);
            if (cachedId != null)
            {
                return cachedId;
            }
        }

        var body = new { magnet };
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, $"{ApiBase}/{apiVersion}/api/torrents/createtorrent") { Content = content };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        createRequest.Headers.UserAgent.ParseAdd("Jellyfin-TorrentLinks/1.0");

        using var createResponse = await _httpClient.SendAsync(createRequest, ct).ConfigureAwait(false);
        if (!createResponse.IsSuccessStatusCode)
        {
            _logger.LogDebug("Torbox createtorrent returned {StatusCode}.", createResponse.StatusCode);
            return null;
        }

        var createJson = await createResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var torrentId = ParseCreateTorrentId(createJson);
        if (torrentId != null)
        {
            return torrentId;
        }

        await Task.Delay(2000, ct).ConfigureAwait(false);
        using var listRequest = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/{apiVersion}/api/torrents/mylist?bypassCache=true");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        listRequest.Headers.UserAgent.ParseAdd("Jellyfin-TorrentLinks/1.0");
        using var listResponse = await _httpClient.SendAsync(listRequest, ct).ConfigureAwait(false);
        if (!listResponse.IsSuccessStatusCode)
        {
            return null;
        }

        var listJson = await listResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return FindTorrentIdByHash(listJson, hash);
    }

    private static int? ParseCachedTorrentId(string json, string hash)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("data", out var data))
            {
                if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty(hash, out var entry))
                {
                    if (entry.TryGetProperty("torrent_id", out var tid))
                    {
                        return tid.GetInt32();
                    }

                    if (entry.TryGetProperty("torrentId", out var tid2))
                    {
                        return tid2.GetInt32();
                    }
                }

                if (data.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in data.EnumerateArray())
                    {
                        if (item.TryGetProperty("hash", out var h) && h.GetString() == hash && item.TryGetProperty("id", out var id))
                        {
                            return id.GetInt32();
                        }
                    }
                }
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static int? ParseCreateTorrentId(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("success", out var ok) && !ok.GetBoolean())
            {
                return null;
            }

            if (root.TryGetProperty("data", out var data))
            {
                if (data.TryGetProperty("torrentId", out var tid))
                {
                    return tid.GetInt32();
                }

                if (data.TryGetProperty("torrent_id", out var tid2))
                {
                    return tid2.GetInt32();
                }
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static int? FindTorrentIdByHash(string json, string hash)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var item in data.EnumerateArray())
            {
                if (item.TryGetProperty("hash", out var h) && string.Equals(h.GetString(), hash, StringComparison.OrdinalIgnoreCase) && item.TryGetProperty("id", out var id))
                {
                    return id.GetInt32();
                }
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private async Task<int?> GetBestVideoFileIdAsync(int torrentId, string apiKey, string apiVersion, bool isEpisode, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/{apiVersion}/api/torrents/mylist?bypassCache=true&id={torrentId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.UserAgent.ParseAdd("Jellyfin-TorrentLinks/1.0");

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        int? maxResolutionPx = null;
        long? maxSizeBytes = null;
        if (Plugin.Instance?.Configuration is PluginConfiguration cfg)
        {
            if (!string.IsNullOrWhiteSpace(cfg.MaxResolution) && int.TryParse(cfg.MaxResolution.Trim(), out var maxPx))
            {
                maxResolutionPx = maxPx;
            }

            var maxGb = isEpisode ? cfg.MaxEpisodeSizeGb : cfg.MaxMovieSizeGb;
            if (maxGb > 0)
            {
                maxSizeBytes = (long)(maxGb * 1024 * 1024 * 1024);
            }
        }

        return ParseBestVideoFileId(json, maxResolutionPx, maxSizeBytes);
    }

    private static readonly string[] VideoMimePrefixes = { "video/", "application/x-mpegURL" };

    private static int? ParseBestVideoFileId(string json, int? maxResolutionPx, long? maxSizeBytes)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            JsonElement? filesParent = null;
            if (root.TryGetProperty("data", out var data))
            {
                if (data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
                {
                    filesParent = data[0];
                }
                else if (data.ValueKind == JsonValueKind.Object)
                {
                    filesParent = data;
                }
            }

            if (filesParent == null || !filesParent.Value.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            long bestSize = 0;
            int? bestId = null;
            foreach (var f in files.EnumerateArray())
            {
                if (!f.TryGetProperty("id", out var idProp))
                {
                    continue;
                }

                var id = idProp.GetInt32();
                var size = f.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                var mime = f.TryGetProperty("mimetype", out var m) ? m.GetString() ?? "" : "";
                var name = f.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";

                var isVideo = VideoMimePrefixes.Any(p => mime.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                    || name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".webm", StringComparison.OrdinalIgnoreCase);

                if (!isVideo)
                {
                    continue;
                }

                if (maxSizeBytes.HasValue && size > maxSizeBytes.Value)
                {
                    continue;
                }

                var resPx = ParseResolutionFromFileName(name);
                if (maxResolutionPx.HasValue && resPx.HasValue && resPx.Value > maxResolutionPx.Value)
                {
                    continue;
                }

                if (size > bestSize)
                {
                    bestSize = size;
                    bestId = id;
                }
            }

            return bestId ?? (files.GetArrayLength() > 0 && files[0].TryGetProperty("id", out var firstId) ? firstId.GetInt32() : (int?)null);
        }
        catch
        {
            return null;
        }
    }

    private static int? ParseResolutionFromFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var lower = name.ToLowerInvariant();
        if (lower.Contains("2160") || lower.Contains("4k") || lower.Contains("4kuhd"))
        {
            return 2160;
        }

        if (lower.Contains("1080"))
        {
            return 1080;
        }

        if (lower.Contains("720"))
        {
            return 720;
        }

        if (lower.Contains("480"))
        {
            return 480;
        }

        return null;
    }

    private async Task<string?> RequestDownloadLinkAsync(int torrentId, int fileId, string apiKey, string apiVersion, CancellationToken ct)
    {
        var url = $"{ApiBase}/{apiVersion}/api/torrents/requestdl?token={Uri.EscapeDataString(apiKey)}&torrentId={torrentId}&fileId={fileId}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("Jellyfin-TorrentLinks/1.0");

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("success", out var ok) && !ok.GetBoolean())
            {
                return null;
            }

            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.String)
            {
                var link = data.GetString();
                return string.IsNullOrWhiteSpace(link) ? null : link.Trim();
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }
}
