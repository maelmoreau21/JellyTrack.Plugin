using System.Text.Json.Serialization;

namespace JellyTrack.Plugin.Models;

public sealed class HeartbeatEvent : PluginEvent
{
    [JsonPropertyName("event")]
    public override string Event => "Heartbeat";

    [JsonPropertyName("pluginVersion")]
    public string PluginVersion { get; set; } = string.Empty;

    [JsonPropertyName("serverName")]
    public string ServerName { get; set; } = string.Empty;

    [JsonPropertyName("jellyfinVersion")]
    public string JellyfinVersion { get; set; } = string.Empty;

    [JsonPropertyName("users")]
    public List<HeartbeatUser> Users { get; set; } = new();
}

public sealed class HeartbeatUser
{
    [JsonPropertyName("jellyfinUserId")]
    public string JellyfinUserId { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;
}
