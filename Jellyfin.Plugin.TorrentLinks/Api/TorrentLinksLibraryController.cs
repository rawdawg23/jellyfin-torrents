using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TorrentLinks.Library;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.TorrentLinks.Api;

/// <summary>
/// API to add Torbox (or other resolver) streams as .strm files to a library folder.
/// </summary>
[ApiController]
[Route("TorrentLinksLibrary")]
public class TorrentLinksLibraryController : ControllerBase
{
    private const string ConfigPagePath = "/web/#/configurationpage";
    private const string ConfigPageQuery = "?name=Torrent%20%26%20Stream%20Links";

    private readonly TorboxLibraryWriter _writer;

    public TorrentLinksLibraryController(TorboxLibraryWriter writer)
    {
        _writer = writer;
    }

    /// <summary>
    /// Returns the full setup URL for this plugin using the current request host (auto-detected).
    /// </summary>
    [HttpGet("SetupUrl")]
    [Produces("application/json")]
    public ActionResult<SetupUrlResponse> GetSetupUrl()
    {
        var scheme = Request.Scheme;
        var host = Request.Host.Value ?? "localhost";
        var setupUrl = $"{scheme}://{host}{ConfigPagePath}{ConfigPageQuery}";
        return Ok(new SetupUrlResponse { SetupUrl = setupUrl });
    }

    /// <summary>
    /// Resolve a stream and write a .strm file to the configured Torbox library folder.
    /// </summary>
    [HttpPost("AddToLibrary")]
    public async Task<ActionResult<AddToLibraryResponse>> AddToLibrary(
        [FromBody] AddToLibraryRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new AddToLibraryResponse { Success = false, Message = "Title is required." });
        }

        var path = await _writer.AddToLibraryAsync(
            request.Title.Trim(),
            request.Year,
            string.IsNullOrWhiteSpace(request.ImdbId) ? null : request.ImdbId.Trim(),
            request.Season,
            request.Episode,
            cancellationToken).ConfigureAwait(false);

        if (path == null)
        {
            return Ok(new AddToLibraryResponse { Success = false, Message = "Could not resolve stream or write .strm. Check Torbox/DMM settings and Torbox library path." });
        }

        return Ok(new AddToLibraryResponse { Success = true, Path = path });
    }
}

/// <summary>
/// Request body for AddToLibrary.
/// </summary>
public class AddToLibraryRequest
{
    /// <summary>Movie or series title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Release year.</summary>
    public int? Year { get; set; }

    /// <summary>IMDb ID (e.g. tt1234567).</summary>
    public string? ImdbId { get; set; }

    /// <summary>Season number for TV episodes.</summary>
    public int? Season { get; set; }

    /// <summary>Episode number for TV episodes.</summary>
    public int? Episode { get; set; }
}

/// <summary>
/// Response from AddToLibrary.
/// </summary>
public class AddToLibraryResponse
{
    /// <summary>Whether the .strm was written.</summary>
    public bool Success { get; set; }

    /// <summary>Path to the created .strm file.</summary>
    public string? Path { get; set; }

    /// <summary>Error or info message.</summary>
    public string? Message { get; set; }
}

/// <summary>
/// Response from GetSetupUrl (auto-detected Jellyfin setup URL).
/// </summary>
public class SetupUrlResponse
{
    /// <summary>Full URL to the plugin configuration page.</summary>
    public string SetupUrl { get; set; } = string.Empty;
}
