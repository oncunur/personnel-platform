namespace PersonnelPlatform.Application.Authorization;

public sealed record RoleSummary(Guid Id, string Code, string Name);
public sealed record PermissionSummary(Guid Id, string Code, string Name, string Module);
public sealed record ScopeSummary(Guid Id, string ScopeType, Guid? ScopeId, DateTimeOffset ValidFrom, DateTimeOffset? ValidUntil);

public sealed record AuthorizationSnapshot(IReadOnlyList<RoleSummary> Roles, IReadOnlyList<PermissionSummary> Permissions, IReadOnlyList<ScopeSummary> Scopes);
