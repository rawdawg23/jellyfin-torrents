using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.TorrentLinks.Configuration;

/// <summary>
/// Plugin configuration for torrent/stream link sites.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        Enabled = true;
        Sites = new List<TorrentSiteConfig>
        {
            new TorrentSiteConfig
            {
                Name = "1337x",
                UrlFormat = "https://1337x.to/search/{encoded}/1/",
                Enabled = true
            },
            new TorrentSiteConfig
            {
                Name = "RARBG (archive)",
                UrlFormat = "https://rarbgprx.org/search/?search={encoded}",
                Enabled = true
            },
            new TorrentSiteConfig
            {
                Name = "YTS",
                UrlFormat = "https://yts.mx/browse-movies/{encoded}/all/all/0/latest",
                Enabled = true
            },
            new TorrentSiteConfig
            {
                Name = "Pirate Bay",
                UrlFormat = "https://thepiratebay.org/search/{encoded}/0/99/0",
                Enabled = true
            },
            new TorrentSiteConfig
            {
                Name = "TorrentGalaxy",
                UrlFormat = "https://torrentgalaxy.to/torrents.php?search={encoded}",
                Enabled = true
            }
        };
    }

    /// <summary>
    /// Gets or sets a value indicating whether the plugin is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to add playable stream sources for movies/episodes (requires Stream API URL).
    /// </summary>
    public bool EnableStreamPlayback { get; set; }

    /// <summary>
    /// Gets or sets the stream resolver API URL. Placeholders: {title}, {year}, {imdb}, {season}, {episode}.
    /// Response must be JSON with "url" or "streamUrl" containing the playable URL.
    /// </summary>
    public string StreamApiUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional API key sent as Bearer token when calling the stream API.
    /// </summary>
    public string StreamApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to use Torbox (https://torbox.app) for stream resolution when IMDb ID is available.
    /// </summary>
    public bool UseTorbox { get; set; }

    /// <summary>
    /// Gets or sets the Torbox API key (from Torbox account settings). Used for search and stream links.
    /// </summary>
    public string TorboxApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Torbox API version (default v1).
    /// </summary>
    public string TorboxApiVersion { get; set; } = "v1";

    /// <summary>
    /// Folder path for movies .strm files. Add this folder as a Movies library in Jellyfin. Leave empty to disable.
    /// </summary>
    public string TorboxLibraryPathMovies { get; set; } = string.Empty;

    /// <summary>
    /// Folder path for TV series .strm files. Add this folder as a TV library in Jellyfin. Leave empty to disable.
    /// </summary>
    public string TorboxLibraryPathSeries { get; set; } = string.Empty;

    /// <summary>
    /// Legacy single folder path; used as fallback if TorboxLibraryPathMovies or TorboxLibraryPathSeries is empty.
    /// </summary>
    public string TorboxLibraryPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to use Debrid Media Manager (https://debridmediamanager.com) for stream resolution.
    /// Log in at DMM to get your User ID for the Stremio addon URL.
    /// </summary>
    public bool UseDmm { get; set; }

    /// <summary>
    /// Gets or sets the DMM base URL (e.g. https://debridmediamanager.com or self-hosted).
    /// </summary>
    public string DmmBaseUrl { get; set; } = "https://debridmediamanager.com";

    /// <summary>
    /// Gets or sets the DMM User ID (12-character ID from DMM after login; used in Stremio addon stream URLs).
    /// </summary>
    public string DmmUserId { get; set; } = string.Empty;

    /// <summary>
    /// Maximum stream resolution to allow. Empty = any. Values: 2160, 1080, 720, 480.
    /// </summary>
    public string MaxResolution { get; set; } = string.Empty;

    /// <summary>
    /// Maximum file size in GB for movies (0 = no limit).
    /// </summary>
    public double MaxMovieSizeGb { get; set; }

    /// <summary>
    /// Maximum file size in GB per episode (0 = no limit).
    /// </summary>
    public double MaxEpisodeSizeGb { get; set; }

    /// <summary>
    /// Gets or sets the list of configured torrent/stream sites.
    /// </summary>
    public List<TorrentSiteConfig> Sites { get; set; }
}

/// <summary>
/// Configuration for a single torrent/stream search site.
/// </summary>
public class TorrentSiteConfig
{
    /// <summary>
    /// Gets or sets the display name of the site.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL format. Use {encoded} for URL-encoded search term, {query} for raw, {year} for release year.
    /// </summary>
    public string UrlFormat { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this site is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
