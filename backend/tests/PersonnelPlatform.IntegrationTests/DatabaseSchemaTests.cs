using Xunit;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.IntegrationTests;

public sealed class DatabaseSchemaTests
{
    [Fact]
    public void Sprint_zero_schema_names_are_stable()
    {
        Assert.Equal("system", DatabaseSchemas.System);
        Assert.Equal("organization", DatabaseSchemas.Organization);
        Assert.Equal("hr", DatabaseSchemas.Hr);
        Assert.Equal("audit", DatabaseSchemas.Audit);
    }
}
