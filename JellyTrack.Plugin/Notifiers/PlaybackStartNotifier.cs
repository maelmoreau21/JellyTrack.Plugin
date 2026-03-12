using JellyTrack.Plugin.Models;
using JellyTrack.Plugin.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace JellyTrack.Plugin.Notifiers;

public class PlaybackStartNotifier : IEventConsumer<PlaybackStartEventArgs>
{
    private readonly JellyTrackApiClient _apiClient;
    private readonly ILibraryManager _libraryManager;
    private readonly IMediaSourceManager _mediaSourceManager;
    private readonly ILogger<PlaybackStartNotifier> _logger;

    public PlaybackStartNotifier(
        JellyTrackApiClient apiClient,
        ILibraryManager libraryManager,
        IMediaSourceManager mediaSourceManager,
        ILogger<PlaybackStartNotifier> logger)
    {
        _apiClient = apiClient;
        _libraryManager = libraryManager;
        _mediaSourceManager = mediaSourceManager;
        _logger = logger;
    }

    public async Task OnEvent(PlaybackStartEventArgs e)
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
        var session = e.Session;

        _logger.LogDebug("PlaybackStart: {User} playing {Item}", user.Username, item.Name);

        var media = BuildMediaInfo(item);
        var sessionInfo = BuildSessionInfo(item, session);

        var payload = new PlaybackStartEvent
        {
            Timestamp = DateTime.UtcNow,
            User = new EventUser
            {
                JellyfinUserId = user.Id.ToString(),
                Username = user.Username
            },
            Media = media,
            Session = sessionInfo
        };

        await _apiClient.SendEventAsync(payload).ConfigureAwait(false);
    }

    private EventMedia BuildMediaInfo(BaseItem item)
    {
        var media = new EventMedia
        {
            JellyfinMediaId = item.Id.ToString(),
            Title = item.Name,
            Type = item.GetBaseItemKind().ToString(),
            Genres = item.Genres?.ToList() ?? new List<string>(),
            DurationMs = item.RunTimeTicks.HasValue ? item.RunTimeTicks.Value / 10000 : 0
        };

        // Series / Season info for episodes
        if (item is Episode episode)
        {
            media.SeriesName = episode.SeriesName;
            media.SeasonName = episode.Season?.Name;
            media.ParentId = episode.SeasonId.ToString();
        }

        // Music info
        if (item is Audio audio)
        {
            media.AlbumName = audio.Album;
            media.AlbumArtist = audio.AlbumArtists?.FirstOrDefault();
            media.Artist = audio.Artists?.FirstOrDefault();
        }

        // Resolution from video stream
        var streams = _mediaSourceManager.GetMediaStreams(item.Id);
        var videoStream = streams?.FirstOrDefault(s => s.Type == MediaStreamType.Video);
        if (videoStream is not null)
        {
            int width = videoStream.Width ?? 0;
            media.Resolution = width switch
            {
                >= 3800 => "4K",
                >= 1900 => "1080p",
                >= 1200 => "720p",
                _ => "SD"
            };
        }

        // Collection / library info
        try
        {
            var collectionFolders = _libraryManager.GetCollectionFolders(item);
            var folder = collectionFolders.FirstOrDefault();
            if (folder is not null)
            {
                media.LibraryName = folder.Name;
                if (folder is CollectionFolder cf)
                {
                    media.CollectionType = cf.CollectionType?.ToString()?.ToLowerInvariant()
                                           ?? InferCollectionType(item);
                }
                else
                {
                    media.CollectionType = InferCollectionType(item);
                }
            }
            else
            {
                media.CollectionType = InferCollectionType(item);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not determine collection folder for item {ItemId}", item.Id);
            media.CollectionType = InferCollectionType(item);
        }

        return media;
    }

    private EventSession BuildSessionInfo(BaseItem item, MediaBrowser.Controller.Session.SessionInfo session)
    {
        var sessionEvent = new EventSession
        {
            SessionId = session.Id,
            ClientName = session.Client,
            DeviceName = session.DeviceName,
            PlayMethod = session.PlayState?.PlayMethod?.ToString(),
            IpAddress = session.RemoteEndPoint,
            PositionTicks = session.PlayState?.PositionTicks ?? 0
        };

        // Transcoding info
        if (session.TranscodingInfo is not null)
        {
            sessionEvent.TranscodeFps = session.TranscodingInfo.Framerate;
            sessionEvent.Bitrate = session.TranscodingInfo.Bitrate;
            sessionEvent.VideoCodec = session.TranscodingInfo.VideoCodec;
            sessionEvent.AudioCodec = session.TranscodingInfo.AudioCodec;
        }

        // Audio and subtitle streams
        var streams = _mediaSourceManager.GetMediaStreams(item.Id);
        if (streams is not null)
        {
            var audioIdx = session.PlayState?.AudioStreamIndex;
            var audioStream = audioIdx.HasValue
                ? streams.FirstOrDefault(s => s.Index == audioIdx.Value && s.Type == MediaStreamType.Audio)
                : streams.FirstOrDefault(s => s.Type == MediaStreamType.Audio && s.IsDefault);

            if (audioStream is not null)
            {
                sessionEvent.AudioLanguage = audioStream.Language;
                sessionEvent.AudioCodec ??= audioStream.Codec;
            }

            var subIdx = session.PlayState?.SubtitleStreamIndex;
            if (subIdx.HasValue && subIdx.Value >= 0)
            {
                var subStream = streams.FirstOrDefault(s => s.Index == subIdx.Value && s.Type == MediaStreamType.Subtitle);
                if (subStream is not null)
                {
                    sessionEvent.SubtitleLanguage = subStream.Language;
                    sessionEvent.SubtitleCodec = subStream.Codec;
                }
            }
        }

        // If not transcoding, get codec from video stream
        if (string.IsNullOrEmpty(sessionEvent.VideoCodec) && streams is not null)
        {
            var videoStream = streams.FirstOrDefault(s => s.Type == MediaStreamType.Video);
            sessionEvent.VideoCodec = videoStream?.Codec;
        }

        return sessionEvent;
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
}
