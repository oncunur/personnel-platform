namespace PersonnelPlatform.Infrastructure.Identity;

public sealed record JwtTokenOptions(
    string Issuer,
    string Audience,
    string SigningKey,
    TimeSpan AccessTokenLifetime,
    TimeSpan RefreshTokenLifetime);
