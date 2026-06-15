namespace VrcxServer.Models;

public class WorldCache
{
    public string WorldId { get; set; } = "";
    public string WorldName { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string Description { get; set; } = "";
    public string ThumbnailUrl { get; set; } = "";
    public int Capacity { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
