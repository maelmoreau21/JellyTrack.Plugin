using System.Text.Json.Serialization;

namespace JellyTrack.Plugin.Models;

public sealed class PlaybackStopEvent : PluginEvent
{
    [JsonPropertyName("event")]
    public override string Event => "PlaybackStop";

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("user")]
    public EventUser User { get; set; } = new();

    [JsonPropertyName("media")]
    public PlaybackStopMedia Media { get; set; } = new();

    [JsonPropertyName("positionTicks")]
    public long PositionTicks { get; set; }

    [JsonPropertyName("durationTicks")]
    public long DurationTicks { get; set; }
}

public sealed class PlaybackStopMedia
{
    [JsonPropertyName("jellyfinMediaId")]
    public string JellyfinMediaId { get; set; } = string.Empty;
}
