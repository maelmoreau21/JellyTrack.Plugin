using System.Reflection;
using JellyTrack.Plugin.Models;
using System.Globalization;
using JellyTrack.Plugin.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace JellyTrack.Plugin.Services;

public class HeartbeatService : IScheduledTask, IHostedService, IDisposable
{
    private readonly JellyTrackApiClient _apiClient;
    private readonly IUserManager _userManager;
    private readonly IServerApplicationHost _appHost;
    private readonly ILogger<HeartbeatService> _logger;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private CancellationTokenSource? _backgroundCts;
    private Task? _backgroundLoop;

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
        await SendHeartbeatInternalAsync("scheduler", cancellationToken).ConfigureAwait(false);
        progress.Report(100);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_backgroundLoop is not null)
        {
            return Task.CompletedTask;
        }

        _backgroundCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _backgroundLoop = RunBackgroundLoopAsync(_backgroundCts.Token);
        var loadedVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        _logger.LogInformation("JellyTrack heartbeat background service started (plugin assembly v{Version})", loadedVersion);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_backgroundCts is null || _backgroundLoop is null)
        {
            return;
        }

        try
        {
            _backgroundCts.Cancel();
            await _backgroundLoop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path.
        }
        finally
        {
            _backgroundCts.Dispose();
            _backgroundCts = null;
            _backgroundLoop = null;
            _logger.LogInformation("JellyTrack heartbeat background service stopped");
        }
    }

    private async Task RunBackgroundLoopAsync(CancellationToken cancellationToken)
    {
        // First heartbeat is sent immediately on startup to mark plugin online quickly.
        await SendHeartbeatInternalAsync("background-startup", cancellationToken).ConfigureAwait(false);

        while (!cancellationToken.IsCancellationRequested)
        {
            var intervalSeconds = GetHeartbeatIntervalSeconds();

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await SendHeartbeatInternalAsync("background-interval", cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendHeartbeatInternalAsync(string source, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || !config.Enabled)
        {
            return;
        }

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LogContainerLocalhostHint(config.JellyTrackUrl);

            var pluginVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

            var users = UserSnapshotResolver.ResolveHeartbeatUsers(_userManager, _logger);
            var runtimeMetrics = _apiClient.GetRuntimeMetricsSnapshot();

            var payload = new HeartbeatEvent
            {
                PluginVersion = pluginVersion,
                ServerName = _appHost.FriendlyName,
                JellyfinVersion = _appHost.ApplicationVersionString,
                Users = users,
                ServerLanguage = !string.IsNullOrWhiteSpace(config.PreferredLanguage)
                    ? config.PreferredLanguage
                    : CultureInfo.CurrentUICulture.Name,
                PluginMetrics = new HeartbeatPluginMetrics
                {
                    QueueDepth = runtimeMetrics.QueueDepth,
                    Retries = runtimeMetrics.RetryAttempts,
                    LastHttpCode = runtimeMetrics.LastHttpCode,
                },
            };

            var success = await _apiClient.SendEventAsync(payload, cancellationToken).ConfigureAwait(false);
            if (success)
            {
                _logger.LogInformation(
                    "JellyTrack heartbeat sent ({Source}) with {UserCount} users",
                    source,
                    users.Count);
            }
            else
            {
                _logger.LogWarning(
                    "JellyTrack heartbeat failed ({Source}). Verify URL/API key/network reachability from Jellyfin host.",
                    source);
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private int GetHeartbeatIntervalSeconds()
    {
        var configured = Plugin.Instance?.Configuration.HeartbeatIntervalSeconds ?? 60;
        return configured > 0 ? configured : 60;
    }

    private void LogContainerLocalhostHint(string? configuredUrl)
    {
        if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var uri))
        {
            return;
        }

        var host = uri.Host?.Trim().ToLowerInvariant();
        if (host is "localhost" or "127.0.0.1" or "::1")
        {
            _logger.LogWarning(
                "JellyTrack URL uses localhost ({Url}). If Jellyfin runs in Docker, localhost points to the Jellyfin container itself. Use host IP, host.docker.internal, or a Docker service name.",
                configuredUrl);
        }
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        var intervalSeconds = GetHeartbeatIntervalSeconds();

        return new[]
        {
            new TaskTriggerInfo
            {
                // Use enum values from MediaBrowser.Model.Tasks.TaskTriggerInfoType (IntervalTrigger, StartupTrigger)
                Type = TaskTriggerInfoType.IntervalTrigger,
                IntervalTicks = TimeSpan.FromSeconds(intervalSeconds).Ticks
            },
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.StartupTrigger
            }
        };
    }

    public void Dispose()
    {
        _backgroundCts?.Cancel();
        _backgroundCts?.Dispose();
        _sendLock.Dispose();
    }
}
