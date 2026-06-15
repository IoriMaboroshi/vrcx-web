namespace VrcxServer.Models;

/// <summary>
/// Represents a VRChat friend/user.
/// </summary>
public class Friend
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public List<string> BioLinks { get; set; } = new();
    public string CurrentAvatarImageUrl { get; set; } = string.Empty;
    public string CurrentAvatarThumbnailImageUrl { get; set; } = string.Empty;
    public string DeveloperType { get; set; } = string.Empty;
    public string FriendKey { get; set; } = string.Empty;
    public bool IsFriend { get; set; }
    public string LastActivity { get; set; } = string.Empty;
    public string LastLogin { get; set; } = string.Empty;
    public string? LastMobile { get; set; }
    public string LastPlatform { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string ProfilePicOverride { get; set; } = string.Empty;
    public string ProfilePicOverrideThumbnail { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusDescription { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string UserIcon { get; set; } = string.Empty;
    public List<string> CurrentAvatarTags { get; set; } = new();

    /// <summary>
    /// Derived state: active, online, or offline.
    /// </summary>
    public string State { get; set; } = "offline";

    /// <summary>
    /// Database: when this record was last updated.
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
