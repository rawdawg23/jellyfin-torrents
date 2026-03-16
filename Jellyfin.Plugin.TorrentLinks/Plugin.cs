using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.TorrentLinks.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.TorrentLinks;

/// <summary>
/// Main plugin that adds external links to torrent/stream search sites.
/// </summary>
public class Plugin : BasePlugin, IHasWebPages, IHasPluginConfiguration
{
    private PluginConfiguration _configuration = new PluginConfiguration();

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    public Plugin()
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Torrent & Stream Links";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("a7b8c9d0-e1f2-3456-7890-abcdef123456");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public BasePluginConfiguration Configuration => _configuration;

    /// <inheritdoc />
    public Type ConfigurationType => typeof(PluginConfiguration);

    /// <inheritdoc />
    public void UpdateConfiguration(BasePluginConfiguration configuration)
    {
        if (configuration is PluginConfiguration pc)
        {
            _configuration = pc;
        }
    }

    /// <inheritdoc />
    public override string Description => "Adds external links to search on torrent and stream indexer sites for movies and series.";

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                DisplayName = "Setup",
                EmbeddedResourcePath = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.Configuration.configPage.html",
                    GetType().Namespace),
                EnableInMainMenu = true,
                MenuSection = "Plugins",
                MenuIcon = "settings"
            }
        ];
    }

}
