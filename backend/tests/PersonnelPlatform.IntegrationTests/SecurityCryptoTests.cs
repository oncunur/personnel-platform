using PersonnelPlatform.Infrastructure.Identity;
using PersonnelPlatform.Infrastructure.Security;
using Xunit;

namespace PersonnelPlatform.IntegrationTests;

public sealed class SecurityCryptoTests
{
    [Fact]
    public void AES_GCM_protector_should_round_trip_sensitive_value()
    {
        var protector = new AesGcmSensitiveDataProtector("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");

        var protectedValue = protector.Protect("TR330006100519786457841326");
        var plaintext = protector.Unprotect(protectedValue);

        Assert.StartsWith("v1:", protectedValue);
        Assert.DoesNotContain("TR330006100519786457841326", protectedValue, StringComparison.Ordinal);
        Assert.Equal("TR330006100519786457841326", plaintext);
    }

    [Fact]
    public void TOTP_service_should_verify_known_RFC6238_vector_and_block_replay()
    {
        var service = new TotpService();
        const string secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
        var now = DateTimeOffset.FromUnixTimeSeconds(59);

        Assert.True(service.TryVerify(secret, "287082", now, null, out var step));
        Assert.Equal(1, step);
        Assert.False(service.TryVerify(secret, "287082", now, step, out _));
    }

    [Fact]
    public void TOTP_uri_should_not_put_secret_in_path_or_account_label_unescaped()
    {
        var service = new TotpService();
        var secret = service.GenerateSecret();

        var uri = service.BuildOtpAuthUri("Personnel Platform", "user@example.com", secret);

        Assert.StartsWith("otpauth://totp/", uri);
        Assert.Contains("secret=", uri);
        Assert.Contains("issuer=Personnel%20Platform", uri);
        Assert.DoesNotContain("user@example.com", uri.Split('?')[0], StringComparison.Ordinal);
    }
}
