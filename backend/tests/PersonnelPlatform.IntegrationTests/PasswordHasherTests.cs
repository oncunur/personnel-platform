using PersonnelPlatform.Infrastructure.Identity;
using Xunit;

namespace PersonnelPlatform.IntegrationTests;

public sealed class PasswordHasherTests
{
    [Fact]
    public void Hash_should_verify_only_the_original_password()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var hash = hasher.Hash("A-strong-development-password!");

        Assert.True(hasher.Verify("A-strong-development-password!", hash));
        Assert.False(hasher.Verify("wrong-password", hash));
    }
}
