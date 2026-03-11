using System.Text.Json.Serialization;

namespace JellyTrack.Plugin.Models;

public sealed class LibraryChangedEvent : PluginEvent
{
    [JsonPropertyName("event")]
    public override string Event => "LibraryChanged";

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("items")]
    public List<LibraryItem> Items { get; set; } = new();
}

public sealed class LibraryItem
{
    [JsonPropertyName("jellyfinMediaId")]
    public string JellyfinMediaId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("collectionType")]
    public string? CollectionType { get; set; }

    [JsonPropertyName("genres")]
    public List<string> Genres { get; set; } = new();

    [JsonPropertyName("resolution")]
    public string? Resolution { get; set; }

    [JsonPropertyName("durationMs")]
    public long DurationMs { get; set; }

    [JsonPropertyName("parentId")]
    public string? ParentId { get; set; }

    [JsonPropertyName("libraryName")]
    public string? LibraryName { get; set; }

    [JsonPropertyName("artist")]
    public string? Artist { get; set; }
}
