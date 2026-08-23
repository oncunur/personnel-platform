using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Leave;

public sealed class LeaveAttachment : AuditableEntity
{
    private LeaveAttachment() { }

    public Guid LeaveId { get; private set; }
    public Guid FileId { get; private set; }
    public string? Description { get; private set; }

    public static LeaveAttachment Create(Guid leaveId, Guid fileId, string? description, DateTimeOffset now, Guid actorUserId)
    {
        if (leaveId == Guid.Empty || fileId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Leave, file and actor are required.");
        if (!string.IsNullOrWhiteSpace(description) && description.Trim().Length > 500) throw new ArgumentException("Attachment description is too long.", nameof(description));
        return new LeaveAttachment
        {
            LeaveId = leaveId,
            FileId = fileId,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            CreatedAt = now,
            CreatedBy = actorUserId
        };
    }
}
