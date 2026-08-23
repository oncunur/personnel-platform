using PersonnelPlatform.Application.Security;
using PersonnelPlatform.Domain.Identity;
using Xunit;

namespace PersonnelPlatform.UnitTests;

public sealed class SecurityHardeningTests
{
    [Fact]
    public void Mfa_challenge_should_stop_after_five_failed_attempts()
    {
        var now = new DateTimeOffset(2026, 8, 23, 18, 0, 0, TimeSpan.Zero);
        var challenge = MfaChallenge.Create(Guid.NewGuid(), "hash", MfaChallengePurposes.Login, now, now.AddMinutes(5), "127.0.0.1", "test");

        for (var i = 0; i < 5; i++) challenge.RegisterFailure();

        Assert.False(challenge.IsUsableAt(now.AddMinutes(1)));
        Assert.Equal(5, challenge.FailedAttemptCount);
    }

    [Fact]
    public void Enabled_MFA_credential_should_reject_TOTP_replay()
    {
        var now = new DateTimeOffset(2026, 8, 23, 18, 0, 0, TimeSpan.Zero);
        var credential = UserMfaCredential.CreatePending(Guid.NewGuid(), "protected-secret", now);
        credential.Enable(100, now);

        Assert.Throws<InvalidOperationException>(() => credential.RecordAcceptedCode(100, now.AddSeconds(30)));
        credential.RecordAcceptedCode(101, now.AddSeconds(30));
        Assert.Equal(101, credential.LastAcceptedTimeStep);
    }

    [Fact]
    public void Sensitive_masking_should_not_expose_full_values()
    {
        Assert.Equal("*******6789", SensitiveDataMasking.MaskNationalId("12345678901"));
        Assert.Equal("TR********************1234", SensitiveDataMasking.MaskIban("TR12 3456 7890 1234 5678 9012 34"));
        Assert.Equal("*** TRY", SensitiveDataMasking.MaskMoney(12345.67m, "try"));
    }
}
