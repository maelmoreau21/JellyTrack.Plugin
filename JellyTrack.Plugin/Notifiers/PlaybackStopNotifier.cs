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

        if (e.Item is null || e.Session is null)
        {
            _logger.LogDebug("PlaybackStop ignored: missing item or session");
            return;
        }

        var (jellyfinUserId, username) = ResolveUser(e);
        if (string.IsNullOrWhiteSpace(jellyfinUserId))
        {
            _logger.LogWarning("PlaybackStop ignored: could not resolve user for session {SessionId}", e.Session.Id);
            return;
        }

        var item = e.Item;

        _logger.LogInformation("PlaybackStop captured: user={UserId}, item={ItemId}, session={SessionId}", jellyfinUserId, item.Id, e.Session.Id);

        var payload = new PlaybackStopEvent
        {
            Timestamp = DateTime.UtcNow,
            SessionId = e.Session.Id,
            User = new EventUser
            {
                JellyfinUserId = jellyfinUserId,
                Username = username
            },
            Media = new PlaybackStopMedia
            {
                JellyfinMediaId = item.Id.ToString()
            },
            PositionTicks = e.PlaybackPositionTicks ?? 0,
            DurationTicks = item.RunTimeTicks ?? 0
        };

        await _apiClient.SendEventAsync(payload).ConfigureAwait(false);
    }

    private static (string? JellyfinUserId, string? Username) ResolveUser(PlaybackStopEventArgs e)
    {
        var user = e.Users.FirstOrDefault();
        if (user is not null)
        {
            return (user.Id.ToString(), user.Username);
        }

        if (e.Session is not null)
        {
            var userId = ReadPropertyAsString(e.Session, "UserId");
            var username = ReadPropertyAsString(e.Session, "UserName")
                           ?? ReadPropertyAsString(e.Session, "Username");
            return (userId, username);
        }

        return (null, null);
    }

    private static string? ReadPropertyAsString(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName);
        if (property is null)
        {
            return null;
        }

        var value = property.GetValue(target);
        if (value is null)
        {
            return null;
        }

        if (value is Guid guid)
        {
            return guid == Guid.Empty ? null : guid.ToString();
        }

        var text = value.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
