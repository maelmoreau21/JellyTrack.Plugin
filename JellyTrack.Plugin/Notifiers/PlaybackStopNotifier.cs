using JellyTrack.Plugin.Models;
using JellyTrack.Plugin.Services;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace JellyTrack.Plugin.Notifiers;

public class PlaybackStopNotifier : IEventConsumer<PlaybackStopEventArgs>
{
    private readonly JellyTrackApiClient _apiClient;
    private readonly ILogger<PlaybackStopNotifier> _logger;

    public PlaybackStopNotifier(
        JellyTrackApiClient apiClient,
        ILogger<PlaybackStopNotifier> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task OnEvent(PlaybackStopEventArgs e)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || !config.Enabled)
        {
            return;
        }

        if (e.Users.Count == 0 || e.Item is null || e.Session is null)
        {
            return;
        }

        var user = e.Users[0];
        var item = e.Item;

        _logger.LogDebug("PlaybackStop: {User} stopped {Item}", user.Username, item.Name);

        var payload = new PlaybackStopEvent
        {
            Timestamp = DateTime.UtcNow,
            SessionId = e.Session.Id,
            User = new EventUser
            {
                JellyfinUserId = user.Id.ToString("N")
            },
            Media = new PlaybackStopMedia
            {
                JellyfinMediaId = item.Id.ToString("N")
            },
            PositionTicks = e.PlaybackPositionTicks ?? 0,
            DurationTicks = item.RunTimeTicks ?? 0
        };

        await _apiClient.SendEventAsync(payload).ConfigureAwait(false);
    }
}
