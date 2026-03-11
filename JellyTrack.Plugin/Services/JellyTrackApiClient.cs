using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using JellyTrack.Plugin.Models;
using Microsoft.Extensions.Logging;

namespace JellyTrack.Plugin.Services;

public class JellyTrackApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<JellyTrackApiClient> _logger;
    private readonly ConcurrentQueue<PluginEvent> _retryQueue = new();
    private const int MaxQueueSize = 100;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public JellyTrackApiClient(IHttpClientFactory httpClientFactory, ILogger<JellyTrackApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient(nameof(JellyTrackApiClient));
        _httpClient.Timeout = TimeSpan.FromSeconds(5);
        _logger = logger;
    }

    public async Task<bool> SendEventAsync(PluginEvent eventPayload, CancellationToken cancellationToken = default)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || !config.Enabled)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.JellyTrackUrl) || string.IsNullOrWhiteSpace(config.ApiKey))
        {
            _logger.LogWarning("JellyTrack URL or API key is not configured");
            return false;
        }

        // Attempt to flush queued events first
        await FlushRetryQueueAsync(config, cancellationToken).ConfigureAwait(false);

        return await SendSingleEventAsync(config, eventPayload, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> SendSingleEventAsync(PluginConfiguration config, PluginEvent eventPayload, CancellationToken cancellationToken)
    {
        try
        {
            var url = config.JellyTrackUrl.TrimEnd('/');
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(eventPayload, eventPayload.GetType(), JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("JellyTrack event {Event} sent successfully", eventPayload.Event);
                return true;
            }

            _logger.LogWarning("JellyTrack API returned {StatusCode} for event {Event}", response.StatusCode, eventPayload.Event);
            EnqueueForRetry(eventPayload);
            return false;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("JellyTrack API request timed out for event {Event}", eventPayload.Event);
            EnqueueForRetry(eventPayload);
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to send event {Event} to JellyTrack", eventPayload.Event);
            EnqueueForRetry(eventPayload);
            return false;
        }
    }

    private void EnqueueForRetry(PluginEvent eventPayload)
    {
        if (_retryQueue.Count >= MaxQueueSize)
        {
            _retryQueue.TryDequeue(out _);
        }

        _retryQueue.Enqueue(eventPayload);
    }

    private async Task FlushRetryQueueAsync(PluginConfiguration config, CancellationToken cancellationToken)
    {
        var count = _retryQueue.Count;
        for (int i = 0; i < count; i++)
        {
            if (!_retryQueue.TryDequeue(out var queued))
            {
                break;
            }

            try
            {
                var url = config.JellyTrackUrl.TrimEnd('/');
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
                request.Content = new StringContent(
                    JsonSerializer.Serialize(queued, queued.GetType(), JsonOptions),
                    Encoding.UTF8,
                    "application/json");

                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Retry failed for queued event {Event}, re-queuing", queued.Event);
                    EnqueueForRetry(queued);
                    break; // stop flushing if server is still down
                }
            }
            catch (Exception)
            {
                EnqueueForRetry(queued);
                break;
            }
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
