using JellyTrack.Plugin.Models;
using JellyTrack.Plugin.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace JellyTrack.Plugin.Notifiers;

public class PlaybackProgressNotifier : IEventConsumer<PlaybackProgressEventArgs>
{
    private readonly JellyTrackApiClient _apiClient;
    private readonly IMediaSourceManager _mediaSourceManager;
    private readonly ILogger<PlaybackProgressNotifier> _logger;
    private readonly Dictionary<string, DateTime> _lastProgressSent = new();
    private readonly object _lock = new();

    public PlaybackProgressNotifier(
        JellyTrackApiClient apiClient,
        IMediaSourceManager mediaSourceManager,
        ILogger<PlaybackProgressNotifier> logger)
    {
        _apiClient = apiClient;
        _mediaSourceManager = mediaSourceManager;
        _logger = logger;
    }

    public async Task OnEvent(PlaybackProgressEventArgs e)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || !config.Enabled)
        {
            return;
        }

        if (e.Item is null || e.Session is null)
        {
            _logger.LogDebug("PlaybackProgress ignored: missing item or session");
            return;
        }

        var (jellyfinUserId, username) = UserSnapshotResolver.ResolveUserFromSession(e.Session);
        if (string.IsNullOrWhiteSpace(jellyfinUserId))
        {
            _logger.LogWarning("PlaybackProgress ignored: could not resolve user for session {SessionId}", e.Session.Id);
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

        var item = e.Item;

        _logger.LogDebug("PlaybackProgress: {User} at {Position} for {Item}", username ?? jellyfinUserId, e.PlaybackPositionTicks, item.Name);

        PlaybackProgressEvent payload;
        try
        {
            payload = new PlaybackProgressEvent
            {
                Timestamp = DateTime.UtcNow,
                SessionId = sessionId,
                User = new EventUser
                {
                    JellyfinUserId = jellyfinUserId,
                    Username = username
                },
                Media = new PlaybackProgressMedia
                {
                    JellyfinMediaId = item.Id.ToString(),
                    Title = item.Name,
                    Type = item.GetBaseItemKind().ToString(),
                    CollectionType = InferCollectionType(item),
                    DurationMs = item.RunTimeTicks.HasValue ? item.RunTimeTicks.Value / 10000 : 0,
                },
                Session = BuildSessionInfo(item, e.Session),
                PositionTicks = e.PlaybackPositionTicks ?? e.Session.PlayState?.PositionTicks ?? 0,
                IsPaused = e.Session.PlayState?.IsPaused ?? false,
                AudioStreamIndex = e.Session.PlayState?.AudioStreamIndex,
                SubtitleStreamIndex = e.Session.PlayState?.SubtitleStreamIndex
            };

            if (item is Episode episode)
            {
                payload.Media.SeriesName = episode.SeriesName;
                payload.Media.SeasonName = episode.Season?.Name;
                payload.Media.ParentId = episode.SeasonId.ToString();
            }

            if (item is Audio audio)
            {
                payload.Media.AlbumName = audio.Album;
                payload.Media.AlbumArtist = audio.AlbumArtists?.FirstOrDefault();
                payload.Media.Artist = audio.Artists?.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PlaybackProgress enrichment failed for session {SessionId}. Sending minimal payload.", sessionId);
            payload = new PlaybackProgressEvent
            {
                Timestamp = DateTime.UtcNow,
                SessionId = sessionId,
                User = new EventUser
                {
                    JellyfinUserId = jellyfinUserId,
                    Username = username
                },
                Media = new PlaybackProgressMedia
                {
                    JellyfinMediaId = item.Id.ToString(),
                    Title = item.Name,
                    Type = item.GetBaseItemKind().ToString(),
                    DurationMs = item.RunTimeTicks.HasValue ? item.RunTimeTicks.Value / 10000 : 0,
                },
                Session = new EventSession
                {
                    SessionId = e.Session.Id,
                    ClientName = e.Session.Client,
                    DeviceName = e.Session.DeviceName,
                    PlayMethod = e.Session.PlayState?.PlayMethod?.ToString(),
                    IpAddress = e.Session.RemoteEndPoint,
                    PositionTicks = e.Session.PlayState?.PositionTicks ?? 0
                },
                PositionTicks = e.PlaybackPositionTicks ?? e.Session.PlayState?.PositionTicks ?? 0,
                IsPaused = e.Session.PlayState?.IsPaused ?? false,
                AudioStreamIndex = e.Session.PlayState?.AudioStreamIndex,
                SubtitleStreamIndex = e.Session.PlayState?.SubtitleStreamIndex
            };
        }

        await _apiClient.SendEventAsync(payload).ConfigureAwait(false);
    }

    private EventSession BuildSessionInfo(BaseItem item, MediaBrowser.Controller.Session.SessionInfo session)
    {
        var sessionInfo = new EventSession
        {
            SessionId = session.Id,
            ClientName = session.Client,
            DeviceName = session.DeviceName,
            PlayMethod = session.PlayState?.PlayMethod?.ToString(),
            IpAddress = session.RemoteEndPoint,
            PositionTicks = session.PlayState?.PositionTicks ?? 0,
        };

        if (session.TranscodingInfo is not null)
        {
            sessionInfo.TranscodeFps = session.TranscodingInfo.Framerate;
            sessionInfo.Bitrate = session.TranscodingInfo.Bitrate;
            sessionInfo.VideoCodec = session.TranscodingInfo.VideoCodec;
            sessionInfo.AudioCodec = session.TranscodingInfo.AudioCodec;
        }

        var streams = _mediaSourceManager.GetMediaStreams(item.Id);
        if (streams is not null)
        {
            var audioIdx = session.PlayState?.AudioStreamIndex;
            var audioStream = audioIdx.HasValue
                ? streams.FirstOrDefault(s => s.Index == audioIdx.Value && s.Type == MediaStreamType.Audio)
                : streams.FirstOrDefault(s => s.Type == MediaStreamType.Audio && s.IsDefault);

            if (audioStream is not null)
            {
                sessionInfo.AudioLanguage = audioStream.Language;
                sessionInfo.AudioCodec ??= audioStream.Codec;
            }

            var subIdx = session.PlayState?.SubtitleStreamIndex;
            if (subIdx.HasValue && subIdx.Value >= 0)
            {
                var subStream = streams.FirstOrDefault(s => s.Index == subIdx.Value && s.Type == MediaStreamType.Subtitle);
                if (subStream is not null)
                {
                    sessionInfo.SubtitleLanguage = subStream.Language;
                    sessionInfo.SubtitleCodec = subStream.Codec;
                }
            }

            if (string.IsNullOrEmpty(sessionInfo.VideoCodec))
            {
                var videoStream = streams.FirstOrDefault(s => s.Type == MediaStreamType.Video);
                sessionInfo.VideoCodec = videoStream?.Codec;
            }
        }

        return sessionInfo;
    }

    private static string InferCollectionType(BaseItem item)
    {
        return item switch
        {
            Episode => "tvshows",
            Audio => "music",
            _ => "movies"
        };
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
