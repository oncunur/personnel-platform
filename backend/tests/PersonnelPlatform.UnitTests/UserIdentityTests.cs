using PersonnelPlatform.Domain.Identity;
using Xunit;

namespace PersonnelPlatform.UnitTests;

public sealed class UserIdentityTests
{
    [Fact]
    public void Failed_logins_should_lock_user_at_configured_threshold()
    {
        var now = new DateTimeOffset(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);
        var user = User.Create("admin", "ADMIN", null, null, "hash", now);

        Assert.False(user.RegisterFailedLogin(now, 3, TimeSpan.FromMinutes(15)));
        Assert.False(user.RegisterFailedLogin(now.AddSeconds(1), 3, TimeSpan.FromMinutes(15)));
        Assert.True(user.RegisterFailedLogin(now.AddSeconds(2), 3, TimeSpan.FromMinutes(15)));
        Assert.True(user.IsLockedAt(now.AddMinutes(1)));
    }

    [Fact]
    public void Successful_login_should_reset_failed_login_state()
    {
        var now = new DateTimeOffset(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);
        var user = User.Create("admin", "ADMIN", null, null, "hash", now);
        _ = user.RegisterFailedLogin(now, 5, TimeSpan.FromMinutes(15));

        user.RegisterSuccessfulLogin(now.AddMinutes(1));

        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockedUntil);
        Assert.Equal(now.AddMinutes(1), user.LastLoginAt);
    }

    [Fact]
    public void Invalidate_sessions_should_increment_security_version()
    {
        var now = new DateTimeOffset(2026, 8, 23, 4, 0, 0, TimeSpan.Zero);
        var user = User.Create("admin", "ADMIN", null, null, "hash", now);
        var previousSecurityVersion = user.SecurityVersion;
        var previousVersion = user.Version;

        user.InvalidateSessions(now.AddMinutes(1));

        Assert.Equal(previousSecurityVersion + 1, user.SecurityVersion);
        Assert.Equal(previousVersion + 1, user.Version);
        Assert.Equal(now.AddMinutes(1), user.UpdatedAt);
    }
}
