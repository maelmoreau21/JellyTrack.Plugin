using JellyTrack.Plugin.Models;
using JellyTrack.Plugin.Services;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace JellyTrack.Plugin.Notifiers;

public class PlaybackProgressNotifier : IEventConsumer<PlaybackProgressEventArgs>
{
    private readonly JellyTrackApiClient _apiClient;
    private readonly ILogger<PlaybackProgressNotifier> _logger;
    private readonly Dictionary<string, DateTime> _lastProgressSent = new();
    private readonly object _lock = new();

    public PlaybackProgressNotifier(
        JellyTrackApiClient apiClient,
        ILogger<PlaybackProgressNotifier> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task OnEvent(PlaybackProgressEventArgs e)
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

        // Throttle progress events based on configured interval
        var sessionId = e.Session.Id;
        var intervalSeconds = config.ProgressIntervalSeconds > 0 ? config.ProgressIntervalSeconds : 15;

        lock (_lock)
        {
            if (_lastProgressSent.TryGetValue(sessionId, out var lastSent)
                && (DateTime.UtcNow - lastSent).TotalSeconds < intervalSeconds)
            {
                return;
            }

            _lastProgressSent[sessionId] = DateTime.UtcNow;
        }

        var user = e.Users[0];
        var item = e.Item;

        _logger.LogDebug("PlaybackProgress: {User} at {Position} for {Item}", user.Username, e.PlaybackPositionTicks, item.Name);

        var payload = new PlaybackProgressEvent
        {
            Timestamp = DateTime.UtcNow,
            SessionId = sessionId,
            User = new EventUser
            {
                JellyfinUserId = user.Id.ToString()
            },
            Media = new PlaybackProgressMedia
            {
                JellyfinMediaId = item.Id.ToString()
            },
            PositionTicks = e.PlaybackPositionTicks ?? e.Session.PlayState?.PositionTicks ?? 0,
            IsPaused = e.Session.PlayState?.IsPaused ?? false,
            AudioStreamIndex = e.Session.PlayState?.AudioStreamIndex,
            SubtitleStreamIndex = e.Session.PlayState?.SubtitleStreamIndex
        };

        await _apiClient.SendEventAsync(payload).ConfigureAwait(false);
    }

    /// <summary>
    /// Clean up stale session entries to prevent memory leaks.
    /// Called periodically or on session end.
    /// </summary>
    internal void CleanupSession(string sessionId)
    {
        lock (_lock)
        {
            _lastProgressSent.Remove(sessionId);
        }
    }
}
