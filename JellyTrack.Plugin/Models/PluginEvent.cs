using System.Text.Json.Serialization;

namespace JellyTrack.Plugin.Models;

public abstract class PluginEvent
{
    [JsonPropertyName("event")]
    public abstract string Event { get; }
}
