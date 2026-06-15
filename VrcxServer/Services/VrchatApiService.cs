using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VrcxServer.Models;

namespace VrcxServer.Services;

/// <summary>
/// HTTP client wrapper for the VRChat API (https://api.vrchat.cloud/api/1).
/// Handles authentication, cookie persistence, and all API interactions.
/// Auth is purely cookie-based — matches VRCX WebApi.cs pattern.
/// </summary>
public class VrchatApiService : IDisposable
{
    private readonly ILogger<VrchatApiService> _logger;
    private readonly HttpClient _httpClient;
    private readonly SocketsHttpHandler _httpHandler;
    private readonly CookieContainer _cookieContainer;
    private string? _authToken;
    private string? _currentUserJson;
    private readonly JsonSerializerOptions _jsonOptions;

    private const string BaseUrl = "https://api.vrchat.cloud/api/1/";
    private const string UserAgent = "VRCX-Server/1.0";

    public VrchatApiService(ILogger<VrchatApiService> logger)
    {
        _logger = logger;
        _cookieContainer = new CookieContainer();

        // Match VRCX WebApi.cs exactly — SocketsHttpHandler with CookieContainer
        _httpHandler = new SocketsHttpHandler
        {
            CookieContainer = _cookieContainer,
            UseCookies = true,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = 10
        };

        _httpClient = new HttpClient(_httpHandler);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // DO NOT set BaseAddress — use full URLs in requests
        // DO NOT set Authorization header — auth is purely cookie-based

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Whether the user is currently authenticated.
    /// </summary>
    public bool IsAuthenticated => !string.IsNullOrEmpty(_authToken);

    /// <summary>
    /// The current auth token.
    /// </summary>
    public string? AuthToken => _authToken;

    /// <summary>
    /// Get VRChat server config (public endpoint, no auth required).
    /// </summary>
    public async Task<JsonElement?> GetConfigAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}config");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonElement>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get VRChat config");
            return null;
        }
    }

    /// <summary>
    /// Login to VRChat with username and password.
    /// Uses Basic auth header on a separate client. Transfers cookies to shared container after.
    /// </summary>
    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        try
        {
            // Build Basic auth header
            var authString = $"{username}:{password}";
            var authBytes = Encoding.UTF8.GetBytes(authString);
            var authBase64 = Convert.ToBase64String(authBytes);

            // Create a SEPARATE HttpClient for login (Basic auth)
            using var loginHandler = new SocketsHttpHandler
            {
                CookieContainer = new CookieContainer(),
                UseCookies = true,
                AutomaticDecompression = DecompressionMethods.All
            };
            using var loginClient = new HttpClient(loginHandler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            loginClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            loginClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}auth/user");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authBase64);

            var response = await loginClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            // Check if 2FA is required
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var userJson = JsonSerializer.Deserialize<JsonElement>(json, _jsonOptions);

                if (userJson.TryGetProperty("requiresTwoFactorAuth", out var twoFactorElement))
                {
                    // 2FA required — transfer auth cookie from loginClient to shared container
                    TransferCookies(loginHandler.CookieContainer);
                    _currentUserJson = json;

                    var methods = new List<string>();
                    foreach (var method in twoFactorElement.EnumerateArray())
                    {
                        methods.Add(method.GetString() ?? "");
                    }

                    return new AuthResult
                    {
                        Success = false,
                        RequiresTwoFactorAuth = true,
                        TwoFactorAuthMethods = methods
                    };
                }

                // No 2FA needed — transfer cookie and extract token
                TransferCookies(loginHandler.CookieContainer);
                ExtractAuthTokenFromCookie();
                _currentUserJson = json;
                _logger.LogInformation("Login successful for user");

                return new AuthResult
                {
                    Success = true,
                    AuthToken = _authToken,
                    CurrentUser = MapUserToFriend(userJson)
                };
            }

            // Parse error
            try
            {
                var errorJson = JsonSerializer.Deserialize<JsonElement>(json, _jsonOptions);
                var errorMessage = errorJson.TryGetProperty("error", out var errorProp)
                    ? errorProp.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Unknown error"
                    : "Unknown error";
                return new AuthResult { Success = false, ErrorMessage = errorMessage };
            }
            catch
            {
                return new AuthResult { Success = false, ErrorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}" };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed");
            return new AuthResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// Verify two-factor authentication code.
    /// Uses _httpClient — cookies were transferred from login.
    /// </summary>
    public async Task<AuthResult> Verify2FAAsync(string code, string method = "totp")
    {
        try
        {
            string endpoint = method.ToLower() switch
            {
                "otp" => $"{BaseUrl}auth/twofactorauth/otp/verify",
                "emailotp" or "email" => $"{BaseUrl}auth/twofactorauth/emailotp/verify",
                _ => $"{BaseUrl}auth/twofactorauth/totp/verify"
            };

            var requestBody = JsonSerializer.Serialize(new { code }, _jsonOptions);
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            // Use _httpClient — cookies were transferred from login
            var response = await _httpClient.PostAsync(endpoint, content);
            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // Check if still needs 2FA
                var resultJson = JsonSerializer.Deserialize<JsonElement>(json, _jsonOptions);
                if (resultJson.TryGetProperty("requiresTwoFactorAuth", out _))
                {
                    return new AuthResult
                    {
                        Success = false,
                        RequiresTwoFactorAuth = true,
                        TwoFactorAuthMethods = new List<string> { "totp", "otp", "emailotp" }
                    };
                }

                // 2FA succeeded — extract auth cookie
                ExtractAuthTokenFromCookie();

                // Get current user
                var userResponse = await _httpClient.GetAsync($"{BaseUrl}auth/user");
                if (userResponse.IsSuccessStatusCode)
                {
                    var userJson = await userResponse.Content.ReadAsStringAsync();
                    _currentUserJson = userJson;
                    var userElement = JsonSerializer.Deserialize<JsonElement>(userJson, _jsonOptions);
                    return new AuthResult
                    {
                        Success = true,
                        AuthToken = _authToken,
                        CurrentUser = MapUserToFriend(userElement)
                    };
                }

                return new AuthResult { Success = true, AuthToken = _authToken };
            }

            return new AuthResult { Success = false, ErrorMessage = $"2FA verification failed: HTTP {(int)response.StatusCode}" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "2FA verification failed");
            return new AuthResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// Get friends list from VRChat API with pagination.
    /// </summary>
    public async Task<List<Friend>> GetFriendsAsync(int offset = 0, int n = 100, bool offline = false)
    {
        EnsureAuthenticated();
        try
        {
            var url = $"{BaseUrl}auth/user/friends?offset={offset}&n={n}&offline={offline.ToString().ToLower()}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var friendsJson = JsonSerializer.Deserialize<List<JsonElement>>(json, _jsonOptions) ?? new();

            return friendsJson.Select(MapUserToFriend).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get friends");
            return new List<Friend>();
        }
    }

    /// <summary>
    /// Get a single friend by user ID.
    /// </summary>
    public async Task<Friend?> GetFriendAsync(string userId)
    {
        EnsureAuthenticated();
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}users/{userId}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var userJson = JsonSerializer.Deserialize<JsonElement>(json, _jsonOptions);
            return MapUserToFriend(userJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get friend {UserId}", userId);
            return null;
        }
    }

    /// <summary>
    /// Get world information by world ID.
    /// </summary>
    public async Task<JsonElement?> GetWorldAsync(string worldId)
    {
        EnsureAuthenticated();
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}worlds/{worldId}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonElement>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get world {WorldId}", worldId);
            return null;
        }
    }

    public async Task<JsonElement?> GetWorldInstancesAsync(string worldId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}worlds/{worldId}");
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(content);

            if (doc.RootElement.TryGetProperty("instances", out var instances))
            {
                return instances;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting world instances for {WorldId}", worldId);
            return null;
        }
    }

    public async Task<JsonElement?> GetInstanceAsync(string worldId, string instanceId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}worlds/{worldId}/{instanceId}");
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(content);
            return doc.RootElement;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting instance {InstanceId} for world {WorldId}", instanceId, worldId);
            return null;
        }
    }

    /// <summary>
    /// Get notifications from VRChat API.
    /// </summary>
    public async Task<List<Notification>> GetNotificationsAsync(int offset = 0, int n = 100, string? type = null)
    {
        EnsureAuthenticated();
        try
        {
            var url = $"{BaseUrl}auth/user/notifications?offset={offset}&n={n}";
            if (!string.IsNullOrEmpty(type))
            {
                url += $"&type={Uri.EscapeDataString(type)}";
            }

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var notifsJson = JsonSerializer.Deserialize<List<JsonElement>>(json, _jsonOptions) ?? new();

            return notifsJson.Select(MapNotification).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get notifications");
            return new List<Notification>();
        }
    }

    public async Task<bool> AcceptNotificationAsync(string notificationId)
    {
        try
        {
            var response = await _httpClient.PutAsync(
                $"{BaseUrl}auth/user/notifications/{notificationId}/accept",
                null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting notification {NotificationId}", notificationId);
            return false;
        }
    }

    public async Task<bool> DeclineNotificationAsync(string notificationId)
    {
        try
        {
            var response = await _httpClient.PutAsync(
                $"{BaseUrl}auth/user/notifications/{notificationId}/decline",
                null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error declining notification {NotificationId}", notificationId);
            return false;
        }
    }

    public async Task<bool> DeleteNotificationAsync(string notificationId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(
                $"{BaseUrl}auth/user/notifications/{notificationId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification {NotificationId}", notificationId);
            return false;
        }
    }

    /// <summary>
    /// Get the current user info.
    /// </summary>
    public async Task<Friend?> GetCurrentUserAsync()
    {
        EnsureAuthenticated();
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}auth/user");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            _currentUserJson = json;
            var userJson = JsonSerializer.Deserialize<JsonElement>(json, _jsonOptions);
            return MapUserToFriend(userJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current user");
            return null;
        }
    }

    /// <summary>
    /// Set the auth token directly (for session restoration).
    /// Adds as cookie — does NOT set Bearer header.
    /// </summary>
    public void SetAuthToken(string token)
    {
        _authToken = token;
        // Add as cookie instead of Bearer header
        _cookieContainer.Add(new Uri(BaseUrl), new Cookie("auth", token));
        // DO NOT: _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Transfer all cookies from source container to shared _cookieContainer.
    /// Called after login to move auth cookies from the login client.
    /// </summary>
    private void TransferCookies(CookieContainer sourceContainer)
    {
        var cookies = sourceContainer.GetCookies(new Uri(BaseUrl));
        foreach (Cookie cookie in cookies)
        {
            _cookieContainer.Add(new Uri(BaseUrl),
                new Cookie(cookie.Name, cookie.Value, cookie.Path, cookie.Domain));
        }
        _logger.LogInformation("Transferred {Count} cookies from login client", cookies.Count);
    }

    /// <summary>
    /// Extract auth token from cookie container — DO NOT set Bearer header.
    /// </summary>
    private void ExtractAuthTokenFromCookie()
    {
        var cookies = _cookieContainer.GetCookies(new Uri(BaseUrl));
        foreach (Cookie cookie in cookies)
        {
            if (cookie.Name == "auth")
            {
                _authToken = cookie.Value;
                _logger.LogInformation("Auth token extracted from cookie container");
                return;
            }
        }
    }

    private void EnsureAuthenticated()
    {
        if (!IsAuthenticated)
        {
            throw new InvalidOperationException("Not authenticated. Call LoginAsync() first.");
        }
    }

    private static Friend MapUserToFriend(JsonElement userJson)
    {
        var friend = new Friend
        {
            Id = userJson.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
            DisplayName = userJson.TryGetProperty("displayName", out var name) ? name.GetString() ?? "" : "",
            Bio = userJson.TryGetProperty("bio", out var bio) ? bio.GetString() ?? "" : "",
            CurrentAvatarImageUrl = userJson.TryGetProperty("currentAvatarImageUrl", out var avatarUrl) ? avatarUrl.GetString() ?? "" : "",
            CurrentAvatarThumbnailImageUrl = userJson.TryGetProperty("currentAvatarThumbnailImageUrl", out var thumbUrl) ? thumbUrl.GetString() ?? "" : "",
            DeveloperType = userJson.TryGetProperty("developerType", out var devType) ? devType.GetString() ?? "" : "",
            FriendKey = userJson.TryGetProperty("friendKey", out var fKey) ? fKey.GetString() ?? "" : "",
            IsFriend = userJson.TryGetProperty("isFriend", out var isFriend) && isFriend.GetBoolean(),
            LastActivity = userJson.TryGetProperty("last_activity", out var lastAct) ? lastAct.GetString() ?? "" : "",
            LastLogin = userJson.TryGetProperty("last_login", out var lastLogin) ? lastLogin.GetString() ?? "" : "",
            LastMobile = userJson.TryGetProperty("last_mobile", out var lastMobile) ? (lastMobile.ValueKind == JsonValueKind.Null ? null : lastMobile.GetString()) : null,
            LastPlatform = userJson.TryGetProperty("last_platform", out var lastPlatform) ? lastPlatform.GetString() ?? "" : "",
            Location = userJson.TryGetProperty("location", out var location) ? location.GetString() ?? "" : "",
            Platform = userJson.TryGetProperty("platform", out var platform) ? platform.GetString() ?? "" : "",
            ProfilePicOverride = userJson.TryGetProperty("profilePicOverride", out var pfp) ? pfp.GetString() ?? "" : "",
            ProfilePicOverrideThumbnail = userJson.TryGetProperty("profilePicOverrideThumbnail", out var pfpThumb) ? pfpThumb.GetString() ?? "" : "",
            ImageUrl = userJson.TryGetProperty("imageUrl", out var imageUrl) ? imageUrl.GetString() ?? "" : "",
            Status = userJson.TryGetProperty("status", out var status) ? status.GetString() ?? "" : "",
            StatusDescription = userJson.TryGetProperty("statusDescription", out var statusDesc) ? statusDesc.GetString() ?? "" : "",
            UserIcon = userJson.TryGetProperty("userIcon", out var userIcon) ? userIcon.GetString() ?? "" : "",
            LastUpdated = DateTime.UtcNow
        };

        // Parse bio links
        if (userJson.TryGetProperty("bioLinks", out var bioLinks) && bioLinks.ValueKind == JsonValueKind.Array)
        {
            foreach (var link in bioLinks.EnumerateArray())
            {
                friend.BioLinks.Add(link.GetString() ?? "");
            }
        }

        // Parse tags
        if (userJson.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
        {
            foreach (var tag in tags.EnumerateArray())
            {
                friend.Tags.Add(tag.GetString() ?? "");
            }
        }

        // Parse current avatar tags
        if (userJson.TryGetProperty("currentAvatarTags", out var avatarTags) && avatarTags.ValueKind == JsonValueKind.Array)
        {
            foreach (var tag in avatarTags.EnumerateArray())
            {
                friend.CurrentAvatarTags.Add(tag.GetString() ?? "");
            }
        }

        // Derive state from location
        friend.State = DeriveState(friend.Location, friend.Status);

        return friend;
    }

    private static Notification MapNotification(JsonElement notifJson)
    {
        return new Notification
        {
            Id = notifJson.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
            Type = notifJson.TryGetProperty("type", out var type) ? type.GetString() ?? "" : "",
            SenderUserId = notifJson.TryGetProperty("senderUserId", out var sender) ? sender.GetString() ?? "" : "",
            ReceiverUserId = notifJson.TryGetProperty("receiverUserId", out var receiver) ? receiver.GetString() ?? "" : "",
            Message = notifJson.TryGetProperty("message", out var msg) ? msg.GetString() ?? "" : "",
            Seen = notifJson.TryGetProperty("seen", out var seen) && seen.GetBoolean(),
            CreatedAt = notifJson.TryGetProperty("created_at", out var createdAt)
                ? DateTime.TryParse(createdAt.GetString(), out var dt) ? dt : DateTime.UtcNow
                : DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        };
    }

    private static string DeriveState(string? location, string? status)
    {
        // Location "offline" or empty = offline
        if (string.IsNullOrEmpty(location) || location == "offline")
            return "offline";
        
        // Map VRChat status to our state values
        // For world locations, private, traveling - use the status field
        return status?.ToLower() switch
        {
            "active" => "active",
            "join me" => "joinme",
            "ask me" => "askme",
            "busy" => "busy",
            _ => "online"  // default for unknown statuses
        };
    }

    /// <summary>
    /// Fetch an image from a URL using the authenticated HttpClient.
    /// Returns the HttpResponseMessage so the caller can forward status code and content type.
    /// </summary>
    public async Task<HttpResponseMessage> GetImageAsync(string imageUrl)
    {
        try
        {
            var response = await _httpClient.GetAsync(imageUrl);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy image from {ImageUrl}", imageUrl);
            return new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway)
            {
                ReasonPhrase = "Failed to fetch image from upstream"
            };
        }
    }

    /// <summary>
    /// Search VRChat users.
    /// </summary>
    public async Task<List<Friend>> SearchUsersAsync(string query, int offset = 0, int n = 10)
    {
        EnsureAuthenticated();
        try
        {
            var url = $"{BaseUrl}users?search={Uri.EscapeDataString(query)}&offset={offset}&n={n}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var usersJson = JsonSerializer.Deserialize<List<JsonElement>>(json, _jsonOptions) ?? new();

            return usersJson.Select(MapUserToFriend).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search users");
            return new List<Friend>();
        }
    }

    /// <summary>
    /// Search VRChat worlds.
    /// </summary>
    public async Task<List<JsonElement>> SearchWorldsAsync(string query, int offset = 0, int n = 10)
    {
        EnsureAuthenticated();
        try
        {
            var url = $"{BaseUrl}worlds?search={Uri.EscapeDataString(query)}&offset={offset}&n={n}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<JsonElement>>(json, _jsonOptions) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search worlds");
            return new List<JsonElement>();
        }
    }

    /// <summary>
    /// Search for users by display name.
    /// </summary>
    public async Task<List<Friend>> SearchUsersAsync(string query, int offset = 0, int n = 10)
    {
        EnsureAuthenticated();
        try
        {
            var url = $"{BaseUrl}users?search={Uri.EscapeDataString(query)}&developerType=none&n={n}&offset={offset}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var usersJson = JsonSerializer.Deserialize<List<JsonElement>>(json, _jsonOptions) ?? new();
            return usersJson.Select(MapUserToFriend).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search users for {Query}", query);
            return new();
        }
    }

    /// <summary>
    /// Search for worlds by name.
    /// </summary>
    public async Task<List<JsonElement>> SearchWorldsAsync(string query, int offset = 0, int n = 10)
    {
        EnsureAuthenticated();
        try
        {
            var url = $"{BaseUrl}worlds?search={Uri.EscapeDataString(query)}&n={n}&offset={offset}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<JsonElement>>(json, _jsonOptions) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search worlds for {Query}", query);
            return new();
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _httpHandler.Dispose();
        GC.SuppressFinalize(this);
    }
}
