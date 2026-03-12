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

        if (!TryResolveEndpoint(config.JellyTrackUrl, out var endpoint))
        {
            _logger.LogWarning("Invalid JellyTrack URL configured: {Url}", config.JellyTrackUrl);
            return false;
        }

        // Attempt to flush queued events first
        await FlushRetryQueueAsync(endpoint, config.ApiKey, cancellationToken).ConfigureAwait(false);

        return await SendSingleEventAsync(endpoint, config.ApiKey, eventPayload, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> SendSingleEventAsync(Uri endpoint, string apiKey, PluginEvent eventPayload, CancellationToken cancellationToken)
    {
        try
        {
            using var request = BuildAuthenticatedRequest(endpoint, apiKey, eventPayload);

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

    private async Task FlushRetryQueueAsync(Uri endpoint, string apiKey, CancellationToken cancellationToken)
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
                using var request = BuildAuthenticatedRequest(endpoint, apiKey, queued);

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

    private HttpRequestMessage BuildAuthenticatedRequest(Uri endpoint, string apiKey, PluginEvent eventPayload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(eventPayload, eventPayload.GetType(), JsonOptions),
            Encoding.UTF8,
            "application/json");

        return request;
    }

    private static bool TryResolveEndpoint(string configuredUrl, out Uri endpoint)
    {
        endpoint = default!;

        if (!Uri.TryCreate(configuredUrl.Trim(), UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        var builder = new UriBuilder(parsed);
        var path = builder.Path?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(path) || path == "/")
        {
            builder.Path = "/api/plugin/events";
        }
        else
        {
            builder.Path = "/" + path.Trim('/');
        }

        endpoint = builder.Uri;
        return true;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
