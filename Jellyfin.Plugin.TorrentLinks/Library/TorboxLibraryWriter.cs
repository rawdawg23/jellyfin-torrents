using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.TorrentLinks.Configuration;
using Jellyfin.Plugin.TorrentLinks.Streaming;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrentLinks.Library;

/// <summary>
/// Writes .strm files to a configured folder so Jellyfin can add it as a library and play Torbox (or other) streams.
/// </summary>
public class TorboxLibraryWriter
{
    private static readonly Regex InvalidChars = new Regex(@"[<>:""/\\|?*]", RegexOptions.Compiled);

    private readonly IStreamResolver _streamResolver;
    private readonly ILogger<TorboxLibraryWriter> _logger;

    public TorboxLibraryWriter(IStreamResolver streamResolver, ILogger<TorboxLibraryWriter> logger)
    {
        _streamResolver = streamResolver;
        _logger = logger;
    }

    /// <summary>
    /// Resolves a stream URL and writes a .strm file (and optional .nfo) so Jellyfin can add it to the library.
    /// </summary>
    /// <param name="title">Movie or series title.</param>
    /// <param name="year">Release year if known.</param>
    /// <param name="imdbId">IMDb ID (e.g. tt1234567) if known.</param>
    /// <param name="season">Season number for TV episodes, null for movies.</param>
    /// <param name="episode">Episode number, null for movies.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Path to the created .strm file, or null if failed.</returns>
    public async Task<string?> AddToLibraryAsync(
        string title,
        int? year,
        string? imdbId,
        int? season,
        int? episode,
        CancellationToken cancellationToken)
    {
        if (Plugin.Instance?.Configuration is not PluginConfiguration config || !config.EnableStreamPlayback)
        {
            _logger.LogWarning("Stream playback disabled or no config.");
            return null;
        }

        var isEpisode = season.HasValue && episode.HasValue;
        var root = isEpisode
            ? (string.IsNullOrWhiteSpace(config.TorboxLibraryPathSeries) ? config.TorboxLibraryPath : config.TorboxLibraryPathSeries)
            : (string.IsNullOrWhiteSpace(config.TorboxLibraryPathMovies) ? config.TorboxLibraryPath : config.TorboxLibraryPathMovies);
        if (string.IsNullOrWhiteSpace(root))
        {
            _logger.LogWarning("No Torbox library path set for {Type}.", isEpisode ? "series" : "movies");
            return null;
        }

        root = root.Trim();
        try
        {
            if (!Directory.Exists(root))
            {
                Directory.CreateDirectory(root);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cannot create or access Torbox library folder: {Path}", root);
            return null;
        }

        var streamUrl = await _streamResolver.GetStreamUrlAsync(title, year, imdbId, season, episode, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(streamUrl) || !Uri.TryCreate(streamUrl, UriKind.Absolute, out _))
        {
            _logger.LogWarning("No stream URL resolved for {Title} (IMDb: {ImdbId}).", title, imdbId ?? "—");
            return null;
        }

        var safeTitle = SanitizeFileName(title);
        var yearSuffix = year.HasValue ? $" ({year.Value})" : string.Empty;
        string dirPath;
        string fileName;
        if (isEpisode)
        {
            var showFolder = safeTitle + yearSuffix;
            dirPath = Path.Combine(root, showFolder, $"Season {season!.Value:D2}");
            fileName = $"{safeTitle} - S{season.Value:D2}E{episode!.Value:D2}.strm";
        }
        else
        {
            dirPath = Path.Combine(root, safeTitle + yearSuffix);
            fileName = safeTitle + yearSuffix + ".strm";
        }

        try
        {
            Directory.CreateDirectory(dirPath);
            var strmPath = Path.Combine(dirPath, fileName);
            await File.WriteAllTextAsync(strmPath, streamUrl, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Wrote .strm: {Path}", strmPath);

            if (!string.IsNullOrWhiteSpace(imdbId))
            {
                var nfoPath = Path.ChangeExtension(strmPath, ".nfo");
                var nfoContent = isEpisode
                    ? $"<?xml version=\"1.0\"?>\n<episodedetails>\n  <imdbid>{imdbId}</imdbid>\n  <title>{System.Net.WebUtility.HtmlEncode(title)}</title>\n  <season>{season}</season>\n  <episode>{episode}</episode>\n</episodedetails>"
                    : $"<?xml version=\"1.0\"?>\n<movie>\n  <imdbid>{imdbId}</imdbid>\n  <title>{System.Net.WebUtility.HtmlEncode(title)}</title>\n  <year>{year ?? 0}</year>\n</movie>";
                await File.WriteAllTextAsync(nfoPath, nfoContent, cancellationToken).ConfigureAwait(false);
            }

            return strmPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write .strm to {Dir}", dirPath);
            return null;
        }
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Unknown";
        var s = InvalidChars.Replace(name.Trim(), "_");
        return string.IsNullOrWhiteSpace(s) ? "Unknown" : s.TrimEnd('.', ' ');
    }
}
