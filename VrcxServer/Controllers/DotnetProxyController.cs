using Microsoft.AspNetCore.Mvc;
using VrcxServer.Services;

namespace VrcxServer.Controllers;

/// <summary>
/// Generic .NET method proxy controller.
/// Routes POST requests to registered service methods via dictionary dispatch.
/// </summary>
[ApiController]
[Route("api/dotnet")]
public class DotnetProxyController : ControllerBase
{
    private readonly VrchatApiService _vrchatApi;
    private readonly ILogger<DotnetProxyController> _logger;
    private readonly Dictionary<string, Func<object?, Task<object?>>> _methods = new();

    public DotnetProxyController(VrchatApiService vrchatApi, ILogger<DotnetProxyController> logger)
    {
        _vrchatApi = vrchatApi;
        _logger = logger;

        // Register VrchatApiService methods
        RegisterMethods();
    }

    /// <summary>
    /// POST /api/dotnet/{className}/{methodName}
    /// Generic method invocation endpoint.
    /// </summary>
    [HttpPost("{className}/{methodName}")]
    public async Task<IActionResult> InvokeMethod(string className, string methodName, [FromBody] object? parameters = null)
    {
        var key = $"{className}.{methodName}";
        _logger.LogDebug("Method proxy invoked: {Key}", key);

        if (_methods.TryGetValue(key, out var method))
        {
            try
            {
                var result = await method(parameters);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Method invocation failed: {Key}", key);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        return NotFound(new { success = false, error = $"Method not found: {key}" });
    }

    private void RegisterMethods()
    {
        // VrchatApiService methods
        RegisterMethod("VrchatApi", "GetConfig", async (p) =>
        {
            var result = await _vrchatApi.GetConfigAsync();
            return result;
        });

        RegisterMethod("VrchatApi", "GetFriends", async (p) =>
        {
            var obj = p as System.Text.Json.JsonElement?;
            var offset = obj?.TryGetProperty("offset", out var o) == true ? o.GetInt32() : 0;
            var n = obj?.TryGetProperty("n", out var nProp) == true ? nProp.GetInt32() : 100;
            var offline = obj?.TryGetProperty("offline", out var off) == true && off.GetBoolean();
            var friends = await _vrchatApi.GetFriendsAsync(offset, n, offline);
            return friends;
        });

        RegisterMethod("VrchatApi", "GetFriend", async (p) =>
        {
            var obj = p as System.Text.Json.JsonElement?;
            var userId = obj?.TryGetProperty("userId", out var u) == true ? u.GetString() : null;
            if (string.IsNullOrEmpty(userId)) throw new ArgumentException("userId is required");
            var friend = await _vrchatApi.GetFriendAsync(userId);
            return friend;
        });

        RegisterMethod("VrchatApi", "GetWorld", async (p) =>
        {
            var obj = p as System.Text.Json.JsonElement?;
            var worldId = obj?.TryGetProperty("worldId", out var w) == true ? w.GetString() : null;
            if (string.IsNullOrEmpty(worldId)) throw new ArgumentException("worldId is required");
            var world = await _vrchatApi.GetWorldAsync(worldId);
            return world;
        });

        RegisterMethod("VrchatApi", "GetNotifications", async (p) =>
        {
            var obj = p as System.Text.Json.JsonElement?;
            var offset = obj?.TryGetProperty("offset", out var o) == true ? o.GetInt32() : 0;
            var n = obj?.TryGetProperty("n", out var nProp) == true ? nProp.GetInt32() : 100;
            var type = obj?.TryGetProperty("type", out var t) == true ? t.GetString() : null;
            var notifs = await _vrchatApi.GetNotificationsAsync(offset, n, type);
            return notifs;
        });

        RegisterMethod("VrchatApi", "GetCurrentUser", async (p) =>
        {
            var user = await _vrchatApi.GetCurrentUserAsync();
            return user;
        });
    }

    private void RegisterMethod(string className, string methodName, Func<object?, Task<object?>> method)
    {
        _methods[$"{className}.{methodName}"] = method;
    }
}
