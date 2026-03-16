using System.Net.Http.Headers;
using System.Text.Json;
using Jellyfin.Plugin.TorrentLinks.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrentLinks.Streaming;

/// <summary>
/// Resolves stream URLs by calling a configurable HTTP API.
/// API URL can use placeholders: {title}, {year}, {imdb}, {season}, {episode}.
/// Expected response: JSON with "url" or "streamUrl" property containing the playable URL.
/// </summary>
public class ApiStreamResolver : IStreamResolver
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiStreamResolver> _logger;

    public ApiStreamResolver(HttpClient httpClient, ILogger<ApiStreamResolver> logger)
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
        if (config == null || !config.EnableStreamPlayback || string.IsNullOrWhiteSpace(config.StreamApiUrl))
        {
            return null;
        }

        var url = config.StreamApiUrl
            .Replace("{title}", Uri.EscapeDataString(title), StringComparison.OrdinalIgnoreCase)
            .Replace("{year}", year?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{imdb}", imdbId ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{season}", season?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{episode}", episode?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            _logger.LogWarning("Invalid Stream API URL configured.");
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("Jellyfin-TorrentLinks/1.0");
            if (!string.IsNullOrWhiteSpace(config.StreamApiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.StreamApiKey.Trim());
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ParseStreamUrlFromJson(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stream resolver API request failed for {Title}", title);
            return null;
        }
    }

    private static string? ParseStreamUrlFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("url", out var urlProp) && urlProp.ValueKind == JsonValueKind.String)
            {
                return urlProp.GetString();
            }

            if (root.TryGetProperty("streamUrl", out var streamUrlProp) && streamUrlProp.ValueKind == JsonValueKind.String)
            {
                return streamUrlProp.GetString();
            }

            if (root.TryGetProperty("stream_url", out var streamUrlSnake) && streamUrlSnake.ValueKind == JsonValueKind.String)
            {
                return streamUrlSnake.GetString();
            }

            if (root.ValueKind == JsonValueKind.String)
            {
                return root.GetString();
            }
        }
        catch
        {
            // Not JSON or wrong shape
        }

        return null;
    }
}
