using PersonnelPlatform.Domain.Identity;
using Xunit;

namespace PersonnelPlatform.UnitTests;

public sealed class UserScopeTests
{
    [Fact]
    public void Scope_should_normalize_type_and_keep_validity()
    {
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);
        var scope = UserScope.Create(userId, "company", companyId, now, now.AddDays(30), now, null);
        Assert.Equal("COMPANY", scope.ScopeType);
        Assert.Equal(companyId, scope.ScopeId);
        Assert.True(scope.IsActive);
    }

    [Fact]
    public void Scope_should_reject_invalid_date_range()
    {
        var now = new DateTimeOffset(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);
        Assert.Throws<ArgumentException>(() => UserScope.Create(Guid.NewGuid(), "COMPANY", Guid.NewGuid(), now, now, now, null));
    }
}
