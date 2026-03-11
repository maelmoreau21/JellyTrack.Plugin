using System.Reflection;
using JellyTrack.Plugin.Models;
using JellyTrack.Plugin.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace JellyTrack.Plugin.Services;

public class HeartbeatService : IScheduledTask
{
    private readonly JellyTrackApiClient _apiClient;
    private readonly IUserManager _userManager;
    private readonly IServerApplicationHost _appHost;
    private readonly ILogger<HeartbeatService> _logger;

    public HeartbeatService(
        JellyTrackApiClient apiClient,
        IUserManager userManager,
        IServerApplicationHost appHost,
        ILogger<HeartbeatService> logger)
    {
        _apiClient = apiClient;
        _userManager = userManager;
        _appHost = appHost;
        _logger = logger;
    }

    public string Name => "JellyTrack Heartbeat";

    public string Key => "JellyTrackHeartbeat";

    public string Description => "Envoie un heartbeat périodique à JellyTrack avec la liste des utilisateurs.";

    public string Category => "JellyTrack";

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || !config.Enabled)
        {
            return;
        }

        _logger.LogDebug("Sending heartbeat to JellyTrack");

        var pluginVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

        var users = _userManager.Users
            .Select(u => new HeartbeatUser
            {
                JellyfinUserId = u.Id.ToString("N"),
                Username = u.Username
            })
            .ToList();

        var payload = new HeartbeatEvent
        {
            PluginVersion = pluginVersion,
            ServerName = _appHost.FriendlyName,
            JellyfinVersion = _appHost.ApplicationVersionString,
            Users = users
        };

        await _apiClient.SendEventAsync(payload, cancellationToken).ConfigureAwait(false);

        progress.Report(100);
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        var intervalSeconds = Plugin.Instance?.Configuration.HeartbeatIntervalSeconds ?? 60;

        return new[]
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfo.TriggerInterval,
                IntervalTicks = TimeSpan.FromSeconds(intervalSeconds).Ticks
            },
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfo.TriggerStartup
            }
        };
    }
}
