using Microsoft.AspNetCore.Authorization;

namespace PersonnelPlatform.Api.Authorization;

public sealed record PermissionRequirement(string PermissionCode) : IAuthorizationRequirement;
