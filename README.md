# Jellyfin Plugin: Torrent & Stream Links

Adds **external links** on movie and series detail pages in Jellyfin so you can quickly open search pages on torrent and stream indexer sites.

## Features

- **External link provider**: On each movie/series/episode page, Jellyfin shows "External links" (e.g. in the web app under the item). This plugin adds links like "1337x", "YTS", "Pirate Bay", etc., that open a search for that title on the configured site.
- **Configurable sites**: Enable/disable sites and add your own. Each site uses a URL format with placeholders:
  - `{encoded}` – URL-encoded search query (title)
  - `{query}` – raw title
  - `{year}` – release year (if available)
- **No API keys** (for links): Uses public search URLs only; no login or API required for the default sites.
- **Stream playback (optional)**: When enabled, the plugin adds a **playable stream source** for movies and episodes so you can watch directly in Jellyfin. Options (tried in order):
  - **Torbox**: Enable "Use Torbox" and set your [Torbox](https://torbox.app) API key (use **Request auth via Torbox** at the top of the plugin config to open Torbox and get your key). The plugin will search by IMDb ID, add the torrent if needed, and return a direct stream link.
  - **Debrid Media Manager (DMM)**: Enable "Use Debrid Media Manager" and set your [DMM](https://debridmediamanager.com) base URL and **User ID**. Log in at DMM with your debrid account (Real-Debrid, AllDebrid, or Torbox); your 12-character User ID is in the Stremio addon install URL. DMM then serves streams by IMDb ID.
  - **Custom API**: Or set a **Stream API URL** (with placeholders `{title}`, `{year}`, `{imdb}`, etc.) that returns JSON with a direct stream URL (`url` or `streamUrl`). Optional API key is sent as Bearer token.

## Installation

### Repo install (recommended)

1. In Jellyfin go to **Dashboard → Plugins → Repositories**.
2. Click **Add** and paste this repository URL:
   ```
   https://raw.githubusercontent.com/rawdawg23/jellyfin-torrents/main/manifest.json
   ```
3. Save, then open **Dashboard → Plugins → Catalog**. Find **Torrent & Stream Links** and click **Install**.
4. Restart Jellyfin if prompted. Configure under **Dashboard → Plugins → Torrent & Stream Links**.

### Manual install

1. **Build** (requires [.NET 9 SDK](https://dotnet.microsoft.com/download)):
   ```bash
   dotnet build -c Release
   ```
2. Copy `Jellyfin.Plugin.TorrentLinks/bin/Release/net9.0/Jellyfin.Plugin.TorrentLinks.dll` (and any other output files) into your Jellyfin server’s plugin folder:
   - **Windows**: `%ProgramData%\Jellyfin\Server\plugins`
   - **Docker**: `/config/plugins` (if mounted)
   - **Linux**: `/var/lib/jellyfin/plugins` or as per your install
3. Restart Jellyfin. The plugin appears under **Dashboard → Plugins** and can be configured there.

## Configuration

- **Dashboard → Plugins → Torrent & Stream Links**
- **Enable plugin**: Show or hide external search links on media pages.
- **Setup UI**: The plugin has a structured setup page (Dashboard → Plugins → Torrent & Stream Links) with sections: General, **Quality & limits**, Stream playback, Torbox, Debrid Media Manager, and External link sites.
- **Quality & limits**: Inside Jellyfin you can cap stream quality:
  - **Max resolution**: Any, 4K (2160p), 1080p, 720p, or 480p. Only streams at or below this resolution are offered (Torbox file selection respects this).
  - **Max movie size (GB)** and **Max episode size (GB)**: Leave 0 for no limit, or set a maximum file size so only smaller streams are chosen.
- **Stream playback**: Enable **Enable stream playback**, then any of:
  - **Request auth via Torbox**: Use the button at the top to open Torbox and get your API key.
  - **Torbox**: Check **Use Torbox** and enter your **Torbox API key**. Quality limits apply to Torbox stream selection.
  - **Debrid Media Manager**: Check **Use DMM**, set **DMM base URL** and your **DMM User ID** (from DMM after login).
  - **Custom API**: Set **Stream API URL** with placeholders; response must be JSON with `url` or `streamUrl`.
- **Torbox library (mounts)**: Set **Movies mount path** and/or **Series mount path** to folders on the server. The plugin writes **.strm** files (and optional .nfo) there when you use **Add to library now**—movies go to the movies path; entries with Season/Episode go to the series path. Use **Add movies library (1 click)** and **Add TV library (1 click)** to add each folder as a Jellyfin library.
- **Sites**: Edit the list of torrent/stream search sites (name, URL format, enabled). Example URL format: `https://example.com/search?q={encoded}`.

## Default sites (examples)

The plugin ships with example URL formats for a few well-known indexers. You can disable or change them. Use only sites and content that you are allowed to use in your jurisdiction.

## Requirements

- Jellyfin server **10.9+** (target ABI 10.9.0.0)
- .NET 9.0

## Development & GitHub hosting

- Solution: `Jellyfin.Plugin.TorrentLinks.sln`
- Project: `Jellyfin.Plugin.TorrentLinks/Jellyfin.Plugin.TorrentLinks.csproj`
- Build: `dotnet build -c Release`
- Plugin ID: `a7b8c9d0-e1f2-3456-7890-abcdef123456`

### Releasing from GitHub

1. Push the repo to GitHub.
2. Create a **Release** (e.g. tag `v1.0.0.0`, title/notes optional) and publish it.
3. The **Release** workflow runs: it builds the plugin, zips it, uploads the zip to the release, and updates `manifest.json` with the new version (sourceUrl, MD5 checksum, timestamp).
4. Users add your repo’s manifest URL in **Dashboard → Plugins → Repositories** and install from the catalog. Use the **raw** manifest URL, e.g.:
   `https://raw.githubusercontent.com/OWNER/REPO/main/manifest.json` (or `master` if that’s your default branch).
5. To change the plugin “owner” label in the catalog, edit the `owner` field in `manifest.json` (e.g. your GitHub username).

## Disclaimer

This plugin only adds links to external search pages. It does not host, stream, or download content. You are responsible for complying with your local laws and the terms of use of any site you open.
