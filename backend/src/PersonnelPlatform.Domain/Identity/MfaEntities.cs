using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Identity;

public static class MfaChallengePurposes
{
    public const string Login = "LOGIN";
    public const string Enrollment = "ENROLLMENT";
    public static bool IsKnown(string value) => value is Login or Enrollment;
}

public sealed class UserMfaCredential : AuditableEntity
{
    private UserMfaCredential() { }
    public Guid UserId { get; private set; }
    public string ProtectedSecret { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }
    public DateTimeOffset? EnabledAt { get; private set; }
    public long? LastAcceptedTimeStep { get; private set; }

    public static UserMfaCredential CreatePending(Guid userId, string protectedSecret, DateTimeOffset now)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User is required.", nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedSecret);
        return new UserMfaCredential { UserId = userId, ProtectedSecret = protectedSecret, IsEnabled = false, CreatedAt = now.ToUniversalTime() };
    }

    public void ReplacePendingSecret(string protectedSecret, DateTimeOffset now)
    {
        if (IsEnabled) throw new InvalidOperationException("Enabled MFA secret cannot be replaced without reset.");
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedSecret);
        ProtectedSecret = protectedSecret; LastAcceptedTimeStep = null; UpdatedAt = now.ToUniversalTime(); Version++;
    }

    public void Enable(long acceptedTimeStep, DateTimeOffset now)
    {
        if (IsEnabled) throw new InvalidOperationException("MFA is already enabled.");
        if (acceptedTimeStep < 0) throw new ArgumentOutOfRangeException(nameof(acceptedTimeStep));
        IsEnabled = true; EnabledAt = now.ToUniversalTime(); LastAcceptedTimeStep = acceptedTimeStep; UpdatedAt = now.ToUniversalTime(); Version++;
    }

    public void RecordAcceptedCode(long acceptedTimeStep, DateTimeOffset now)
    {
        if (!IsEnabled) throw new InvalidOperationException("MFA is not enabled.");
        if (LastAcceptedTimeStep is not null && acceptedTimeStep <= LastAcceptedTimeStep.Value) throw new InvalidOperationException("TOTP code replay is not allowed.");
        LastAcceptedTimeStep = acceptedTimeStep; UpdatedAt = now.ToUniversalTime(); Version++;
    }

    public void Reset(string protectedSecret, DateTimeOffset now, Guid actorUserId)
    {
        if (actorUserId == Guid.Empty) throw new ArgumentException("Actor is required.", nameof(actorUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedSecret);
        ProtectedSecret = protectedSecret; IsEnabled = false; EnabledAt = null; LastAcceptedTimeStep = null;
        UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
    }
}

public sealed class MfaChallenge : Entity
{
    private MfaChallenge() { }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public string Purpose { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public int FailedAttemptCount { get; private set; }
    public string? IpAddress { get; private set; }
    public string? DeviceName { get; private set; }

    public static MfaChallenge Create(Guid userId, string tokenHash, string purpose, DateTimeOffset now, DateTimeOffset expiresAt, string? ipAddress, string? deviceName)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User is required.", nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        var normalizedPurpose = purpose.Trim().ToUpperInvariant();
        if (!MfaChallengePurposes.IsKnown(normalizedPurpose)) throw new ArgumentException("MFA challenge purpose is invalid.", nameof(purpose));
        if (expiresAt <= now) throw new ArgumentException("MFA challenge expiry must be in the future.");
        return new MfaChallenge { UserId = userId, TokenHash = tokenHash, Purpose = normalizedPurpose, CreatedAt = now.ToUniversalTime(), ExpiresAt = expiresAt.ToUniversalTime(), IpAddress = Limit(ipAddress, 100), DeviceName = Limit(deviceName, 200) };
    }

    public bool IsUsableAt(DateTimeOffset now) => ConsumedAt is null && ExpiresAt > now && FailedAttemptCount < 5;
    public void RegisterFailure() { if (ConsumedAt is null && FailedAttemptCount < 5) FailedAttemptCount++; }
    public void Consume(DateTimeOffset now) { if (!IsUsableAt(now)) throw new InvalidOperationException("MFA challenge is not usable."); ConsumedAt = now.ToUniversalTime(); }
    private static string? Limit(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];
}
