namespace VrcxServer.Models;

/// <summary>
/// Represents a VRChat notification.
/// </summary>
public class Notification
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string SenderUserId { get; set; } = string.Empty;
    public string ReceiverUserId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public object? Details { get; set; }
    public bool Seen { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
