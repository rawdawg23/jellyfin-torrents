namespace Jellyfin.Plugin.TorrentLinks.Streaming;

/// <summary>
/// Tries Torbox first, then DMM, then the generic API resolver.
/// </summary>
public class CompositeStreamResolver : IStreamResolver
{
    private readonly TorboxStreamResolver _torbox;
    private readonly DmmStreamResolver _dmm;
    private readonly ApiStreamResolver _api;

    public CompositeStreamResolver(TorboxStreamResolver torbox, DmmStreamResolver dmm, ApiStreamResolver api)
    {
        _torbox = torbox;
        _dmm = dmm;
        _api = api;
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
        var url = await _torbox.GetStreamUrlAsync(title, year, imdbId, season, episode, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        url = await _dmm.GetStreamUrlAsync(title, year, imdbId, season, episode, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        return await _api.GetStreamUrlAsync(title, year, imdbId, season, episode, cancellationToken).ConfigureAwait(false);
    }
}
