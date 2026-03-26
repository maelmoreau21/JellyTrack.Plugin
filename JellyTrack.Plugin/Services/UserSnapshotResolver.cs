using System.Collections;
using System.Text.RegularExpressions;
using JellyTrack.Plugin.Models;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace JellyTrack.Plugin.Services;

internal static class UserSnapshotResolver
{
    private static readonly Regex CompactGuidPattern = new("^[A-Fa-f0-9]{32}$", RegexOptions.Compiled);

    public static List<HeartbeatUser> ResolveHeartbeatUsers(IUserManager userManager, ILogger logger)
    {
        var rawUsers = GetUsersEnumerable(userManager, logger);
        var users = new List<HeartbeatUser>();

        foreach (var userObj in rawUsers)
        {
            var userId = ReadPropertyAsString(userObj, "Id");
            if (string.IsNullOrWhiteSpace(userId))
            {
                logger.LogWarning("Heartbeat: User found but ID is null/empty. Skipping.");
                continue;
            }

            var username = ReadPropertyAsString(userObj, "Username")
                           ?? ReadPropertyAsString(userObj, "Name")
                           ?? "Unknown";

            users.Add(new HeartbeatUser
            {
                JellyfinUserId = userId,
                Username = username,
            });
        }

        return users;
    }

    public static (string? JellyfinUserId, string? Username) ResolveUserFromSession(object? session)
    {
        if (session is null)
        {
            return (null, null);
        }

        var userId = ReadPropertyAsString(session, "UserId");
        var username = ReadPropertyAsString(session, "UserName")
                       ?? ReadPropertyAsString(session, "Username")
                       ?? ReadPropertyAsString(session, "User")
                       ?? ReadPropertyAsString(session, "Name");

        return (userId, username);
    }

    private static IEnumerable<object> GetUsersEnumerable(IUserManager userManager, ILogger logger)
    {
        var managerType = userManager.GetType();

        // Preferred path when the runtime exposes a Users property.
        var usersProperty = managerType.GetProperty("Users");
        if (usersProperty is not null)
        {
            var usersValue = SafeGetValue(usersProperty, userManager, logger);
            if (usersValue is IEnumerable usersEnumerable)
            {
                foreach (var user in usersEnumerable)
                {
                    if (user is not null)
                    {
                        yield return user;
                    }
                }

                yield break;
            }
        }

        // Fallback for runtimes exposing GetUsers/GetUserList methods.
        foreach (var methodName in new[] { "GetUsers", "GetUserList" })
        {
            var zeroArg = managerType.GetMethod(methodName, Type.EmptyTypes);
            if (zeroArg is not null)
            {
                var result = SafeInvoke(zeroArg, userManager, Array.Empty<object>(), logger);
                if (result is IEnumerable enumerable)
                {
                    foreach (var user in enumerable)
                    {
                        if (user is not null)
                        {
                            yield return user;
                        }
                    }

                    yield break;
                }
            }

            var oneArg = managerType
                .GetMethods()
                .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == 1);
            if (oneArg is not null)
            {
                var result = SafeInvoke(oneArg, userManager, new object?[] { null }, logger);
                if (result is IEnumerable enumerable)
                {
                    foreach (var user in enumerable)
                    {
                        if (user is not null)
                        {
                            yield return user;
                        }
                    }

                    yield break;
                }
            }
        }
    }

    private static object? SafeGetValue(System.Reflection.PropertyInfo property, object instance, ILogger logger)
    {
        try
        {
            return property.GetValue(instance);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CRITICAL: Unable to read user manager property '{PropertyName}'. This usually indicates a Jellyfin version mismatch.", property.Name);
            return null;
        }
    }

    private static object? SafeInvoke(System.Reflection.MethodInfo method, object instance, object?[] args, ILogger logger)
    {
        try
        {
            return method.Invoke(instance, args);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CRITICAL: Unable to invoke user manager method '{MethodName}'.", method.Name);
            return null;
        }
    }

    private static string? ReadPropertyAsString(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName);
        if (property is null)
        {
            return null;
        }

        var value = property.GetValue(target);
        if (value is null)
        {
            return null;
        }

        if (value is Guid guid)
        {
            return guid == Guid.Empty ? null : guid.ToString("D");
        }

        var text = value.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        if (CompactGuidPattern.IsMatch(trimmed))
        {
            return Guid.ParseExact(trimmed, "N").ToString("D");
        }

        return trimmed;
    }
}