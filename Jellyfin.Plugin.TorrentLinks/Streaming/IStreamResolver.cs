namespace Jellyfin.Plugin.TorrentLinks.Streaming;

/// <summary>
/// Resolves a playable stream URL for a movie or episode.
/// </summary>
public interface IStreamResolver
{
    /// <summary>
    /// Tries to get a direct stream URL for the given title/year/IMDb.
    /// </summary>
    /// <param name="title">Movie or episode title.</param>
    /// <param name="year">Release year if known.</param>
    /// <param name="imdbId">IMDb ID (e.g. tt1234567) if known.</param>
    /// <param name="season">Season number for episodes, null for movies.</param>
    /// <param name="episode">Episode number, null for movies.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Stream URL if found, null otherwise.</returns>
    Task<string?> GetStreamUrlAsync(
        string title,
        int? year,
        string? imdbId,
        int? season,
        int? episode,
        CancellationToken cancellationToken);
}
