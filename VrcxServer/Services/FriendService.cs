using System.Threading.Channels;
using System.Text.Json;
using VrcxServer.Models;

namespace VrcxServer.Services;

/// <summary>
/// Background service that polls VRChat friend list and detects status changes.
/// Publishes events to connected WebSocket clients.
/// </summary>
public class FriendService : BackgroundService
{
    private readonly ILogger<FriendService> _logger;
    private readonly VrchatApiService _vrchatApi;
    private readonly DatabaseService _database;
    private readonly Channel<FriendEvent> _eventChannel;
    private readonly Dictionary<string, Friend> _previousState = new();
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Event channel for broadcasting friend events to WebSocket clients.
    /// </summary>
    public Channel<FriendEvent> EventChannel => _eventChannel;

    public FriendService(
        ILogger<FriendService> logger,
        VrchatApiService vrchatApi,
        DatabaseService database,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _vrchatApi = vrchatApi;
        _database = database;
        _scopeFactory = scopeFactory;
        _eventChannel = Channel.CreateUnbounded<FriendEvent>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromSeconds(60);

        _logger.LogInformation("FriendService started. Polling every {Interval}s", pollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_vrchatApi.IsAuthenticated)
                {
                    await Task.Delay(pollInterval, stoppingToken);
                    continue;
                }

                await PollFriendsAsync();
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during friend poll cycle");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        _logger.LogInformation("FriendService stopped");
    }

    /// <summary>
    /// Poll the VRChat friend list and detect changes.
    /// </summary>
    private async Task PollFriendsAsync()
    {
        _logger.LogDebug("Polling friends...");

        var allFriends = new List<Friend>();
        var offset = 0;
        const int pageSize = 100;

        // Fetch all online friends first
        while (true)
        {
            var friends = await _vrchatApi.GetFriendsAsync(offset: offset, n: pageSize, offline: false);
            if (friends.Count == 0) break;
            allFriends.AddRange(friends);
            if (friends.Count < pageSize) break;
            offset += pageSize;
        }

        // Fetch offline friends
        offset = 0;
        while (true)
        {
            var friends = await _vrchatApi.GetFriendsAsync(offset: offset, n: pageSize, offline: true);
            if (friends.Count == 0) break;
            allFriends.AddRange(friends);
            if (friends.Count < pageSize) break;
            offset += pageSize;
        }

        _logger.LogDebug("Fetched {Count} friends", allFriends.Count);

        // Detect changes and generate events
        var events = new List<FriendEvent>();
        var currentIds = new HashSet<string>();

        foreach (var friend in allFriends)
        {
            currentIds.Add(friend.Id);

            if (_previousState.TryGetValue(friend.Id, out var previous))
            {
                // Check for online/offline status change
                if (previous.State != friend.State)
                {
                    var eventType = friend.State == "offline" ? "offline" : "online";

                    events.Add(new FriendEvent
                    {
                        UserId = friend.Id,
                        DisplayName = friend.DisplayName,
                        EventType = eventType,
                        OldValue = previous.State,
                        NewValue = friend.State,
                        WorldName = await ResolveWorldNameAsync(friend.Location),
                        Timestamp = DateTime.UtcNow
                    });
                }

                // Check for status change
                if (previous.Status != friend.Status)
                {
                    events.Add(new FriendEvent
                    {
                        UserId = friend.Id,
                        DisplayName = friend.DisplayName,
                        EventType = "status_change",
                        OldValue = previous.Status,
                        NewValue = friend.Status,
                        Timestamp = DateTime.UtcNow
                    });
                }

                // Check for location change
                if (previous.Location != friend.Location)
                {
                    events.Add(new FriendEvent
                    {
                        UserId = friend.Id,
                        DisplayName = friend.DisplayName,
                        EventType = "location_change",
                        OldValue = previous.Location,
                        NewValue = friend.Location,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }

            // Save to database
            await _database.SaveFriendAsync(friend);
        }

        // Save events and broadcast
        foreach (var friendEvent in events)
        {
            await _database.SaveFriendEventAsync(friendEvent);

            // Write to channel for WebSocket broadcast
            _eventChannel.Writer.TryWrite(friendEvent);

            _logger.LogInformation("Friend event: {EventType} - {DisplayName} ({Old} -> {New})",
                friendEvent.EventType,
                friendEvent.DisplayName,
                friendEvent.OldValue,
                friendEvent.NewValue);
        }

        // Update previous state
        _previousState.Clear();
        foreach (var friend in allFriends)
        {
            _previousState[friend.Id] = friend;
        }

        _logger.LogDebug("Poll complete. {EventCount} events detected", events.Count);
    }

    private async Task<string> ResolveWorldNameAsync(string? location)
    {
        if (string.IsNullOrEmpty(location) || !location.StartsWith("wrld_"))
            return location ?? "Unknown";

        var worldId = location.Split(':')[0];

        try
        {
            var worldData = await _vrchatApi.GetWorldAsync(worldId);
            if (worldData.HasValue)
            {
                var name = worldData.Value.GetProperty("name").GetString();
                if (!string.IsNullOrEmpty(name))
                    return name;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve world name for {WorldId}", worldId);
        }

        return worldId;
    }
}
