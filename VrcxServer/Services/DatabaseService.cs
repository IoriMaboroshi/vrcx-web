using Microsoft.Data.Sqlite;
using VrcxServer.Models;

namespace VrcxServer.Services;

/// <summary>
/// SQLite database manager for VRCX Server.
/// Handles all database operations including table creation and CRUD.
/// </summary>
public class DatabaseService : IDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseService> _logger;
    private SqliteConnection? _connection;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public DatabaseService(IConfiguration configuration, ILogger<DatabaseService> logger)
    {
        _logger = logger;
        var dataDir = configuration.GetValue<string>("DataDirectory") ?? "data";
        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "vrcx.db");
        _connectionString = $"Data Source={dbPath}";
        _logger.LogInformation("Database path: {Path}", dbPath);
    }

    /// <summary>
    /// Initialize the database connection and create tables if they don't exist.
    /// </summary>
    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection(_connectionString);
        await _connection.OpenAsync();

        await using var command = _connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS friends (
                id TEXT PRIMARY KEY,
                display_name TEXT NOT NULL,
                bio TEXT DEFAULT '',
                bio_links TEXT DEFAULT '[]',
                current_avatar_image_url TEXT DEFAULT '',
                current_avatar_thumbnail_image_url TEXT DEFAULT '',
                developer_type TEXT DEFAULT '',
                friend_key TEXT DEFAULT '',
                is_friend INTEGER DEFAULT 0,
                last_activity TEXT DEFAULT '',
                last_login TEXT DEFAULT '',
                last_mobile TEXT,
                last_platform TEXT DEFAULT '',
                location TEXT DEFAULT '',
                platform TEXT DEFAULT '',
                profile_pic_override TEXT DEFAULT '',
                status TEXT DEFAULT '',
                status_description TEXT DEFAULT '',
                tags TEXT DEFAULT '[]',
                user_icon TEXT DEFAULT '',
                current_avatar_tags TEXT DEFAULT '[]',
                state TEXT DEFAULT 'offline',
                last_updated TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS friend_events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id TEXT NOT NULL,
                display_name TEXT NOT NULL,
                event_type TEXT NOT NULL,
                old_value TEXT,
                new_value TEXT,
                world_name TEXT,
                timestamp TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS notifications (
                id TEXT PRIMARY KEY,
                type TEXT NOT NULL,
                sender_user_id TEXT NOT NULL,
                receiver_user_id TEXT NOT NULL,
                message TEXT DEFAULT '',
                details TEXT,
                seen INTEGER DEFAULT 0,
                created_at TEXT NOT NULL,
                last_updated TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS sessions (
                id TEXT PRIMARY KEY,
                auth_token TEXT,
                user_id TEXT,
                created_at TEXT NOT NULL,
                last_active TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_friend_events_user_id ON friend_events(user_id);
            CREATE INDEX IF NOT EXISTS idx_friend_events_timestamp ON friend_events(timestamp);
            CREATE INDEX IF NOT EXISTS idx_notifications_receiver ON notifications(receiver_user_id);
            CREATE INDEX IF NOT EXISTS idx_notifications_type ON notifications(type);

            CREATE TABLE IF NOT EXISTS world_cache (
                world_id TEXT PRIMARY KEY,
                world_name TEXT NOT NULL,
                author_name TEXT,
                description TEXT,
                thumbnail_url TEXT,
                capacity INTEGER,
                last_updated TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS friend_groups (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                color TEXT,
                sort_order INTEGER DEFAULT 0,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS friend_group_members (
                group_id TEXT NOT NULL,
                user_id TEXT NOT NULL,
                added_at TEXT NOT NULL,
                PRIMARY KEY (group_id, user_id),
                FOREIGN KEY (group_id) REFERENCES friend_groups(id)
            );

            CREATE TABLE IF NOT EXISTS user_notes (
                user_id TEXT PRIMARY KEY,
                note TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
        ";

        await command.ExecuteNonQueryAsync();
        _logger.LogInformation("Database initialized successfully");
    }

    /// <summary>
    /// Save or update a friend record.
    /// </summary>
    public async Task SaveFriendAsync(Friend friend)
    {
        await _semaphore.WaitAsync();
        try
        {
            EnsureConnection();
            await using var command = _connection!.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO friends (
                    id, display_name, bio, bio_links, current_avatar_image_url,
                    current_avatar_thumbnail_image_url, developer_type, friend_key,
                    is_friend, last_activity, last_login, last_mobile, last_platform,
                    location, platform, profile_pic_override, status, status_description,
                    tags, user_icon, current_avatar_tags, state, last_updated
                ) VALUES (
                    @id, @displayName, @bio, @bioLinks, @currentAvatarImageUrl,
                    @currentAvatarThumbnailImageUrl, @developerType, @friendKey,
                    @isFriend, @lastActivity, @lastLogin, @lastMobile, @lastPlatform,
                    @location, @platform, @profilePicOverride, @status, @statusDescription,
                    @tags, @userIcon, @currentAvatarTags, @state, @lastUpdated
                )";

            command.Parameters.AddWithValue("@id", friend.Id);
            command.Parameters.AddWithValue("@displayName", friend.DisplayName);
            command.Parameters.AddWithValue("@bio", friend.Bio ?? "");
            command.Parameters.AddWithValue("@bioLinks", System.Text.Json.JsonSerializer.Serialize(friend.BioLinks));
            command.Parameters.AddWithValue("@currentAvatarImageUrl", friend.CurrentAvatarImageUrl ?? "");
            command.Parameters.AddWithValue("@currentAvatarThumbnailImageUrl", friend.CurrentAvatarThumbnailImageUrl ?? "");
            command.Parameters.AddWithValue("@developerType", friend.DeveloperType ?? "");
            command.Parameters.AddWithValue("@friendKey", friend.FriendKey ?? "");
            command.Parameters.AddWithValue("@isFriend", friend.IsFriend ? 1 : 0);
            command.Parameters.AddWithValue("@lastActivity", friend.LastActivity ?? "");
            command.Parameters.AddWithValue("@lastLogin", friend.LastLogin ?? "");
            command.Parameters.AddWithValue("@lastMobile", (object?)friend.LastMobile ?? DBNull.Value);
            command.Parameters.AddWithValue("@lastPlatform", friend.LastPlatform ?? "");
            command.Parameters.AddWithValue("@location", friend.Location ?? "");
            command.Parameters.AddWithValue("@platform", friend.Platform ?? "");
            command.Parameters.AddWithValue("@profilePicOverride", friend.ProfilePicOverride ?? "");
            command.Parameters.AddWithValue("@status", friend.Status ?? "");
            command.Parameters.AddWithValue("@statusDescription", friend.StatusDescription ?? "");
            command.Parameters.AddWithValue("@tags", System.Text.Json.JsonSerializer.Serialize(friend.Tags));
            command.Parameters.AddWithValue("@userIcon", friend.UserIcon ?? "");
            command.Parameters.AddWithValue("@currentAvatarTags", System.Text.Json.JsonSerializer.Serialize(friend.CurrentAvatarTags));
            command.Parameters.AddWithValue("@state", friend.State ?? "offline");
            command.Parameters.AddWithValue("@lastUpdated", friend.LastUpdated.ToString("o"));

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Get a friend by ID.
    /// </summary>
    public async Task<Friend?> GetFriendAsync(string userId)
    {
        await _semaphore.WaitAsync();
        try
        {
            EnsureConnection();
            await using var command = _connection!.CreateCommand();
            command.CommandText = "SELECT * FROM friends WHERE id = @id";
            command.Parameters.AddWithValue("@id", userId);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapFriend(reader);
            }
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Get all friends from the database.
    /// </summary>
    public async Task<List<Friend>> GetAllFriendsAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            EnsureConnection();
            await using var command = _connection!.CreateCommand();
            command.CommandText = "SELECT * FROM friends ORDER BY display_name";

            var friends = new List<Friend>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                friends.Add(MapFriend(reader));
            }
            return friends;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Save a friend event.
    /// </summary>
    public async Task SaveFriendEventAsync(FriendEvent friendEvent)
    {
        await _semaphore.WaitAsync();
        try
        {
            EnsureConnection();
            await using var command = _connection!.CreateCommand();
            command.CommandText = @"
                INSERT INTO friend_events (user_id, display_name, event_type, old_value, new_value, world_name, timestamp)
                VALUES (@userId, @displayName, @eventType, @oldValue, @newValue, @worldName, @timestamp)";

            command.Parameters.AddWithValue("@userId", friendEvent.UserId);
            command.Parameters.AddWithValue("@displayName", friendEvent.DisplayName);
            command.Parameters.AddWithValue("@eventType", friendEvent.EventType);
            command.Parameters.AddWithValue("@oldValue", (object?)friendEvent.OldValue ?? DBNull.Value);
            command.Parameters.AddWithValue("@newValue", (object?)friendEvent.NewValue ?? DBNull.Value);
            command.Parameters.AddWithValue("@worldName", (object?)friendEvent.WorldName ?? DBNull.Value);
            command.Parameters.AddWithValue("@timestamp", friendEvent.Timestamp.ToString("o"));

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Get friend events, optionally filtered by user ID and event type.
    /// </summary>
    public async Task<List<FriendEvent>> GetFriendEventsAsync(string? userId = null, string? eventType = null, int offset = 0, int limit = 100)
    {
        await _semaphore.WaitAsync();
        try
        {
            EnsureConnection();
            await using var command = _connection!.CreateCommand();

            var conditions = new List<string>();
            if (!string.IsNullOrEmpty(userId))
            {
                conditions.Add("user_id = @userId");
                command.Parameters.AddWithValue("@userId", userId);
            }
            if (!string.IsNullOrEmpty(eventType))
            {
                conditions.Add("event_type = @eventType");
                command.Parameters.AddWithValue("@eventType", eventType);
            }

            var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
            command.CommandText = $"SELECT * FROM friend_events {whereClause} ORDER BY timestamp DESC LIMIT @limit OFFSET @offset";
            command.Parameters.AddWithValue("@limit", limit);
            command.Parameters.AddWithValue("@offset", offset);

            var events = new List<FriendEvent>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                events.Add(MapFriendEvent(reader));
            }
            return events;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Save a notification.
    /// </summary>
    public async Task SaveNotificationAsync(Notification notification)
    {
        await _semaphore.WaitAsync();
        try
        {
            EnsureConnection();
            await using var command = _connection!.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO notifications (
                    id, type, sender_user_id, receiver_user_id, message, details, seen, created_at, last_updated
                ) VALUES (
                    @id, @type, @senderUserId, @receiverUserId, @message, @details, @seen, @createdAt, @lastUpdated
                )";

            command.Parameters.AddWithValue("@id", notification.Id);
            command.Parameters.AddWithValue("@type", notification.Type);
            command.Parameters.AddWithValue("@senderUserId", notification.SenderUserId);
            command.Parameters.AddWithValue("@receiverUserId", notification.ReceiverUserId);
            command.Parameters.AddWithValue("@message", notification.Message ?? "");
            command.Parameters.AddWithValue("@details", notification.Details != null ? System.Text.Json.JsonSerializer.Serialize(notification.Details) : DBNull.Value);
            command.Parameters.AddWithValue("@seen", notification.Seen ? 1 : 0);
            command.Parameters.AddWithValue("@createdAt", notification.CreatedAt.ToString("o"));
            command.Parameters.AddWithValue("@lastUpdated", notification.LastUpdated.ToString("o"));

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Get notifications, optionally filtered by type and seen status.
    /// </summary>
    public async Task<List<Notification>> GetNotificationsAsync(string? type = null, bool? seen = null, int offset = 0, int limit = 100)
    {
        await _semaphore.WaitAsync();
        try
        {
            EnsureConnection();
            await using var command = _connection!.CreateCommand();

            var conditions = new List<string>();
            if (!string.IsNullOrEmpty(type))
            {
                conditions.Add("type = @type");
                command.Parameters.AddWithValue("@type", type);
            }
            if (seen.HasValue)
            {
                conditions.Add("seen = @seen");
                command.Parameters.AddWithValue("@seen", seen.Value ? 1 : 0);
            }

            var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
            command.CommandText = $"SELECT * FROM notifications {whereClause} ORDER BY created_at DESC LIMIT @limit OFFSET @offset";
            command.Parameters.AddWithValue("@limit", limit);
            command.Parameters.AddWithValue("@offset", offset);

            var notifications = new List<Notification>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                notifications.Add(MapNotification(reader));
            }
            return notifications;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static Friend MapFriend(SqliteDataReader reader)
    {
        return new Friend
        {
            Id = reader.GetString(reader.GetOrdinal("id")),
            DisplayName = reader.GetString(reader.GetOrdinal("display_name")),
            Bio = reader.GetString(reader.GetOrdinal("bio")),
            BioLinks = System.Text.Json.JsonSerializer.Deserialize<List<string>>(reader.GetString(reader.GetOrdinal("bio_links"))) ?? new(),
            CurrentAvatarImageUrl = reader.GetString(reader.GetOrdinal("current_avatar_image_url")),
            CurrentAvatarThumbnailImageUrl = reader.GetString(reader.GetOrdinal("current_avatar_thumbnail_image_url")),
            DeveloperType = reader.GetString(reader.GetOrdinal("developer_type")),
            FriendKey = reader.GetString(reader.GetOrdinal("friend_key")),
            IsFriend = reader.GetInt32(reader.GetOrdinal("is_friend")) == 1,
            LastActivity = reader.GetString(reader.GetOrdinal("last_activity")),
            LastLogin = reader.GetString(reader.GetOrdinal("last_login")),
            LastMobile = reader.IsDBNull(reader.GetOrdinal("last_mobile")) ? null : reader.GetString(reader.GetOrdinal("last_mobile")),
            LastPlatform = reader.GetString(reader.GetOrdinal("last_platform")),
            Location = reader.GetString(reader.GetOrdinal("location")),
            Platform = reader.GetString(reader.GetOrdinal("platform")),
            ProfilePicOverride = reader.GetString(reader.GetOrdinal("profile_pic_override")),
            Status = reader.GetString(reader.GetOrdinal("status")),
            StatusDescription = reader.GetString(reader.GetOrdinal("status_description")),
            Tags = System.Text.Json.JsonSerializer.Deserialize<List<string>>(reader.GetString(reader.GetOrdinal("tags"))) ?? new(),
            UserIcon = reader.GetString(reader.GetOrdinal("user_icon")),
            CurrentAvatarTags = System.Text.Json.JsonSerializer.Deserialize<List<string>>(reader.GetString(reader.GetOrdinal("current_avatar_tags"))) ?? new(),
            State = reader.GetString(reader.GetOrdinal("state")),
            LastUpdated = DateTime.Parse(reader.GetString(reader.GetOrdinal("last_updated")))
        };
    }

    private static FriendEvent MapFriendEvent(SqliteDataReader reader)
    {
        return new FriendEvent
        {
            Id = reader.GetInt64(reader.GetOrdinal("id")),
            UserId = reader.GetString(reader.GetOrdinal("user_id")),
            DisplayName = reader.GetString(reader.GetOrdinal("display_name")),
            EventType = reader.GetString(reader.GetOrdinal("event_type")),
            OldValue = reader.IsDBNull(reader.GetOrdinal("old_value")) ? null : reader.GetString(reader.GetOrdinal("old_value")),
            NewValue = reader.IsDBNull(reader.GetOrdinal("new_value")) ? null : reader.GetString(reader.GetOrdinal("new_value")),
            WorldName = reader.IsDBNull(reader.GetOrdinal("world_name")) ? null : reader.GetString(reader.GetOrdinal("world_name")),
            Timestamp = DateTime.Parse(reader.GetString(reader.GetOrdinal("timestamp")))
        };
    }

    private static Notification MapNotification(SqliteDataReader reader)
    {
        var detailsJson = reader.IsDBNull(reader.GetOrdinal("details")) ? null : reader.GetString(reader.GetOrdinal("details"));
        object? details = null;
        if (detailsJson != null)
        {
            try { details = System.Text.Json.JsonSerializer.Deserialize<object>(detailsJson); }
            catch { details = detailsJson; }
        }

        return new Notification
        {
            Id = reader.GetString(reader.GetOrdinal("id")),
            Type = reader.GetString(reader.GetOrdinal("type")),
            SenderUserId = reader.GetString(reader.GetOrdinal("sender_user_id")),
            ReceiverUserId = reader.GetString(reader.GetOrdinal("receiver_user_id")),
            Message = reader.GetString(reader.GetOrdinal("message")),
            Details = details,
            Seen = reader.GetInt32(reader.GetOrdinal("seen")) == 1,
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at"))),
            LastUpdated = DateTime.Parse(reader.GetString(reader.GetOrdinal("last_updated")))
        };
    }

    public async Task<WorldCache?> GetWorldCacheAsync(string worldId)
    {
        await _semaphore.WaitAsync();
        try
        {
            EnsureConnection();
            await using var command = _connection!.CreateCommand();
            command.CommandText = "SELECT * FROM world_cache WHERE world_id = @Id";
            command.Parameters.AddWithValue("@Id", worldId);
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new WorldCache
                {
                    WorldId = reader.GetString(reader.GetOrdinal("world_id")),
                    WorldName = reader.GetString(reader.GetOrdinal("world_name")),
                    AuthorName = reader.IsDBNull(reader.GetOrdinal("author_name")) ? null : reader.GetString(reader.GetOrdinal("author_name")),
                    Description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString(reader.GetOrdinal("description")),
                    ThumbnailUrl = reader.IsDBNull(reader.GetOrdinal("thumbnail_url")) ? null : reader.GetString(reader.GetOrdinal("thumbnail_url")),
                    Capacity = reader.IsDBNull(reader.GetOrdinal("capacity")) ? 0 : reader.GetInt32(reader.GetOrdinal("capacity")),
                    LastUpdated = DateTime.Parse(reader.GetString(reader.GetOrdinal("last_updated")))
                };
            }
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SaveWorldCacheAsync(WorldCache world)
    {
        await _semaphore.WaitAsync();
        try
        {
            EnsureConnection();
            await using var command = _connection!.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO world_cache (world_id, world_name, author_name, description, thumbnail_url, capacity, last_updated)
                VALUES (@Id, @Name, @Author, @Desc, @Thumb, @Cap, @Updated)";
            command.Parameters.AddWithValue("@Id", world.WorldId);
            command.Parameters.AddWithValue("@Name", world.WorldName);
            command.Parameters.AddWithValue("@Author", world.AuthorName ?? "");
            command.Parameters.AddWithValue("@Desc", world.Description ?? "");
            command.Parameters.AddWithValue("@Thumb", world.ThumbnailUrl ?? "");
            command.Parameters.AddWithValue("@Cap", world.Capacity);
            command.Parameters.AddWithValue("@Updated", world.LastUpdated.ToString("o"));
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<FriendGroup>> GetFriendGroupsAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            EnsureConnection();
            await using var command = _connection!.CreateCommand();
            command.CommandText = "SELECT * FROM friend_groups ORDER BY sort_order";
            var groups = new List<FriendGroup>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                groups.Add(new FriendGroup
                {
                    Id = reader.GetString(reader.GetOrdinal("id")),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    Color = reader.IsDBNull(reader.GetOrdinal("color")) ? null : reader.GetString(reader.GetOrdinal("color")),
                    SortOrder = reader.GetInt32(reader.GetOrdinal("sort_order")),
                    CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at")))
                });
            }
            return groups;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<FriendGroup?> GetFriendGroupAsync(string groupId)
    {
        await _semaphore.WaitAsync();
        try
        {
            EnsureConnection();
            await using var command = _connection!.CreateCommand();
            command.CommandText = "SELECT * FROM friend_groups WHERE id = @Id";
            command.Parameters.AddWithValue("@Id", groupId);
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new FriendGroup
                {
                    Id = reader.GetString(reader.GetOrdinal("id")),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    Color = reader.IsDBNull(reader.GetOrdinal("color")) ? null : reader.GetString(reader.GetOrdinal("color")),
                    SortOrder = reader.GetInt32(reader.GetOrdinal("sort_order")),
                    CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at")))
                };
            }
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task CreateFriendGroupAsync(FriendGroup group)
    {
        await _semaphore.WaitAsync();
        try
        {
            EnsureConnection();
            await using var command = _connection!.CreateCommand();
            command.CommandText = @"
                INSERT INTO friend_groups (id, name, color, sort_order, created_at)
                VALUES (@Id, @Name, @Color, @Sort, @Created)";
            command.Parameters.AddWithValue("@Id", group.Id);
            command.Parameters.AddWithValue("@Name", group.Name);
            command.Parameters.AddWithValue("@Color", group.Color ?? "");
            command.Parameters.AddWithValue("@Sort", group.SortOrder);
            command.Parameters.AddWithValue("@Created", group.CreatedAt.ToString("o"));
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UpdateFriendGroupAsync(FriendGroup group)
    {
        await _semaphore.WaitAsync();
        try
        {
            EnsureConnection();
            await using var command = _connection!.CreateCommand();
            command.CommandText = @"
                UPDATE friend_groups SET name = @Name, color = @Color, sort_order = @Sort
                WHERE id = @Id";
            command.Parameters.AddWithValue("@Id", group.Id);
            command.Parameters.AddWithValue("@Name", group.Name);
            command.Parameters.AddWithValue("@Color", group.Color ?? "");
            command.Parameters.AddWithValue("@Sort", group.SortOrder);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteFriendGroupAsync(string groupId)
    {
        await _semaphore.WaitAsync();
        try
        {
            EnsureConnection();
            await using var command = _connection!.CreateCommand();
            command.CommandText = "DELETE FROM friend_group_members WHERE group_id = @Id; DELETE FROM friend_groups WHERE id = @Id";
            command.Parameters.AddWithValue("@Id", groupId);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task AddGroupMemberAsync(string groupId, string userId)
    {
        await _semaphore.WaitAsync();
        try
        {
            EnsureConnection();
            await using var command = _connection!.CreateCommand();
            command.CommandText = @"
                INSERT OR IGNORE INTO friend_group_members (group_id, user_id, added_at)
                VALUES (@GroupId, @UserId, @Added)";
            command.Parameters.AddWithValue("@GroupId", groupId);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@Added", DateTime.UtcNow.ToString("o"));
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RemoveGroupMemberAsync(string groupId, string userId)
    {
        await _semaphore.WaitAsync();
        try
        {
            EnsureConnection();
            await using var command = _connection!.CreateCommand();
            command.CommandText = "DELETE FROM friend_group_members WHERE group_id = @GroupId AND user_id = @UserId";
            command.Parameters.AddWithValue("@GroupId", groupId);
            command.Parameters.AddWithValue("@UserId", userId);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<string>> GetGroupMembersAsync(string groupId)
    {
        await _semaphore.WaitAsync();
        try
        {
            EnsureConnection();
            await using var command = _connection!.CreateCommand();
            command.CommandText = "SELECT user_id FROM friend_group_members WHERE group_id = @GroupId";
            command.Parameters.AddWithValue("@GroupId", groupId);
            var members = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                members.Add(reader.GetString(reader.GetOrdinal("user_id")));
            }
            return members;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    // ── UserNotes ───────────────────────────────────────────────────────

    public async Task<string?> GetUserNoteAsync(string userId)
    {
        await _semaphore.WaitAsync();
        try
        {
            EnsureConnection();
            await using var command = _connection!.CreateCommand();
            command.CommandText = "SELECT note FROM user_notes WHERE user_id = @Id";
            command.Parameters.AddWithValue("@Id", userId);
            var result = await command.ExecuteScalarAsync();
            return result?.ToString();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SaveUserNoteAsync(string userId, string note)
    {
        await _semaphore.WaitAsync();
        try
        {
            EnsureConnection();
            await using var command = _connection!.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO user_notes (user_id, note, updated_at)
                VALUES (@Id, @Note, @Updated)";
            command.Parameters.AddWithValue("@Id", userId);
            command.Parameters.AddWithValue("@Note", note);
            command.Parameters.AddWithValue("@Updated", DateTime.UtcNow.ToString("o"));
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    // ── Settings ────────────────────────────────────────────────────────

    public async Task<Dictionary<string, string>> GetSettingsAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            EnsureConnection();
            await using var command = _connection!.CreateCommand();
            command.CommandText = "SELECT key, value FROM settings";
            var settings = new Dictionary<string, string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                settings[reader.GetString(reader.GetOrdinal("key"))] = reader.GetString(reader.GetOrdinal("value"));
            }
            return settings;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SaveSettingAsync(string key, string value)
    {
        await _semaphore.WaitAsync();
        try
        {
            EnsureConnection();
            await using var command = _connection!.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO settings (key, value)
                VALUES (@Key, @Value)";
            command.Parameters.AddWithValue("@Key", key);
            command.Parameters.AddWithValue("@Value", value);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SaveSettingsAsync(Dictionary<string, string> settings)
    {
        foreach (var kvp in settings)
        {
            await SaveSettingAsync(kvp.Key, kvp.Value);
        }
    }

    private void EnsureConnection()
    {
        if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
        {
            throw new InvalidOperationException("Database connection is not open. Call InitializeAsync() first.");
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
