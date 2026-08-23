using PersonnelPlatform.Domain.Organization;
using Xunit;

namespace PersonnelPlatform.UnitTests;

public sealed class OrganizationEntityTests
{
    [Fact]
    public void Company_should_normalize_code_and_currency()
    {
        var company = Company.Create(" abc ", "ABC Company", null, null, null, null, "usd", DateTimeOffset.UtcNow, null);
        Assert.Equal("ABC", company.Code);
        Assert.Equal("USD", company.DefaultCurrency);
        Assert.True(company.IsActive);
    }

    [Fact]
    public void Project_should_reject_end_date_before_start_date()
    {
        var companyId = Guid.NewGuid();
        var start = new DateOnly(2026, 9, 10);
        var end = new DateOnly(2026, 9, 1);
        Assert.Throws<ArgumentException>(() => Project.Create(companyId, "P1", "Project", null, "TR", start, end, DateTimeOffset.UtcNow, null));
    }

    [Fact]
    public void Project_should_start_in_draft_status()
    {
        var project = Project.Create(Guid.NewGuid(), "P1", "Project", "Istanbul", "TR", null, null, DateTimeOffset.UtcNow, null);
        Assert.Equal(ProjectStatuses.Draft, project.Status);
    }
}
