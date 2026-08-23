using System.Security.Claims;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Audit;
using PersonnelPlatform.Application.Authorization;

namespace PersonnelPlatform.Api.Security;

public static class SecurityEndpoints
{
    public static IEndpointRouteBuilder MapSecurityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/security").WithTags("Security").RequireAuthorization();
        group.MapGet("/users", ListUsersAsync).RequirePermission(SystemPermissions.UserView);
        group.MapPost("/users", CreateUserAsync).RequirePermission(SystemPermissions.UserManage);
        group.MapPost("/users/{userId:guid}/activate", ActivateUserAsync).RequirePermission(SystemPermissions.UserManage);
        group.MapPost("/users/{userId:guid}/deactivate", DeactivateUserAsync).RequirePermission(SystemPermissions.UserManage);
        group.MapGet("/roles", ListRolesAsync).RequirePermission(SystemPermissions.RoleView);
        group.MapPost("/roles", CreateRoleAsync).RequirePermission(SystemPermissions.RoleManage);
        group.MapGet("/permissions", ListPermissionsAsync).RequirePermission(SystemPermissions.PermissionView);
        group.MapPut("/roles/{roleId:guid}/permissions", SetRolePermissionsAsync).RequirePermission(SystemPermissions.RoleManage);
        group.MapGet("/users/{userId:guid}/authorization", GetUserAuthorizationAsync).RequirePermission(SystemPermissions.UserView);
        group.MapPut("/users/{userId:guid}/roles", SetUserRolesAsync).RequirePermission(SystemPermissions.UserManage);
        group.MapPut("/users/{userId:guid}/scopes", SetUserScopesAsync).RequirePermission(SystemPermissions.ScopeManage);
        group.MapGet("/scope-check/company/{companyId:guid}", CheckCompanyScopeAsync).RequirePermission(SystemPermissions.ScopeView);
        group.MapGet("/audit", ListAuditAsync).RequirePermission(SystemPermissions.AuditView);
        return endpoints;
    }

    private static Task<IReadOnlyList<SecurityUserSummary>> ListUsersAsync(
        SecurityAdministrationService service,
        CancellationToken ct) => service.ListUsersAsync(ct);

    private static async Task<IResult> CreateUserAsync(
        CreateSecurityUserRequest request,
        SecurityAdministrationService service,
        AuditService auditService,
        ILoggerFactory loggerFactory,
        ClaimsPrincipal principal,
        HttpContext context,
        CancellationToken ct)
    {
        var result = await service.CreateUserAsync(request, ct);
        await AuditSecurityAsync(
            auditService,
            loggerFactory,
            principal,
            context,
            "SECURITY_USER_CREATED",
            result.Succeeded,
            "USER",
            result.Value?.Id.ToString(),
            result.ErrorCode,
            result.ErrorMessage,
            ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task<IResult> ActivateUserAsync(
        Guid userId,
        SecurityAdministrationService service,
        AuditService auditService,
        ILoggerFactory loggerFactory,
        ClaimsPrincipal principal,
        HttpContext context,
        CancellationToken ct)
    {
        var result = await service.SetUserActiveAsync(userId, true, ct);
        await AuditSecurityAsync(auditService, loggerFactory, principal, context, "SECURITY_USER_ACTIVATED", result.Succeeded, "USER", userId.ToString(), result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context);
    }

    private static async Task<IResult> DeactivateUserAsync(
        Guid userId,
        SecurityAdministrationService service,
        AuditService auditService,
        ILoggerFactory loggerFactory,
        ClaimsPrincipal principal,
        HttpContext context,
        CancellationToken ct)
    {
        var result = await service.SetUserActiveAsync(userId, false, ct);
        await AuditSecurityAsync(auditService, loggerFactory, principal, context, "SECURITY_USER_DEACTIVATED", result.Succeeded, "USER", userId.ToString(), result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context);
    }

    private static async Task<IResult> ListRolesAsync(SecurityAdministrationService service, CancellationToken ct)
    {
        var roles = await service.ListRolesAsync(ct);
        return Results.Ok(roles.Select(x => new RoleSummary(x.Id, x.Code, x.Name)));
    }

    private static async Task<IResult> CreateRoleAsync(
        CreateRoleRequest request,
        SecurityAdministrationService service,
        AuditService auditService,
        ILoggerFactory loggerFactory,
        ClaimsPrincipal principal,
        HttpContext context,
        CancellationToken ct)
    {
        var result = await service.CreateRoleAsync(request, ct);
        await AuditSecurityAsync(
            auditService,
            loggerFactory,
            principal,
            context,
            "SECURITY_ROLE_CREATED",
            result.Succeeded,
            "ROLE",
            result.Value?.Id.ToString(),
            result.ErrorCode,
            result.ErrorMessage,
            ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListPermissionsAsync(SecurityAdministrationService service, CancellationToken ct)
    {
        var permissions = await service.ListPermissionsAsync(ct);
        return Results.Ok(permissions.Select(x => new PermissionSummary(x.Id, x.Code, x.Name, x.Module)));
    }

    private static async Task<IResult> SetRolePermissionsAsync(
        Guid roleId,
        SetRolePermissionsRequest request,
        SecurityAdministrationService service,
        AuditService auditService,
        ILoggerFactory loggerFactory,
        ClaimsPrincipal principal,
        HttpContext context,
        CancellationToken ct)
    {
        var result = await service.SetRolePermissionsAsync(roleId, request.PermissionIds ?? Array.Empty<Guid>(), Actor(principal), ct);
        await AuditSecurityAsync(auditService, loggerFactory, principal, context, "SECURITY_ROLE_PERMISSIONS_CHANGED", result.Succeeded, "ROLE", roleId.ToString(), result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context);
    }

    private static async Task<IResult> GetUserAuthorizationAsync(
        Guid userId,
        AccessControlService accessControlService,
        CancellationToken ct) =>
        Results.Ok(await accessControlService.GetSnapshotAsync(userId, ct));

    private static async Task<IResult> SetUserRolesAsync(
        Guid userId,
        SetUserRolesRequest request,
        SecurityAdministrationService service,
        AuditService auditService,
        ILoggerFactory loggerFactory,
        ClaimsPrincipal principal,
        HttpContext context,
        CancellationToken ct)
    {
        var result = await service.SetUserRolesAsync(userId, request.RoleIds ?? Array.Empty<Guid>(), Actor(principal), ct);
        await AuditSecurityAsync(auditService, loggerFactory, principal, context, "SECURITY_USER_ROLES_CHANGED", result.Succeeded, "USER", userId.ToString(), result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context);
    }

    private static async Task<IResult> SetUserScopesAsync(
        Guid userId,
        SetUserScopesRequest request,
        SecurityAdministrationService service,
        AuditService auditService,
        ILoggerFactory loggerFactory,
        ClaimsPrincipal principal,
        HttpContext context,
        CancellationToken ct)
    {
        var result = await service.SetUserScopesAsync(userId, request.Scopes ?? Array.Empty<UserScopeInput>(), Actor(principal), ct);
        await AuditSecurityAsync(auditService, loggerFactory, principal, context, "SECURITY_USER_SCOPES_CHANGED", result.Succeeded, "USER", userId.ToString(), result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context);
    }

    private static async Task<IResult> CheckCompanyScopeAsync(
        Guid companyId,
        ClaimsPrincipal principal,
        AccessControlService accessControlService,
        HttpContext context,
        CancellationToken ct)
    {
        var userId = Actor(principal);
        if (userId is null)
            return Error(context, StatusCodes.Status401Unauthorized, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");

        var allowed = await accessControlService.HasScopeAsync(userId.Value, ScopeTypes.Company, companyId, ct);
        return Results.Ok(new ScopeCheckResponse(ScopeTypes.Company, companyId, allowed));
    }

    private static async Task<IResult> ListAuditAsync(
        string? category,
        string? eventType,
        Guid? actorUserId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int? take,
        AuditService auditService,
        CancellationToken ct) =>
        Results.Ok(await auditService.ListAsync(category, eventType, actorUserId, from, to, take ?? 100, ct));

    private static async Task AuditSecurityAsync(
        AuditService auditService,
        ILoggerFactory loggerFactory,
        ClaimsPrincipal principal,
        HttpContext context,
        string eventType,
        bool succeeded,
        string? targetType,
        string? targetId,
        string? errorCode,
        string? message,
        CancellationToken cancellationToken)
    {
        try
        {
            await auditService.WriteAsync(
                new AuditEvent(
                    AuditCategories.Security,
                    eventType,
                    succeeded,
                    succeeded ? AuditSeverities.Info : AuditSeverities.Warning,
                    Actor(principal),
                    principal.FindFirstValue("unique_name"),
                    context.Connection.RemoteIpAddress?.ToString(),
                    context.Request.Headers["User-Agent"].ToString(),
                    context.TraceIdentifier,
                    targetType,
                    targetId,
                    errorCode,
                    message),
                cancellationToken);
        }
        catch (Exception exception)
        {
            loggerFactory
                .CreateLogger("SecurityAudit")
                .LogError(exception, "Security audit write failed for event {EventType}.", eventType);
        }
    }

    private static Guid? Actor(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue("sub"), out var userId) ? userId : null;

    private static IResult ToResult<T>(
        SecurityResult<T> result,
        HttpContext context,
        int successStatus = StatusCodes.Status200OK) where T : class
    {
        if (result.Succeeded && result.Value is not null)
            return Results.Json(result.Value, statusCode: successStatus);

        var code = result.ErrorCode ?? "SECURITY_OPERATION_FAILED";
        var status = code.EndsWith("_NOT_FOUND", StringComparison.Ordinal)
            ? StatusCodes.Status404NotFound
            : code.EndsWith("_ALREADY_EXISTS", StringComparison.Ordinal)
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status422UnprocessableEntity;

        return Error(context, status, code, result.ErrorMessage ?? "İşlem tamamlanamadı.");
    }

    private static IResult Error(HttpContext context, int statusCode, string code, string message) =>
        Results.Json(ApiErrorResponse.Create(code, message, context.TraceIdentifier), statusCode: statusCode);
}
