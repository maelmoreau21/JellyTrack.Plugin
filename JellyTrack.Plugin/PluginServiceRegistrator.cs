using JellyTrack.Plugin.Notifiers;
using JellyTrack.Plugin.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JellyTrack.Plugin;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<JellyTrackApiClient>();
        serviceCollection.AddSingleton<HeartbeatService>();
        serviceCollection.AddSingleton<IScheduledTask>(sp => sp.GetRequiredService<HeartbeatService>());
        serviceCollection.AddSingleton<IHostedService>(sp => sp.GetRequiredService<HeartbeatService>());
        serviceCollection.AddSingleton<IEventConsumer<PlaybackStartEventArgs>, PlaybackStartNotifier>();
        serviceCollection.AddSingleton<IEventConsumer<PlaybackStopEventArgs>, PlaybackStopNotifier>();
        serviceCollection.AddSingleton<IEventConsumer<PlaybackProgressEventArgs>, PlaybackProgressNotifier>();
        serviceCollection.AddHostedService<LibraryChangeNotifier>();
    }
}
