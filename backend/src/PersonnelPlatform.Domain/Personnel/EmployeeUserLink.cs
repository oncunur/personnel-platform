using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Personnel;

public sealed class EmployeeUserLink : AuditableEntity
{
    private EmployeeUserLink() { }

    public Guid UserId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public bool IsActive { get; private set; }

    public static EmployeeUserLink Create(Guid userId, Guid employeeId, DateTimeOffset now, Guid? actorUserId)
    {
        if (userId == Guid.Empty || employeeId == Guid.Empty) throw new ArgumentException("User and employee are required.");
        return new EmployeeUserLink
        {
            UserId = userId,
            EmployeeId = employeeId,
            IsActive = true,
            CreatedAt = now,
            CreatedBy = actorUserId
        };
    }

    public void Relink(Guid employeeId, DateTimeOffset now, Guid? actorUserId)
    {
        if (employeeId == Guid.Empty) throw new ArgumentException("Employee is required.", nameof(employeeId));
        EmployeeId = employeeId;
        IsActive = true;
        UpdatedAt = now;
        UpdatedBy = actorUserId;
        Version++;
    }

    public void Deactivate(DateTimeOffset now, Guid? actorUserId)
    {
        if (!IsActive) return;
        IsActive = false;
        UpdatedAt = now;
        UpdatedBy = actorUserId;
        Version++;
    }
}
