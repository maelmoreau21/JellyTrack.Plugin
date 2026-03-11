using MediaBrowser.Model.Plugins;

namespace JellyTrack.Plugin;

public class PluginConfiguration : BasePluginConfiguration
{
    public string JellyTrackUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public int HeartbeatIntervalSeconds { get; set; } = 60;

    public int ProgressIntervalSeconds { get; set; } = 15;

    public bool Enabled { get; set; } = true;
}
