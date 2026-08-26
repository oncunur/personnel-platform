using PersonnelPlatform.Application.Identity;
using Xunit;

namespace PersonnelPlatform.UnitTests;

public sealed class AuthPolicyOptionsTests
{
    private static readonly IReadOnlySet<string> RequiredRoles = new HashSet<string>(StringComparer.Ordinal)
    {
        "SYSTEM_ADMIN"
    };

    [Fact]
    public void Disabled_MFA_should_not_be_required_for_a_protected_role()
    {
        var options = CreateOptions(false);

        Assert.False(options.RequiresMfa(["SYSTEM_ADMIN"]));
    }

    [Fact]
    public void Enabled_MFA_should_be_required_for_a_protected_role()
    {
        var options = CreateOptions(true);

        Assert.True(options.RequiresMfa(["SYSTEM_ADMIN"]));
        Assert.False(options.RequiresMfa(["EMPLOYEE"]));
    }

    private static AuthPolicyOptions CreateOptions(bool mfaEnabled) => new(
        5,
        TimeSpan.FromMinutes(15),
        mfaEnabled,
        TimeSpan.FromMinutes(5),
        "PersonnelPlatform",
        RequiredRoles);
}
