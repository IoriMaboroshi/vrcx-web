using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using VrcxServer.Models;
using VrcxServer.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080);
});

// Add services
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<VrchatApiService>();
builder.Services.AddSingleton<FriendService>();
builder.Services.AddHostedService<FriendService>();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddEndpointsApiExplorer();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Initialize database
var database = app.Services.GetRequiredService<DatabaseService>();
await database.InitializeAsync();

// Middleware
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

// WebSocket middleware
app.UseWebSockets();

// WebSocket endpoint
app.Map("/ws", async (HttpContext context) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var friendService = context.RequestServices.GetRequiredService<FriendService>();

        logger.LogInformation("WebSocket client connected");

        try
        {
            // Start a task to forward events from the channel to the WebSocket
            var receiveTask = ReceiveMessages(webSocket, logger);
            var sendTask = SendEvents(webSocket, friendService.EventChannel, logger);

            await Task.WhenAny(receiveTask, sendTask);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WebSocket error");
        }
        finally
        {
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            logger.LogInformation("WebSocket client disconnected");
        }
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

// API Endpoints

// GET /api/status
app.MapGet("/api/status", () => Results.Ok(new
{
    status = "ok",
    version = "1.0.0",
    timestamp = DateTime.UtcNow.ToString("o")
}));

// GET /api/config
app.MapGet("/api/config", async (VrchatApiService vrchatApi) =>
{
    var config = await vrchatApi.GetConfigAsync();
    if (config.HasValue)
    {
        return Results.Ok(new { success = true, data = config.Value });
    }
    return Results.Ok(new { success = true, data = new { message = "Config not available (not authenticated)" } });
});

// POST /api/auth/login
app.MapPost("/api/auth/login", async (LoginRequest request, VrchatApiService vrchatApi) =>
{
    if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
    {
        return Results.BadRequest(new { success = false, error = "Username and password are required" });
    }

    var result = await vrchatApi.LoginAsync(request.Username, request.Password);
    return Results.Ok(result);
});

// POST /api/auth/2fa
app.MapPost("/api/auth/2fa", async (TwoFactorRequest request, VrchatApiService vrchatApi) =>
{
    if (string.IsNullOrEmpty(request.Code))
    {
        return Results.BadRequest(new { success = false, error = "2FA code is required" });
    }

    var result = await vrchatApi.Verify2FAAsync(request.Code, request.Method);
    return Results.Ok(result);
});

// GET /api/friends
app.MapGet("/api/friends", async (VrchatApiService vrchatApi, int? offset, int? n, bool? offline) =>
{
    try
    {
        var friends = await vrchatApi.GetFriendsAsync(
            offset: offset ?? 0,
            n: n ?? 100,
            offline: offline ?? false
        );
        return Results.Ok(new { success = true, data = friends, count = friends.Count });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Unauthorized();
    }
});

// GET /api/friends/{userId}
app.MapGet("/api/friends/{userId}", async (string userId, VrchatApiService vrchatApi) =>
{
    try
    {
        var friend = await vrchatApi.GetFriendAsync(userId);
        if (friend == null)
        {
            return Results.NotFound(new { success = false, error = "Friend not found" });
        }
        return Results.Ok(new { success = true, data = friend });
    }
    catch (InvalidOperationException)
    {
        return Results.Unauthorized();
    }
});

// GET /api/worlds/{worldId}
app.MapGet("/api/worlds/{worldId}", async (string worldId, VrchatApiService vrchatApi) =>
{
    try
    {
        var world = await vrchatApi.GetWorldAsync(worldId);
        if (!world.HasValue)
        {
            return Results.NotFound(new { success = false, error = "World not found" });
        }
        return Results.Ok(new { success = true, data = world.Value });
    }
    catch (InvalidOperationException)
    {
        return Results.Unauthorized();
    }
});

// GET /api/notifications
app.MapGet("/api/notifications", async (VrchatApiService vrchatApi, int? offset, int? n, string? type) =>
{
    try
    {
        var notifications = await vrchatApi.GetNotificationsAsync(
            offset: offset ?? 0,
            n: n ?? 100,
            type: type
        );
        return Results.Ok(new { success = true, data = notifications, count = notifications.Count });
    }
    catch (InvalidOperationException)
    {
        return Results.Unauthorized();
    }
});

// GET /api/friend-events
app.MapGet("/api/friend-events", async (DatabaseService db, string? userId, string? eventType, int? offset, int? limit) =>
{
    var events = await db.GetFriendEventsAsync(
        userId: userId,
        eventType: eventType,
        offset: offset ?? 0,
        limit: limit ?? 100
    );
    return Results.Ok(new { success = true, data = events, count = events.Count });
});

// GET /api/friends-local
app.MapGet("/api/friends-local", async (DatabaseService db) =>
{
    var friends = await db.GetAllFriendsAsync();
    return Results.Ok(new { success = true, data = friends, count = friends.Count });
});

// GET /api/friends-local/{userId}
app.MapGet("/api/friends-local/{userId}", async (string userId, DatabaseService db) =>
{
    var friend = await db.GetFriendAsync(userId);
    if (friend == null)
    {
        return Results.NotFound(new { success = false, error = "Friend not found in local database" });
    }
    return Results.Ok(new { success = true, data = friend });
});

// ===== NEW ENDPOINTS =====

// GET /api/search/users
app.MapGet("/api/search/users", async (string q, int? offset, int? n, VrchatApiService vrchatApi) =>
{
    try { var users = await vrchatApi.SearchUsersAsync(q, offset ?? 0, n ?? 10); return Results.Ok(new { success = true, data = users }); }
    catch (InvalidOperationException) { return Results.Unauthorized(); }
});

// GET /api/search/worlds
app.MapGet("/api/search/worlds", async (string q, int? offset, int? n, VrchatApiService vrchatApi) =>
{
    try { var worlds = await vrchatApi.SearchWorldsAsync(q, offset ?? 0, n ?? 10); return Results.Ok(new { success = true, data = worlds }); }
    catch (InvalidOperationException) { return Results.Unauthorized(); }
});

// GET /api/worlds/{worldId}/instances
app.MapGet("/api/worlds/{worldId}/instances", async (string worldId, VrchatApiService vrchatApi) =>
{
    try
    {
        var instances = await vrchatApi.GetWorldInstancesAsync(worldId);
        if (!instances.HasValue)
            return Results.NotFound(new { success = false, error = "World not found" });
        return Results.Ok(new { success = true, data = instances.Value });
    }
    catch (InvalidOperationException) { return Results.Unauthorized(); }
});

// GET /api/instances/{worldId}/{instanceId}
app.MapGet("/api/instances/{worldId}/{instanceId}", async (string worldId, string instanceId, VrchatApiService vrchatApi) =>
{
    try
    {
        var instance = await vrchatApi.GetInstanceAsync(worldId, instanceId);
        if (!instance.HasValue)
            return Results.NotFound(new { success = false, error = "Instance not found" });
        return Results.Ok(new { success = true, data = instance.Value });
    }
    catch (InvalidOperationException) { return Results.Unauthorized(); }
});

// POST /api/notifications/{id}/accept
app.MapPost("/api/notifications/{id}/accept", async (string id, VrchatApiService vrchatApi) =>
{
    var result = await vrchatApi.AcceptNotificationAsync(id);
    return Results.Ok(new { success = result });
});

// POST /api/notifications/{id}/decline
app.MapPost("/api/notifications/{id}/decline", async (string id, VrchatApiService vrchatApi) =>
{
    var result = await vrchatApi.DeclineNotificationAsync(id);
    return Results.Ok(new { success = result });
});

// DELETE /api/notifications/{id}
app.MapDelete("/api/notifications/{id}", async (string id, VrchatApiService vrchatApi) =>
{
    var result = await vrchatApi.DeleteNotificationAsync(id);
    return Results.Ok(new { success = result });
});

// GET /api/notes/{friendId}
app.MapGet("/api/notes/{friendId}", async (string friendId, DatabaseService db) =>
{
    var note = await db.GetUserNoteAsync(friendId);
    return Results.Ok(new { success = true, data = new { note = note ?? "" } });
});

// PUT /api/notes/{friendId}
app.MapPut("/api/notes/{friendId}", async (string friendId, NoteRequest request, DatabaseService db) =>
{
    await db.SaveUserNoteAsync(friendId, request.Note);
    return Results.Ok(new { success = true });
});

// GET /api/settings
app.MapGet("/api/settings", async (DatabaseService db) =>
{
    var settings = await db.GetSettingsAsync();
    return Results.Ok(new { success = true, data = settings });
});

// PUT /api/settings
app.MapPut("/api/settings", async (SettingsRequest request, DatabaseService db) =>
{
    await db.SaveSettingsAsync(request.Settings);
    return Results.Ok(new { success = true });
});

// ===== END NEW ENDPOINTS =====

// GET /api/image-proxy/{encodedUrl} — proxy images through authenticated VRChat session
app.MapGet("/api/image-proxy/{encodedUrl}", async (string encodedUrl, VrchatApiService vrchatApi) =>
{
    string decodedUrl;
    try
    {
        var base64 = encodedUrl.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        decodedUrl = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }
    catch
    {
        return Results.BadRequest(new { success = false, error = "Invalid base64url encoding" });
    }

    if (string.IsNullOrEmpty(decodedUrl) || !Uri.TryCreate(decodedUrl, UriKind.Absolute, out _))
    {
        return Results.BadRequest(new { success = false, error = "Invalid URL" });
    }

    var response = await vrchatApi.GetImageAsync(decodedUrl);
    if (!response.IsSuccessStatusCode)
    {
        return Results.StatusCode((int)response.StatusCode);
    }

    var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/png";
    var imageBytes = await response.Content.ReadAsByteArrayAsync();
    return Results.File(imageBytes, contentType);
});

// GET /api/search/users
app.MapGet("/api/search/users", async (string q, VrchatApiService vrchatApi, int? offset, int? n) =>
{
    try
    {
        var results = await vrchatApi.SearchUsersAsync(q, offset ?? 0, n ?? 10);
        return Results.Ok(new { success = true, data = results });
    }
    catch (InvalidOperationException) { return Results.Unauthorized(); }
});

// GET /api/search/worlds
app.MapGet("/api/search/worlds", async (string q, VrchatApiService vrchatApi, int? offset, int? n) =>
{
    try
    {
        var results = await vrchatApi.SearchWorldsAsync(q, offset ?? 0, n ?? 10);
        return Results.Ok(new { success = true, data = results });
    }
    catch (InvalidOperationException) { return Results.Unauthorized(); }
});

// Map controllers (DotnetProxyController)
app.MapControllers();

// Fallback to index.html for SPA routing
app.MapFallbackToFile("index.html");

app.Run();

// WebSocket helper methods
static async Task ReceiveMessages(WebSocket webSocket, ILogger logger)
{
    var buffer = new byte[1024 * 4];
    try
    {
        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }
            // Handle incoming messages if needed (e.g., commands from client)
            var message = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
            logger.LogDebug("WebSocket received: {Message}", message);
        }
    }
    catch (Exception ex)
    {
        logger.LogDebug(ex, "WebSocket receive ended");
    }
}

static async Task SendEvents(WebSocket webSocket, System.Threading.Channels.Channel<FriendEvent> channel, ILogger logger)
{
    try
    {
        await foreach (var friendEvent in channel.Reader.ReadAllAsync())
        {
            if (webSocket.State != WebSocketState.Open) break;

            var json = JsonSerializer.Serialize(new
            {
                type = "friend_event",
                data = friendEvent
            }, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            await webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );
        }
    }
    catch (Exception ex)
    {
        logger.LogDebug(ex, "WebSocket send ended");
    }
}

// Request models
record LoginRequest(string Username, string Password);
record TwoFactorRequest(string Code, string Method = "totp");
record NoteRequest(string Note);
record SettingsRequest(Dictionary<string, string> Settings);
