namespace VrcxServer.Models;

/// <summary>
/// Server configuration model.
/// </summary>
public class ServerConfig
{
    public string Version { get; set; } = "1.0.0";
    public string ApiEndpoint { get; set; } = "https://api.vrchat.cloud/api/1";
    public int FriendPollIntervalSeconds { get; set; } = 60;
    public int DatabasePageSize { get; set; } = 100;
    public bool EnableWebSocket { get; set; } = true;
    public string DataDirectory { get; set; } = "data";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Standard API response wrapper.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }

    public static ApiResponse<T> Ok(T data) => new() { Success = true, Data = data };
    public static ApiResponse<T> Fail(string error) => new() { Success = false, Error = error };
}

/// <summary>
/// Paginated result wrapper.
/// </summary>
public class PaginatedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Offset { get; set; }
    public int Limit { get; set; }
    public bool HasMore { get; set; }
}
