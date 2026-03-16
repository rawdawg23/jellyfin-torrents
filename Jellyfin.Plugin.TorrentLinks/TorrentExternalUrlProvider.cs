using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.TorrentLinks.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrentLinks;

/// <summary>
/// Provides external URLs to search for torrents/streams for movies and series.
/// </summary>
public class TorrentExternalUrlProvider : IExternalUrlProvider
{
    private readonly ILogger<TorrentExternalUrlProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TorrentExternalUrlProvider"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public TorrentExternalUrlProvider(ILogger<TorrentExternalUrlProvider> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Torrent & Stream Links";

    /// <inheritdoc />
    public IEnumerable<string> GetExternalUrls(BaseItem item)
    {
        if (Plugin.Instance?.Configuration is not PluginConfiguration config || !config.Enabled)
        {
            return Enumerable.Empty<string>();
        }

        var searchQuery = GetSearchQuery(item);
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            return Enumerable.Empty<string>();
        }

        var encodedQuery = Uri.EscapeDataString(searchQuery);
        var year = GetYear(item);
        var urls = new List<string>();

        foreach (var site in config.Sites.Where(s => s.Enabled && !string.IsNullOrWhiteSpace(s.Name) && !string.IsNullOrWhiteSpace(s.UrlFormat)))
        {
            try
            {
                var url = BuildUrl(site.UrlFormat, encodedQuery, searchQuery, year);
                if (!string.IsNullOrEmpty(url))
                {
                    urls.Add(url);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to build URL for site {Name}", site.Name);
            }
        }

        return urls;
    }

    private static string? GetSearchQuery(BaseItem item)
    {
        return item.Name;
    }

    private static string? GetYear(BaseItem item)
    {
        if (item.PremiereDate.HasValue)
        {
            return item.PremiereDate.Value.Year.ToString();
        }

        if (item.ProductionYear.HasValue)
        {
            return item.ProductionYear.Value.ToString();
        }

        return null;
    }

    private static string BuildUrl(string format, string encodedQuery, string rawQuery, string? year)
    {
        // Supports placeholders: {query}, {encoded}, {year}
        var url = format
            .Replace("{query}", rawQuery, StringComparison.OrdinalIgnoreCase)
            .Replace("{encoded}", encodedQuery, StringComparison.OrdinalIgnoreCase)
            .Replace("{year}", year ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        return url.Trim();
    }
}
