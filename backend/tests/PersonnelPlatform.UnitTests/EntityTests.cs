using Xunit;
using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.UnitTests;

public sealed class EntityTests
{
    [Fact]
    public void New_entity_should_receive_a_non_empty_id()
    {
        var entity = new TestEntity();
        Assert.NotEqual(Guid.Empty, entity.Id);
    }

    private sealed class TestEntity : Entity;
}
