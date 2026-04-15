using MediaBrowser.Model.Plugins;

namespace JellyTrack.Plugin;

public class PluginConfiguration : BasePluginConfiguration
{
    public const int DefaultHeartbeatIntervalSeconds = 600;

    public string JellyTrackUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public int HeartbeatIntervalSeconds { get; set; } = DefaultHeartbeatIntervalSeconds;

    public int ProgressIntervalSeconds { get; set; } = 15;

    // Keep disabled by default so a fresh install does not emit network traffic
    // before the admin validates URL and API key.
    public bool Enabled { get; set; } = false;

    // Optional: preferred language for the plugin. Leave empty to use Jellyfin's current UI language.
    public string PreferredLanguage { get; set; } = string.Empty;
}
