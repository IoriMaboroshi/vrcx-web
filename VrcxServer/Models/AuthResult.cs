namespace VrcxServer.Models;

/// <summary>
/// Result of an authentication attempt.
/// </summary>
public class AuthResult
{
    public bool Success { get; set; }
    public string? AuthToken { get; set; }
    public string? ErrorMessage { get; set; }
    public bool RequiresTwoFactorAuth { get; set; }
    public List<string>? TwoFactorAuthMethods { get; set; }
    public Friend? CurrentUser { get; set; }
}

/// <summary>
/// Login request body.
/// </summary>
public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// 2FA verification request body.
/// </summary>
public class TwoFactorRequest
{
    public string Code { get; set; } = string.Empty;
    public string Method { get; set; } = "totp"; // "totp", "otp", "emailotp"
}
