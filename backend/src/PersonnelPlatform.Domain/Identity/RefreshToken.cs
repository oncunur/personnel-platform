using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Identity;

public sealed class RefreshToken : Entity
{
    private RefreshToken() { }

    private RefreshToken(Guid userId, string tokenHash, int securityVersion, DateTimeOffset expiresAt, DateTimeOffset createdAt, string? createdByIp, string? deviceName, bool mfaVerified)
    {
        UserId = userId; TokenHash = tokenHash; SecurityVersion = securityVersion; ExpiresAt = expiresAt; CreatedAt = createdAt;
        CreatedByIp = createdByIp; DeviceName = deviceName; MfaVerified = mfaVerified;
    }

    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public int SecurityVersion { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedByIp { get; private set; }
    public string? DeviceName { get; private set; }
    public bool MfaVerified { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevokedByIp { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    public static RefreshToken Create(Guid userId, string tokenHash, int securityVersion, DateTimeOffset expiresAt, DateTimeOffset createdAt, string? createdByIp, string? deviceName, bool mfaVerified = false)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User id must not be empty.", nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        if (expiresAt <= createdAt) throw new ArgumentException("Refresh token expiry must be after creation time.", nameof(expiresAt));
        if (securityVersion <= 0) throw new ArgumentOutOfRangeException(nameof(securityVersion));
        return new RefreshToken(userId, tokenHash, securityVersion, expiresAt, createdAt, createdByIp, deviceName, mfaVerified);
    }

    public bool IsActiveAt(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Rotate(DateTimeOffset now, string replacementTokenHash, string? revokedByIp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replacementTokenHash);
        RevokedAt = now; RevokedByIp = revokedByIp; ReplacedByTokenHash = replacementTokenHash;
    }

    public void Revoke(DateTimeOffset now, string? revokedByIp)
    {
        if (RevokedAt is not null) return;
        RevokedAt = now; RevokedByIp = revokedByIp;
    }
}
