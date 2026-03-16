using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.TorrentLinks.Api;

/// <summary>
/// Serves the plugin configuration page HTML at /web/configurationpage when the client requests it by plugin name.
/// Fixes 404 when the server does not serve plugin config pages from the core.
/// </summary>
[ApiController]
[Route("web")]
public class ConfigurationPageController : ControllerBase
{
    private const string PluginName = "Torrent & Stream Links";
    private const string ResourceName = "Jellyfin.Plugin.TorrentLinks.Configuration.configPage.html";

    /// <summary>
    /// Serves the config page HTML for this plugin when name matches; otherwise 404.
    /// </summary>
    [HttpGet("configurationpage")]
    [Produces("text/html")]
    public IActionResult GetConfigurationPage([FromQuery] string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || !string.Equals(name.Trim(), PluginName, System.StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        var assembly = typeof(Jellyfin.Plugin.TorrentLinks.Plugin).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream == null)
        {
            return NotFound();
        }

        using var reader = new StreamReader(stream);
        var html = reader.ReadToEnd();
        return Content(html, "text/html; charset=utf-8");
    }
}
