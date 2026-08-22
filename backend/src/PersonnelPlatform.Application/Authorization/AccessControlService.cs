namespace PersonnelPlatform.Application.Authorization;

public sealed class AccessControlService(IAuthorizationRepository repository, TimeProvider timeProvider)
{
    public Task<AuthorizationSnapshot> GetSnapshotAsync(Guid userId, CancellationToken cancellationToken) => repository.GetSnapshotAsync(userId, timeProvider.GetUtcNow(), cancellationToken);
    public Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken cancellationToken) => repository.HasPermissionAsync(userId, permissionCode, cancellationToken);
    public Task<bool> HasScopeAsync(Guid userId, string scopeType, Guid? scopeId, CancellationToken cancellationToken) => repository.HasScopeAsync(userId, scopeType, scopeId, timeProvider.GetUtcNow(), cancellationToken);
}
