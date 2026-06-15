namespace VrcxServer.Models;

/// <summary>
/// Represents a friend status change event.
/// </summary>
public class FriendEvent
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty; // "online", "offline", "status_change", "location_change"
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? WorldName { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
