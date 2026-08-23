using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Domain.Identity;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Authorization;

public sealed class AuthorizationRepository(ApplicationDbContext dbContext) : IAuthorizationRepository
{
    public async Task<IReadOnlyList<Role>> ListRolesAsync(CancellationToken cancellationToken) =>
        await dbContext.Roles.Where(x => x.DeletedAt == null).OrderBy(x => x.Code).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Permission>> ListPermissionsAsync(CancellationToken cancellationToken) =>
        await dbContext.Permissions.Where(x => x.IsActive).OrderBy(x => x.Module).ThenBy(x => x.Code).ToListAsync(cancellationToken);

    public Task<Role?> FindRoleByIdAsync(Guid roleId, CancellationToken cancellationToken) =>
        dbContext.Roles.FirstOrDefaultAsync(x => x.Id == roleId && x.DeletedAt == null, cancellationToken);

    public Task<Role?> FindRoleByCodeAsync(string roleCode, CancellationToken cancellationToken) =>
        dbContext.Roles.FirstOrDefaultAsync(x => x.Code == roleCode && x.DeletedAt == null, cancellationToken);

    public void AddRole(Role role) => dbContext.Roles.Add(role);

    public async Task ReplaceRolePermissionsAsync(Guid roleId, IReadOnlyCollection<Guid> permissionIds, Guid? actorUserId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await dbContext.RolePermissions.Where(x => x.RoleId == roleId).ToListAsync(cancellationToken);
        dbContext.RolePermissions.RemoveRange(existing);
        dbContext.RolePermissions.AddRange(permissionIds.Select(id => RolePermission.Create(roleId, id, now, actorUserId)));
    }

    public async Task ReplaceUserRolesAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, Guid? actorUserId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await dbContext.UserRoles.Where(x => x.UserId == userId).ToListAsync(cancellationToken);
        dbContext.UserRoles.RemoveRange(existing);
        dbContext.UserRoles.AddRange(roleIds.Select(id => UserRole.Create(userId, id, now, actorUserId)));
    }

    public async Task ReplaceUserScopesAsync(Guid userId, IReadOnlyCollection<UserScope> scopes, CancellationToken cancellationToken)
    {
        var existing = await dbContext.UserScopes.Where(x => x.UserId == userId).ToListAsync(cancellationToken);
        dbContext.UserScopes.RemoveRange(existing);
        dbContext.UserScopes.AddRange(scopes);
    }

    public async Task<AuthorizationSnapshot> GetSnapshotAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var roleRows = await (
            from userRole in dbContext.UserRoles
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where userRole.UserId == userId && role.IsActive && role.DeletedAt == null
            select new { role.Id, role.Code, role.Name })
            .Distinct()
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);

        var roles = roleRows
            .Select(x => new RoleSummary(x.Id, x.Code, x.Name))
            .ToList();

        var permissionRows = await (
            from userRole in dbContext.UserRoles
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            join rolePermission in dbContext.RolePermissions on role.Id equals rolePermission.RoleId
            join permission in dbContext.Permissions on rolePermission.PermissionId equals permission.Id
            where userRole.UserId == userId && role.IsActive && role.DeletedAt == null && permission.IsActive
            select new { permission.Id, permission.Code, permission.Name, permission.Module })
            .Distinct()
            .OrderBy(x => x.Module)
            .ThenBy(x => x.Code)
            .ToListAsync(cancellationToken);

        var permissions = permissionRows
            .Select(x => new PermissionSummary(x.Id, x.Code, x.Name, x.Module))
            .ToList();

        var scopes = await dbContext.UserScopes
            .Where(x => x.UserId == userId && x.IsActive && x.ValidFrom <= now && (x.ValidUntil == null || x.ValidUntil > now))
            .OrderBy(x => x.ScopeType).ThenBy(x => x.ScopeId)
            .Select(x => new ScopeSummary(x.Id, x.ScopeType, x.ScopeId, x.ValidFrom, x.ValidUntil))
            .ToListAsync(cancellationToken);

        return new AuthorizationSnapshot(roles, permissions, scopes);
    }

    public Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken cancellationToken) =>
        (from userRole in dbContext.UserRoles
         join role in dbContext.Roles on userRole.RoleId equals role.Id
         join rolePermission in dbContext.RolePermissions on role.Id equals rolePermission.RoleId
         join permission in dbContext.Permissions on rolePermission.PermissionId equals permission.Id
         where userRole.UserId == userId && role.IsActive && role.DeletedAt == null && permission.IsActive && permission.Code == permissionCode
         select permission.Id).AnyAsync(cancellationToken);

    public Task<bool> HasScopeAsync(Guid userId, string scopeType, Guid? scopeId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var normalizedType = scopeType.Trim().ToUpperInvariant();
        return dbContext.UserScopes.AnyAsync(
            x => x.UserId == userId && x.IsActive && x.ValidFrom <= now && (x.ValidUntil == null || x.ValidUntil > now)
                && (x.ScopeType == ScopeTypes.Global || (x.ScopeType == normalizedType && x.ScopeId == scopeId)),
            cancellationToken);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
