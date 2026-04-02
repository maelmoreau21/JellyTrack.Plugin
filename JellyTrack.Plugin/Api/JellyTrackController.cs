using System.Net;
using JellyTrack.Plugin.Models;
using JellyTrack.Plugin.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.IO;
using System.Linq;

namespace JellyTrack.Plugin.Api;

[ApiController]
[Route("JellyTrack")]
public class JellyTrackController : ControllerBase
{
    private readonly JellyTrackApiClient _apiClient;
    private readonly IServerApplicationHost _applicationHost;
    private readonly IUserManager _userManager;
    private readonly IAuthorizationContext _authorizationContext;
    private readonly ILogger<JellyTrackController> _logger;

    public JellyTrackController(
        JellyTrackApiClient apiClient,
        IServerApplicationHost applicationHost,
        IUserManager userManager,
        IAuthorizationContext authorizationContext,
        ILogger<JellyTrackController> logger)
    {
        _apiClient = apiClient;
        _applicationHost = applicationHost;
        _userManager = userManager;
        _authorizationContext = authorizationContext;
        _logger = logger;
    }

    private static bool IsAdminUserObject(object? user)
    {
        if (user is null)
        {
            return false;
        }

        var userType = user.GetType();
        var directAdminProp = userType.GetProperty("IsAdministrator");
        if (directAdminProp?.GetValue(user) is bool isAdminDirect)
        {
            return isAdminDirect;
        }

        var policyProp = userType.GetProperty("Policy");
        var policyObj = policyProp?.GetValue(user);
        var policyAdminProp = policyObj?.GetType().GetProperty("IsAdministrator");
        if (policyAdminProp?.GetValue(policyObj) is bool isAdminFromPolicy)
        {
            return isAdminFromPolicy;
        }

        return false;
    }

    private static bool TryGetBooleanProperty(object? source, string propertyName, out bool value)
    {
        value = false;
        if (source is null)
        {
            return false;
        }

        var prop = source.GetType().GetProperty(propertyName);
        if (prop?.GetValue(source) is bool boolValue)
        {
            value = boolValue;
            return true;
        }

        return false;
    }

    private static string? TryGetStringProperty(object? source, string propertyName)
    {
        if (source is null)
        {
            return null;
        }

        var prop = source.GetType().GetProperty(propertyName);
        var value = prop?.GetValue(source);
        if (value is null)
        {
            return null;
        }

        return value.ToString();
    }

    private object? ResolveUserByIdFromAuthorization(object authInfo)
    {
        var userIdRaw = TryGetStringProperty(authInfo, "UserId");
        if (string.IsNullOrWhiteSpace(userIdRaw))
        {
            return null;
        }

        var getUserByIdMethods = _userManager
            .GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => string.Equals(m.Name, "GetUserById", StringComparison.Ordinal) && m.GetParameters().Length == 1)
            .ToArray();

        foreach (var method in getUserByIdMethods)
        {
            var parameterType = method.GetParameters()[0].ParameterType;
            object? argument = null;

            if (parameterType == typeof(Guid))
            {
                if (!Guid.TryParse(userIdRaw, out var userGuid))
                {
                    continue;
                }
                argument = userGuid;
            }
            else if (parameterType == typeof(string))
            {
                argument = userIdRaw;
            }
            else
            {
                continue;
            }

            try
            {
                return method.Invoke(_userManager, new[] { argument });
            }
            catch
            {
                // Try next overload when available.
            }
        }

        return null;
    }

    private async Task<bool> IsAuthorizedAdminRequestAsync(CancellationToken cancellationToken)
    {
        var auth = await _authorizationContext.GetAuthorizationInfo(Request).ConfigureAwait(false);
        if (auth is null || !auth.IsAuthenticated)
        {
            return false;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        if (IsAdminUserObject(auth.User))
        {
            return true;
        }

        if (TryGetBooleanProperty(auth, "IsAdministrator", out var isAdmin) && isAdmin)
        {
            return true;
        }

        var userPolicy = auth.GetType().GetProperty("UserPolicy")?.GetValue(auth);
        if (TryGetBooleanProperty(userPolicy, "IsAdministrator", out isAdmin) && isAdmin)
        {
            return true;
        }

        var resolvedUser = ResolveUserByIdFromAuthorization(auth);
        if (IsAdminUserObject(resolvedUser))
        {
            return true;
        }

        _logger.LogWarning("Authenticated request rejected on admin endpoint because admin flag could not be resolved.");
        return false;
    }

    [HttpGet("Localization/{lang}")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public ActionResult GetLocalization(string lang)
    {
        if (string.IsNullOrWhiteSpace(lang)) lang = "en";

        var assembly = typeof(JellyTrackController).Assembly;
        var tried = new[]
        {
            $"{typeof(Plugin).Namespace}.Localization.{lang}.json",
            $"{typeof(Plugin).Namespace}.Localization.{(lang.Contains('-') ? lang.Split('-')[0] : lang)}.json",
            $"{typeof(Plugin).Namespace}.Localization.en.json"
        };

        foreach (var name in tried)
        {
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null) continue;
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            return Content(json, "application/json");
        }

        return NotFound();
    }

    [HttpPost("Test")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.ServiceUnavailable)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult> TestConnection([FromBody] TestRequest request, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAdminRequestAsync(cancellationToken).ConfigureAwait(false))
        {
            _logger.LogWarning("Unauthorized access attempt on JellyTrack/Test endpoint.");
            return Unauthorized(new TestConnectionResponse
            {
                Success = false,
                Message = "Administrator authentication required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Url) || string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return BadRequest(new TestConnectionResponse
            {
                Success = false,
                Message = "URL and API Key are required."
            });
        }

        var testEvent = new HeartbeatEvent
        {
            PluginVersion = Plugin.Instance?.Version.ToString() ?? "0.0.0.0",
            ServerName = _applicationHost.FriendlyName,
            JellyfinVersion = _applicationHost.ApplicationVersionString,
            Users = new List<HeartbeatUser>()
        };

        try 
        {
            var result = await _apiClient.TestConnectionAsync(request.Url, request.ApiKey, testEvent, cancellationToken);
            var response = new TestConnectionResponse
            {
                Success = result.Success,
                Endpoint = result.Endpoint,
                StatusCode = (int?)result.StatusCode,
                Message = result.Message
            };

            if (result.Success)
            {
                return Ok(response);
            }

            return StatusCode((int)(result.StatusCode ?? HttpStatusCode.ServiceUnavailable), response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing connection to JellyTrack");
            return StatusCode((int)HttpStatusCode.InternalServerError, new TestConnectionResponse
            {
                Success = false,
                Message = ex.Message
            });
        }
    }

    [HttpPost("HeartbeatNow")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.ServiceUnavailable)]
    public async Task<ActionResult> SendHeartbeatNow(CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAdminRequestAsync(cancellationToken).ConfigureAwait(false))
        {
            _logger.LogWarning("Unauthorized access attempt on JellyTrack/HeartbeatNow endpoint.");
            return Unauthorized(new TestConnectionResponse
            {
                Success = false,
                Message = "Administrator authentication required."
            });
        }

        var config = Plugin.Instance?.Configuration;
        if (config is null || !config.Enabled)
        {
            return BadRequest(new TestConnectionResponse
            {
                Success = false,
                Message = "Plugin disabled or configuration unavailable."
            });
        }

        if (string.IsNullOrWhiteSpace(config.JellyTrackUrl) || string.IsNullOrWhiteSpace(config.ApiKey))
        {
            return BadRequest(new TestConnectionResponse
            {
                Success = false,
                Message = "JellyTrack URL and API key must be configured first."
            });
        }

        var payload = BuildHeartbeatPayload();
        var success = await _apiClient.SendEventAsync(payload, cancellationToken).ConfigureAwait(false);
        if (success)
        {
            return Ok(new TestConnectionResponse
            {
                Success = true,
                Message = "Heartbeat sent successfully."
            });
        }

        return StatusCode((int)HttpStatusCode.ServiceUnavailable, new TestConnectionResponse
        {
            Success = false,
            Message = "Heartbeat could not be delivered to JellyTrack. Check Jellyfin logs for details."
        });
    }

    private HeartbeatEvent BuildHeartbeatPayload()
    {
        return new HeartbeatEvent
        {
            PluginVersion = Plugin.Instance?.Version.ToString() ?? "0.0.0.0",
            ServerName = _applicationHost.FriendlyName,
            JellyfinVersion = _applicationHost.ApplicationVersionString,
            Users = UserSnapshotResolver.ResolveHeartbeatUsers(_userManager, _logger)
        };
    }

    public class TestRequest
    {
        public string Url { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
    }

    public class TestConnectionResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? StatusCode { get; set; }
        public string Endpoint { get; set; } = string.Empty;
    }
}
