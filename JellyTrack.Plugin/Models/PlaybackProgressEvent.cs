using System.Text.Json.Serialization;

namespace JellyTrack.Plugin.Models;

public sealed class PlaybackProgressEvent : PluginEvent
{
    [JsonPropertyName("event")]
    public override string Event => "PlaybackProgress";

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("user")]
    public EventUser User { get; set; } = new();

    [JsonPropertyName("media")]
    public PlaybackProgressMedia Media { get; set; } = new();

    [JsonPropertyName("positionTicks")]
    public long PositionTicks { get; set; }

    [JsonPropertyName("isPaused")]
    public bool IsPaused { get; set; }

    [JsonPropertyName("audioStreamIndex")]
    public int? AudioStreamIndex { get; set; }

    [JsonPropertyName("subtitleStreamIndex")]
    public int? SubtitleStreamIndex { get; set; }
}

public sealed class PlaybackProgressMedia
{
    [JsonPropertyName("jellyfinMediaId")]
    public string JellyfinMediaId { get; set; } = string.Empty;
}
