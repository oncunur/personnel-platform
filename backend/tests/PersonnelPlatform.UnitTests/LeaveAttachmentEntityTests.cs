using PersonnelPlatform.Domain.Leave;
using Xunit;

namespace PersonnelPlatform.UnitTests;

public sealed class LeaveAttachmentEntityTests
{
    [Fact]
    public void Create_trims_description_and_sets_audit_fields()
    {
        var leaveId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var attachment = LeaveAttachment.Create(leaveId, fileId, "  sağlık raporu  ", now, actor);

        Assert.Equal(leaveId, attachment.LeaveId);
        Assert.Equal(fileId, attachment.FileId);
        Assert.Equal("sağlık raporu", attachment.Description);
        Assert.Equal(now, attachment.CreatedAt);
        Assert.Equal(actor, attachment.CreatedBy);
    }

    [Fact]
    public void Create_rejects_empty_identifiers()
    {
        Assert.Throws<ArgumentException>(() => LeaveAttachment.Create(Guid.Empty, Guid.NewGuid(), null, DateTimeOffset.UtcNow, Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => LeaveAttachment.Create(Guid.NewGuid(), Guid.Empty, null, DateTimeOffset.UtcNow, Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => LeaveAttachment.Create(Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow, Guid.Empty));
    }

    [Fact]
    public void Create_rejects_description_longer_than_500_characters()
    {
        Assert.Throws<ArgumentException>(() => LeaveAttachment.Create(Guid.NewGuid(), Guid.NewGuid(), new string('x', 501), DateTimeOffset.UtcNow, Guid.NewGuid()));
    }
}
