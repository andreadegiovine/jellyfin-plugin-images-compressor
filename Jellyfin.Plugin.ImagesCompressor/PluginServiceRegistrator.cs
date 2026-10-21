using Jellyfin.Plugin.ImagesCompressor.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.ImagesCompressor;

/// <summary>
/// Registers the plugin's supporting services. The scheduled task
/// (<see cref="Services.ImagesCompressorTask"/>) must not be registered here: Jellyfin discovers
/// <c>IScheduledTask</c> implementations by scanning the loaded assemblies, and builds them
/// through the DI container, which is why their dependencies are registered here.
/// </summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<CompressionMarkerStore>();
        serviceCollection.AddSingleton<ImageCompressor>();
        serviceCollection.AddHostedService<LibraryScanQueueService>();
    }
}
