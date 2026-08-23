using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PersonnelPlatform.Application.Identity;
using PersonnelPlatform.Domain.Identity;

namespace PersonnelPlatform.Infrastructure.Identity;

public sealed class JwtTokenService(JwtTokenOptions options) : IAuthTokenService
{
    private readonly byte[] signingKey = ValidateAndEncodeSigningKey(options.SigningKey);

    public IssuedTokenPair Issue(User user, DateTimeOffset now, bool mfaVerified = false)
    {
        var accessExpiresAt = now.Add(options.AccessTokenLifetime);
        var refreshExpiresAt = now.Add(options.RefreshTokenLifetime);
        var accessToken = CreateAccessToken(user, now, accessExpiresAt, mfaVerified);
        var refreshToken = Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        var refreshTokenHash = HashRefreshToken(refreshToken);
        return new IssuedTokenPair(accessToken, accessExpiresAt, refreshToken, refreshTokenHash, refreshExpiresAt);
    }

    public string HashRefreshToken(string refreshToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken))).ToLowerInvariant();
    }

    private string CreateAccessToken(User user, DateTimeOffset issuedAt, DateTimeOffset expiresAt, bool mfaVerified)
    {
        var headerBytes = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object> { ["alg"] = "HS256", ["typ"] = "JWT" });
        var payload = new Dictionary<string, object>
        {
            ["iss"] = options.Issuer,
            ["aud"] = options.Audience,
            ["sub"] = user.Id.ToString(),
            ["unique_name"] = user.Username,
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["iat"] = issuedAt.ToUnixTimeSeconds(),
            ["nbf"] = issuedAt.ToUnixTimeSeconds(),
            ["exp"] = expiresAt.ToUnixTimeSeconds(),
            ["sv"] = user.SecurityVersion,
            ["amr"] = mfaVerified ? "mfa" : "pwd"
        };
        if (!string.IsNullOrWhiteSpace(user.Email)) payload["email"] = user.Email;
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        var header = Base64UrlEncode(headerBytes);
        var body = Base64UrlEncode(payloadBytes);
        var unsignedToken = $"{header}.{body}";
        var signature = HMACSHA256.HashData(signingKey, Encoding.ASCII.GetBytes(unsignedToken));
        return $"{unsignedToken}.{Base64UrlEncode(signature)}";
    }

    private static byte[] ValidateAndEncodeSigningKey(string signingKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signingKey);
        var bytes = Encoding.UTF8.GetBytes(signingKey);
        if (bytes.Length < 32) throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 UTF-8 bytes.");
        return bytes;
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
