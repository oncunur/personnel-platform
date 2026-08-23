using System.Text;
using System.Text.Json;
using PersonnelPlatform.Domain.Identity;
using PersonnelPlatform.Infrastructure.Identity;
using Xunit;

namespace PersonnelPlatform.IntegrationTests;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void Issue_should_create_access_and_refresh_tokens_with_password_assurance()
    {
        var now = new DateTimeOffset(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);
        var service = CreateService();
        var user = User.Create("admin", "ADMIN", "admin@local.test", "ADMIN@LOCAL.TEST", "hash", now);

        var tokens = service.Issue(user, now, false);

        Assert.Equal(3, tokens.AccessToken.Split('.').Length);
        Assert.True(tokens.AccessTokenExpiresAt > now);
        Assert.True(tokens.RefreshTokenExpiresAt > tokens.AccessTokenExpiresAt);
        Assert.Equal(64, tokens.RefreshTokenHash.Length);
        Assert.Equal(tokens.RefreshTokenHash, service.HashRefreshToken(tokens.RefreshToken));
        Assert.Equal("pwd", ReadAmr(tokens.AccessToken));
    }

    [Fact]
    public void Issue_should_mark_MFA_verified_session_in_access_token()
    {
        var now = new DateTimeOffset(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);
        var service = CreateService();
        var user = User.Create("admin", "ADMIN", "admin@local.test", "ADMIN@LOCAL.TEST", "hash", now);

        var tokens = service.Issue(user, now, true);

        Assert.Equal("mfa", ReadAmr(tokens.AccessToken));
    }

    private static JwtTokenService CreateService() => new(new JwtTokenOptions(
        "issuer",
        "audience",
        "this-is-a-development-signing-key-with-more-than-32-bytes",
        TimeSpan.FromMinutes(15),
        TimeSpan.FromDays(7)));

    private static string ReadAmr(string jwt)
    {
        var body = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        body = body.PadRight(body.Length + ((4 - body.Length % 4) % 4), '=');
        using var json = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(body)));
        return json.RootElement.GetProperty("amr").GetString()!;
    }
}
