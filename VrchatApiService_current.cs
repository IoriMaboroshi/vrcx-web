using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VrcxServer.Models;

namespace VrcxServer.Services;

/// <summary>
/// HTTP client wrapper for the VRChat API (https://api.vrchat.cloud/api/1).
/// Handles authentication, cookie persistence, and all API interactions.
/// </summary>
public class VrchatApiService : IDisposable
{
    private readonly ILogger<VrchatApiService> _logger;
    private readonly HttpClient _httpClient;
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
        var handler = new HttpClientHandler
        {
            CookieContainer = _cookieContainer,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.All
        };
        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

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
            var response = await _httpClient.GetAsync("config");
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
    /// Uses Basic auth header. Returns auth token in response header.
    /// </summary>
    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        try
        {
            // Build Basic auth header
            var authString = $"{Uri.EscapeDataString(username)}:{Uri.EscapeDataString(password)}";
            var authBytes = Encoding.UTF8.GetBytes(authString);
            var authBase64 = Convert.ToBase64String(authBytes);

            var request = new HttpRequestMessage(HttpMethod.Get, "auth/user");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authBase64);

            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            // Check if 2FA is required
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var userJson = JsonSerializer.Deserialize<JsonElement>(json, _jsonOptions);

                if (userJson.TryGetProperty("requiresTwoFactorAuth", out var twoFactorElement))
                {
                    // 2FA required - extract methods
                    var methods = new List<string>();
                    foreach (var method in twoFactorElement.EnumerateArray())
                    {
                        methods.Add(method.GetString() ?? "");
                    }
                    _currentUserJson = json;

                    return new AuthResult
                    {
                        Success = false,
                        RequiresTwoFactorAuth = true,
                        TwoFactorAuthMethods = methods
                    };
                }

                // No 2FA needed - extract auth token
                ExtractAuthToken(response);
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
    /// </summary>
    public async Task<AuthResult> Verify2FAAsync(string code, string method = "totp")
    {
        try
        {
            string endpoint = method.ToLower() switch
            {
                "otp" => "auth/twofactorauth/otp/verify",
                "emailotp" or "email" => "auth/twofactorauth/emailotp/verify",
                _ => "auth/twofactorauth/totp/verify"
            };

            _logger.LogInformation("2FA verify: method={Method}, endpoint={Endpoint}, code length={CodeLen}", method, endpoint, code.Length);

            var requestBody = JsonSerializer.Serialize(new { code }, _jsonOptions);
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, content);
            var json = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("2FA verify response: status={Status}, body={Body}", response.StatusCode, json.Length > 500 ? json[..500] : json);

            if (response.IsSuccessStatusCode)
            {
                // Check if still needs 2FA
                var resultJson = JsonSerializer.Deserialize<JsonElement>(json, _jsonOptions);
                if (resultJson.TryGetProperty("requiresTwoFactorAuth", out _))
                {
                    _logger.LogWarning("2FA verify: still requires 2FA");
                    return new AuthResult
                    {
                        Success = false,
                        RequiresTwoFactorAuth = true,
                        TwoFactorAuthMethods = new List<string> { "totp", "otp", "emailotp" }
                    };
                }

                // Try to get current user after 2FA
                ExtractAuthToken(response);

                _logger.LogInformation("2FA verify: auth token extracted={HasToken}, trying auth/user", _authToken != null);

                var userRequest = CreateAuthRequest(HttpMethod.Get, "auth/user");
                var userResponse = await _httpClient.SendAsync(userRequest);
                var userJson2 = await userResponse.Content.ReadAsStringAsync();
                _logger.LogInformation("2FA verify: auth/user status={Status}, body={Body}", userResponse.StatusCode, userJson2.Length > 300 ? userJson2[..300] : userJson2);

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

            _logger.LogWarning("2FA verify failed: status={Status}, body={Body}", response.StatusCode, json);
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
            var url = $"auth/user/friends?offset={offset}&n={n}&offline={offline.ToString().ToLower()}";
            var request = CreateAuthRequest(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request);

            var json = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("GetFriends: status={Status}, body preview={Body}", response.StatusCode, json.Length > 200 ? json[..200] : json);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GetFriends failed: {Status} - {Body}", response.StatusCode, json.Length > 500 ? json[..500] : json);
                return new List<Friend>();
            }

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
            var response = await _httpClient.GetAsync($"user/{userId}");
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
            var response = await _httpClient.GetAsync($"worlds/{worldId}");
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

    /// <summary>
    /// Get notifications from VRChat API.
    /// </summary>
    public async Task<List<Notification>> GetNotificationsAsync(int offset = 0, int n = 100, string? type = null)
    {
        EnsureAuthenticated();
        try
        {
            var url = $"auth/user/notifications?offset={offset}&n={n}";
            if (!string.IsNullOrEmpty(type))
            {
                url += $"&type={Uri.EscapeDataString(type)}";
            }

            var request = CreateAuthRequest(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errJson = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("GetNotifications failed: {Status} - {Body}", response.StatusCode, errJson.Length > 300 ? errJson[..300] : errJson);
                return new List<Notification>();
            }

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

    /// <summary>
    /// Get the current user info.
    /// </summary>
    public async Task<Friend?> GetCurrentUserAsync()
    {
        EnsureAuthenticated();
        try
        {
            var request = CreateAuthRequest(HttpMethod.Get, "auth/user");
            var response = await _httpClient.SendAsync(request);
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
    /// </summary>
    public void SetAuthToken(string token)
    {
        _authToken = token;
    }

    private void ExtractAuthToken(HttpResponseMessage response)
    {
        // VRChat uses auth cookie, not Bearer token.
        // With UseCookies=false, cookies come through Set-Cookie headers.
        if (response.Headers.Contains("Set-Cookie"))
        {
            var cookies = response.Headers.GetValues("Set-Cookie");
            foreach (var cookie in cookies)
            {
                if (cookie.Contains("auth="))
                {
                    var authPart = cookie.Split(';')[0]; // Get just the name=value
                    if (authPart.StartsWith("auth="))
                    {
                        _authToken = authPart[5..]; // Remove "auth=" prefix
                        _logger.LogInformation("Auth token extracted from Set-Cookie header");
                        return;
                    }
                }
            }
        }

        // Fallback: check cookie container
        var cookiesFromContainer = _cookieContainer.GetCookies(new Uri("https://api.vrchat.cloud/"));
        foreach (Cookie cookie in cookiesFromContainer)
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

    /// <summary>
    /// Create an authenticated HTTP request. Sets both Cookie and Authorization header.
    /// </summary>
    private HttpRequestMessage CreateAuthRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(_authToken))
        {
            request.Headers.Add("Cookie", $"auth={_authToken}");
        }
        return request;
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
        if (string.IsNullOrEmpty(location) || location == "offline" || location == "private")
        {
            return "offline";
        }
        if (status == "active")
        {
            return "active";
        }
        // If location contains "wrld_" they're in a world
        if (location.StartsWith("wrld_"))
        {
            return "online";
        }
        return "offline";
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
