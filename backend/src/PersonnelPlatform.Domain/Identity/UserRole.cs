using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Identity;

public sealed class UserRole : Entity
{
    private UserRole() { }

    private UserRole(Guid userId, Guid roleId, DateTimeOffset assignedAt, Guid? assignedBy)
    {
        UserId = userId;
        RoleId = roleId;
        AssignedAt = assignedAt;
        AssignedBy = assignedBy;
    }

    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }
    public Guid? AssignedBy { get; private set; }

    public static UserRole Create(Guid userId, Guid roleId, DateTimeOffset assignedAt, Guid? assignedBy)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User id must not be empty.", nameof(userId));
        if (roleId == Guid.Empty) throw new ArgumentException("Role id must not be empty.", nameof(roleId));
        return new UserRole(userId, roleId, assignedAt, assignedBy);
    }
}
