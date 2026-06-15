# Fix 2FA + Auth Cookie Transfer

ulw

## Reference First
Read `/workspace/vrcx-server/vrcx-reference/Dotnet/WebApi.cs` — this is VRCX's actual HTTP client implementation.

Key facts from VRCX source:
1. VRCX uses `SocketsHttpHandler` with `CookieContainer` + `UseCookies = true`
2. VRCX does NOT set any `Authorization: Bearer` header on `_httpClient.DefaultRequestHeaders`
3. Auth is purely cookie-based — VRChat sets `auth` cookie on login, and all subsequent requests use that cookie automatically via `CookieContainer`
4. The frontend passes `Authorization: Basic xxx` header in the request options for the login call only

## Current Bug
Our `VrchatApiService` sets `Authorization: Bearer` header on `_httpClient.DefaultRequestHeaders` after login. VRChat's WAF detects BOTH cookies AND Bearer header and returns 403 Forbidden.

## Fix Required
Modify `VrcxServer/Services/VrchatApiService.cs`:

### 1. Constructor — use SocketsHttpHandler (like VRCX)
```csharp
// Match VRCX WebApi.cs exactly
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
// DO NOT set Authorization header
```

### 2. LoginAsync — keep separate client for Basic auth, transfer cookie after
```csharp
public async Task<AuthResult> LoginAsync(string username, string password)
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

    var request = new HttpRequestMessage(HttpMethod.Get, "https://api.vrchat.cloud/api/1/auth/user");
    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authBase64);

    var response = await loginClient.SendAsync(request);
    var json = await response.Content.ReadAsStringAsync();

    if (response.StatusCode == HttpStatusCode.OK)
    {
        var userJson = JsonSerializer.Deserialize<JsonElement>(json, _jsonOptions);
        if (userJson.TryGetProperty("requiresTwoFactorAuth", out var twoFactorElement))
        {
            // 2FA required — transfer auth cookie from loginClient to shared container
            TransferCookies(loginHandler.CookieContainer);
            // Store current user JSON for later
            _currentUserJson = json;
            var methods = twoFactorElement.EnumerateArray().Select(m => m.GetString() ?? "").ToList();
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
        return new AuthResult
        {
            Success = true,
            AuthToken = _authToken,
            CurrentUser = MapUserToFriend(userJson)
        };
    }

    // Parse error...
}

private void TransferCookies(CookieContainer sourceContainer)
{
    // Transfer all cookies from source to shared _cookieContainer
    var cookies = sourceContainer.GetCookies(new Uri("https://api.vrchat.cloud/api/1/"));
    foreach (Cookie cookie in cookies)
    {
        _cookieContainer.Add(new Uri("https://api.vrchat.cloud/api/1/"), 
            new Cookie(cookie.Name, cookie.Value, cookie.Path, cookie.Domain));
    }
    _logger.LogInformation("Transferred {Count} cookies from login client", cookies.Count);
}

private void ExtractAuthTokenFromCookie()
{
    // Extract auth token from cookie container — DO NOT set Bearer header
    var cookies = _cookieContainer.GetCookies(new Uri("https://api.vrchat.cloud/api/1/"));
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
```

### 3. Verify2FAAsync — use _httpClient (cookies already transferred)
```csharp
public async Task<AuthResult> Verify2FAAsync(string code, string method = "totp")
{
    string endpoint = method.ToLower() switch
    {
        "otp" => "https://api.vrchat.cloud/api/1/auth/twofactorauth/otp/verify",
        "emailotp" or "email" => "https://api.vrchat.cloud/api/1/auth/twofactorauth/emailotp/verify",
        _ => "https://api.vrchat.cloud/api/1/auth/twofactorauth/totp/verify"
    };

    var requestBody = JsonSerializer.Serialize(new { code }, _jsonOptions);
    var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

    // Use _httpClient — cookies were transferred from login
    var response = await _httpClient.PostAsync(endpoint, content);
    var json = await response.Content.ReadAsStringAsync();

    if (response.IsSuccessStatusCode)
    {
        var resultJson = JsonSerializer.Deserialize<JsonElement>(json, _jsonOptions);
        if (resultJson.TryGetProperty("requiresTwoFactorAuth", out _))
        {
            return new AuthResult { Success = false, RequiresTwoFactorAuth = true };
        }

        // 2FA succeeded — extract auth cookie
        ExtractAuthTokenFromCookie();

        // Get current user
        var userResponse = await _httpClient.GetAsync("https://api.vrchat.cloud/api/1/auth/user");
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

    return new AuthResult { Success = false, ErrorMessage = $"2FA failed: HTTP {(int)response.StatusCode}" };
}
```

### 4. All API methods — use full URLs, no BaseAddress
Since we removed `BaseAddress`, all API calls must use full URLs:
- `"auth/user/friends?..."` → `"https://api.vrchat.cloud/api/1/auth/user/friends?..."`
- `"user/{userId}"` → `"https://api.vrchat.cloud/api/1/users/{userId}"`
- etc.

### 5. SetAuthToken — do NOT set Bearer header
```csharp
public void SetAuthToken(string token)
{
    _authToken = token;
    // Add as cookie instead of Bearer header
    _cookieContainer.Add(new Uri("https://api.vrchat.cloud/api/1/"), new Cookie("auth", token));
    // DO NOT: _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
}
```

## CRITICAL RULES
1. NEVER set `Authorization: Bearer` header on `_httpClient` — VRChat WAF blocks it
2. ALL auth is cookie-based via `CookieContainer`
3. Use full URLs in all API calls (no BaseAddress)
4. Reference VRCX `WebApi.cs` for the correct pattern
5. After `opencode run`, build backend: `cd /workspace/vrcx-server/VrcxServer && dotnet publish -c Release`
6. Build frontend: `cd /workspace/vrcx-server/frontend-src && npm run build && cp -r dist/* ../VrcxServer/wwwroot/`
7. Copy to host projects and rebuild Docker image
