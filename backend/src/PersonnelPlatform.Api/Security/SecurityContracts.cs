using PersonnelPlatform.Application.Authorization;

namespace PersonnelPlatform.Api.Security;

public sealed record SetRolePermissionsRequest(IReadOnlyCollection<Guid> PermissionIds);
public sealed record SetUserRolesRequest(IReadOnlyCollection<Guid> RoleIds);
public sealed record SetUserScopesRequest(IReadOnlyCollection<UserScopeInput> Scopes);
public sealed record ScopeCheckResponse(string ScopeType, Guid? ScopeId, bool Allowed);
