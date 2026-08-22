using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Identity;

public sealed class User : AuditableEntity
{
    private User()
    {
    }

    private User(
        string username,
        string normalizedUsername,
        string? email,
        string? normalizedEmail,
        string passwordHash,
        DateTimeOffset createdAt)
    {
        Username = username;
        NormalizedUsername = normalizedUsername;
        Email = email;
        NormalizedEmail = normalizedEmail;
        PasswordHash = passwordHash;
        IsActive = true;
        SecurityVersion = 1;
        CreatedAt = createdAt;
    }

    public string Username { get; private set; } = string.Empty;
    public string NormalizedUsername { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? NormalizedEmail { get; private set; }
    public string PasswordHash { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public int FailedLoginCount { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public int SecurityVersion { get; private set; }

    public static User Create(
        string username,
        string normalizedUsername,
        string? email,
        string? normalizedEmail,
        string passwordHash,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedUsername);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new User(
            username.Trim(),
            normalizedUsername,
            string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            normalizedEmail,
            passwordHash,
            createdAt);
    }

    public bool IsLockedAt(DateTimeOffset now) => LockedUntil is not null && LockedUntil > now;

    public bool RegisterFailedLogin(DateTimeOffset now, int maxFailedAttempts, TimeSpan lockoutDuration)
    {
        if (maxFailedAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFailedAttempts));
        }

        FailedLoginCount++;
        UpdatedAt = now;
        Version++;

        if (FailedLoginCount < maxFailedAttempts)
        {
            return false;
        }

        LockedUntil = now.Add(lockoutDuration);
        return true;
    }

    public void RegisterSuccessfulLogin(DateTimeOffset now)
    {
        FailedLoginCount = 0;
        LockedUntil = null;
        LastLoginAt = now;
        UpdatedAt = now;
        Version++;
    }

    public void ChangePasswordHash(string passwordHash, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        PasswordHash = passwordHash;
        SecurityVersion++;
        UpdatedAt = now;
        Version++;
    }

    public void Activate(DateTimeOffset now)
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        FailedLoginCount = 0;
        LockedUntil = null;
        SecurityVersion++;
        UpdatedAt = now;
        Version++;
    }

    public void Deactivate(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        SecurityVersion++;
        UpdatedAt = now;
        Version++;
    }
}
