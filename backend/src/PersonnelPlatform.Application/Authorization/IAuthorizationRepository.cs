using PersonnelPlatform.Domain.Identity;

namespace PersonnelPlatform.Application.Authorization;

public interface IAuthorizationRepository
{
    Task<IReadOnlyList<Role>> ListRolesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Permission>> ListPermissionsAsync(CancellationToken cancellationToken);
    Task<Role?> FindRoleByIdAsync(Guid roleId, CancellationToken cancellationToken);
    Task<Role?> FindRoleByCodeAsync(string roleCode, CancellationToken cancellationToken);
    void AddRole(Role role);
    Task ReplaceRolePermissionsAsync(Guid roleId, IReadOnlyCollection<Guid> permissionIds, Guid? actorUserId, DateTimeOffset now, CancellationToken cancellationToken);
    Task ReplaceUserRolesAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, Guid? actorUserId, DateTimeOffset now, CancellationToken cancellationToken);
    Task ReplaceUserScopesAsync(Guid userId, IReadOnlyCollection<UserScope> scopes, CancellationToken cancellationToken);
    Task<AuthorizationSnapshot> GetSnapshotAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken cancellationToken);
    Task<bool> HasScopeAsync(Guid userId, string scopeType, Guid? scopeId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
