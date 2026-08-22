namespace PersonnelPlatform.Api.Auth;

public sealed record LoginRequest(string Username, string Password);

public sealed record AuthResponse(
    Guid UserId,
    string Username,
    string? Email,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt);

public sealed record MeResponse(Guid UserId, string Username, string? Email, int SecurityVersion);
