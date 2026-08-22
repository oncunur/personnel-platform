using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Identity;

public sealed class RolePermission : Entity
{
    private RolePermission() { }

    private RolePermission(Guid roleId, Guid permissionId, DateTimeOffset grantedAt, Guid? grantedBy)
    {
        RoleId = roleId;
        PermissionId = permissionId;
        GrantedAt = grantedAt;
        GrantedBy = grantedBy;
    }

    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }
    public Guid? GrantedBy { get; private set; }

    public static RolePermission Create(Guid roleId, Guid permissionId, DateTimeOffset grantedAt, Guid? grantedBy)
    {
        if (roleId == Guid.Empty) throw new ArgumentException("Role id must not be empty.", nameof(roleId));
        if (permissionId == Guid.Empty) throw new ArgumentException("Permission id must not be empty.", nameof(permissionId));
        return new RolePermission(roleId, permissionId, grantedAt, grantedBy);
    }
}
