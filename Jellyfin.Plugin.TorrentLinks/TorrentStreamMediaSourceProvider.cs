using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TorrentLinks.Configuration;
using Jellyfin.Plugin.TorrentLinks.Streaming;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrentLinks;

/// <summary>
/// Adds playable stream sources for movies and episodes by resolving stream URLs
/// via the configured stream resolver (e.g. external API).
/// </summary>
public class TorrentStreamMediaSourceProvider : IMediaSourceProvider
{
    private readonly IStreamResolver _streamResolver;
    private readonly ILogger<TorrentStreamMediaSourceProvider> _logger;

    public TorrentStreamMediaSourceProvider(
        IStreamResolver streamResolver,
        ILogger<TorrentStreamMediaSourceProvider> logger)
    {
        _streamResolver = streamResolver;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Torrent & Stream Links";

    /// <inheritdoc />
    public Task<IEnumerable<MediaSourceInfo>> GetMediaSources(BaseItem item, CancellationToken cancellationToken)
    {
        var sources = new List<MediaSourceInfo>();

        if (Plugin.Instance?.Configuration is not PluginConfiguration config || !config.EnableStreamPlayback)
        {
            return Task.FromResult<IEnumerable<MediaSourceInfo>>(sources);
        }

        string title;
        int? year = null;
        string? imdbId = null;
        int? season = null;
        int? episode = null;

        if (item is Movie movie)
        {
            title = movie.Name ?? movie.OriginalTitle ?? string.Empty;
            year = movie.ProductionYear ?? movie.PremiereDate?.Year;
            if (movie.ProviderIds?.TryGetValue("Imdb", out var id) == true)
            {
                imdbId = id;
            }
        }
        else if (item is Episode ep)
        {
            title = ep.SeriesName ?? ep.Name ?? string.Empty;
            if (ep.PremiereDate.HasValue)
            {
                title += " " + ep.PremiereDate.Value.Year;
            }

            year = ep.PremiereDate?.Year ?? ep.ProductionYear;
            season = ep.ParentIndexNumber;
            episode = ep.IndexNumber;
            if (ep.Series?.ProviderIds?.TryGetValue("Imdb", out var sid) == true)
            {
                imdbId = sid;
            }
        }
        else
        {
            return Task.FromResult<IEnumerable<MediaSourceInfo>>(sources);
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return Task.FromResult<IEnumerable<MediaSourceInfo>>(sources);
        }

        return GetMediaSourcesAsync();

        async Task<IEnumerable<MediaSourceInfo>> GetMediaSourcesAsync()
        {
            var streamUrl = await _streamResolver.GetStreamUrlAsync(title, year, imdbId, season, episode, cancellationToken)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(streamUrl) || !Uri.TryCreate(streamUrl, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                return sources;
            }

            var sourceId = "torrentlinks-stream-" + item.Id.ToString("N", System.Globalization.CultureInfo.InvariantCulture);
            var mediaSource = new MediaSourceInfo
            {
                Id = sourceId,
                Path = streamUrl,
                Protocol = MediaProtocol.Http,
                SupportsDirectPlay = true,
                SupportsDirectStream = true,
                SupportsTranscoding = false,
                Container = GetContainerFromUrl(streamUrl),
                Name = "Stream (Torrent Links)"
            };

            sources.Add(mediaSource);
            _logger.LogDebug("Added stream source for {Name}: {Url}", item.Name, streamUrl);
            return sources;
        }
    }

    /// <inheritdoc />
    public Task<IDirectStreamProvider?> GetDirectStreamProviderByUniqueId(string uniqueId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IDirectStreamProvider?>(null);
    }

    /// <inheritdoc />
    public Task<ILiveStream> OpenMediaSource(string openToken, List<ILiveStream> currentLiveStreams, CancellationToken cancellationToken)
    {
        return Task.FromException<ILiveStream>(new NotSupportedException("Opening media source by token is not supported by this provider."));
    }

    private static string GetContainerFromUrl(string url)
    {
        if (url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase))
        {
            return "m3u8";
        }

        if (url.Contains(".mp4", StringComparison.OrdinalIgnoreCase) || url.Contains("mp4?", StringComparison.OrdinalIgnoreCase))
        {
            return "mp4";
        }

        return "mp4";
    }
}
