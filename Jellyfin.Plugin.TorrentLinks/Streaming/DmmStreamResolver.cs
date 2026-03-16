using System.Text.Json;
using Jellyfin.Plugin.TorrentLinks.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrentLinks.Streaming;

/// <summary>
/// Resolves stream URLs via Debrid Media Manager (DMM) Stremio addon API.
/// User must be logged in at DMM and provide their 12-char User ID (from the Stremio addon install URL).
/// See https://debridmediamanager.com
/// </summary>
public class DmmStreamResolver : IStreamResolver
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DmmStreamResolver> _logger;

    public DmmStreamResolver(HttpClient httpClient, ILogger<DmmStreamResolver> logger)
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
        if (config == null || !config.EnableStreamPlayback || !config.UseDmm
            || string.IsNullOrWhiteSpace(config.DmmBaseUrl) || string.IsNullOrWhiteSpace(config.DmmUserId))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(imdbId) || !imdbId.TrimStart().StartsWith("tt", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("DMM requires IMDb ID; none available for {Title}.", title);
            return null;
        }

        var baseUrl = config.DmmBaseUrl.TrimEnd('/');
        var userId = config.DmmUserId.Trim();
        var type = (season.HasValue && episode.HasValue) ? "series" : "movie";
        var streamId = (season.HasValue && episode.HasValue)
            ? $"{imdbId.Trim()}:{season.Value}:{episode.Value}"
            : imdbId.Trim();

        var url = $"{baseUrl}/api/stremio/{Uri.EscapeDataString(userId)}/stream/{type}/{Uri.EscapeDataString(streamId)}.json";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("Jellyfin-TorrentLinks/1.0");

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("DMM stream API returned {StatusCode} for {Imdb}.", response.StatusCode, streamId);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ParseFirstStreamUrlFromStremioJson(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DMM stream request failed for {Title} (IMDb {Imdb}).", title, imdbId);
            return null;
        }
    }

    private static string? ParseFirstStreamUrlFromStremioJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("streams", out var streams) || streams.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var item in streams.EnumerateArray())
            {
                if (item.TryGetProperty("url", out var urlProp) && urlProp.ValueKind == JsonValueKind.String)
                {
                    var url = urlProp.GetString();
                    if (!string.IsNullOrWhiteSpace(url) && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                    {
                        return url.Trim();
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
}
