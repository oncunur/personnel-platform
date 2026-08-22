using PersonnelPlatform.Domain.Identity;
using PersonnelPlatform.Infrastructure.Identity;
using Xunit;

namespace PersonnelPlatform.IntegrationTests;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void Issue_should_create_access_and_refresh_tokens()
    {
        var now = new DateTimeOffset(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);
        var options = new JwtTokenOptions(
            "issuer",
            "audience",
            "this-is-a-development-signing-key-with-more-than-32-bytes",
            TimeSpan.FromMinutes(15),
            TimeSpan.FromDays(7));
        var service = new JwtTokenService(options);
        var user = User.Create("admin", "ADMIN", "admin@local.test", "ADMIN@LOCAL.TEST", "hash", now);

        var tokens = service.Issue(user, now);

        Assert.Equal(3, tokens.AccessToken.Split('.').Length);
        Assert.True(tokens.AccessTokenExpiresAt > now);
        Assert.True(tokens.RefreshTokenExpiresAt > tokens.AccessTokenExpiresAt);
        Assert.Equal(64, tokens.RefreshTokenHash.Length);
        Assert.Equal(tokens.RefreshTokenHash, service.HashRefreshToken(tokens.RefreshToken));
    }
}
