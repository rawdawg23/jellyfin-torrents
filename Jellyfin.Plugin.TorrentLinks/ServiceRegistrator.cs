using Jellyfin.Plugin.TorrentLinks.Streaming;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.TorrentLinks;

/// <summary>
/// Registers plugin services with the Jellyfin DI container.
/// </summary>
public class ServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IExternalUrlProvider, TorrentExternalUrlProvider>();
        serviceCollection.AddSingleton<HttpClient>(_ => new HttpClient());
        serviceCollection.AddSingleton<TorboxStreamResolver>();
        serviceCollection.AddSingleton<DmmStreamResolver>();
        serviceCollection.AddSingleton<ApiStreamResolver>();
        serviceCollection.AddSingleton<IStreamResolver, CompositeStreamResolver>();
        serviceCollection.AddSingleton<IMediaSourceProvider, TorrentStreamMediaSourceProvider>();
    }
}
