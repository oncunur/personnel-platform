using System.Security.Claims;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Audit;
using PersonnelPlatform.Application.Organization;

namespace PersonnelPlatform.Api.Organization;

public static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/organization").WithTags("Organization").RequireAuthorization();

        group.MapGet("/companies", ListCompaniesAsync).RequirePermission(OrganizationPermissions.CompanyView);
        group.MapPost("/companies", CreateCompanyAsync).RequirePermission(OrganizationPermissions.CompanyManage);

        group.MapGet("/branches", ListBranchesAsync).RequirePermission(OrganizationPermissions.BranchView);
        group.MapPost("/branches", CreateBranchAsync).RequirePermission(OrganizationPermissions.BranchManage);

        group.MapGet("/departments", ListDepartmentsAsync).RequirePermission(OrganizationPermissions.DepartmentView);
        group.MapPost("/departments", CreateDepartmentAsync).RequirePermission(OrganizationPermissions.DepartmentManage);

        group.MapGet("/positions", ListPositionsAsync).RequirePermission(OrganizationPermissions.PositionView);
        group.MapPost("/positions", CreatePositionAsync).RequirePermission(OrganizationPermissions.PositionManage);

        group.MapGet("/projects", ListProjectsAsync).RequirePermission(OrganizationPermissions.ProjectView);
        group.MapPost("/projects", CreateProjectAsync).RequirePermission(OrganizationPermissions.ProjectManage);

        group.MapGet("/cost-centers", ListCostCentersAsync).RequirePermission(OrganizationPermissions.CostCenterView);
        group.MapPost("/cost-centers", CreateCostCenterAsync).RequirePermission(OrganizationPermissions.CostCenterManage);
        return endpoints;
    }

    private static async Task<IResult> ListCompaniesAsync(
        ClaimsPrincipal principal,
        OrganizationService service,
        HttpContext context,
        CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return Results.Ok(await service.ListCompaniesAsync(userId, ct));
    }

    private static async Task<IResult> CreateCompanyAsync(
        CreateCompanyRequest request,
        ClaimsPrincipal principal,
        OrganizationService service,
        AuditService auditService,
        ILoggerFactory loggerFactory,
        HttpContext context,
        CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.CreateCompanyAsync(userId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "ORGANIZATION_COMPANY_CREATED", result.Succeeded, "COMPANY", result.Value?.Id.ToString(), result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListBranchesAsync(
        Guid companyId,
        ClaimsPrincipal principal,
        OrganizationService service,
        HttpContext context,
        CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.ListBranchesAsync(userId, companyId, ct), context);
    }

    private static async Task<IResult> CreateBranchAsync(
        CreateBranchRequest request,
        ClaimsPrincipal principal,
        OrganizationService service,
        AuditService auditService,
        ILoggerFactory loggerFactory,
        HttpContext context,
        CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.CreateBranchAsync(userId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "ORGANIZATION_BRANCH_CREATED", result.Succeeded, "BRANCH", result.Value?.Id.ToString(), result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListDepartmentsAsync(
        Guid companyId,
        ClaimsPrincipal principal,
        OrganizationService service,
        HttpContext context,
        CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.ListDepartmentsAsync(userId, companyId, ct), context);
    }

    private static async Task<IResult> CreateDepartmentAsync(
        CreateDepartmentRequest request,
        ClaimsPrincipal principal,
        OrganizationService service,
        AuditService auditService,
        ILoggerFactory loggerFactory,
        HttpContext context,
        CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.CreateDepartmentAsync(userId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "ORGANIZATION_DEPARTMENT_CREATED", result.Succeeded, "DEPARTMENT", result.Value?.Id.ToString(), result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListPositionsAsync(
        Guid departmentId,
        ClaimsPrincipal principal,
        OrganizationService service,
        HttpContext context,
        CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.ListPositionsAsync(userId, departmentId, ct), context);
    }

    private static async Task<IResult> CreatePositionAsync(
        CreatePositionRequest request,
        ClaimsPrincipal principal,
        OrganizationService service,
        AuditService auditService,
        ILoggerFactory loggerFactory,
        HttpContext context,
        CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.CreatePositionAsync(userId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "ORGANIZATION_POSITION_CREATED", result.Succeeded, "POSITION", result.Value?.Id.ToString(), result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListProjectsAsync(
        Guid companyId,
        ClaimsPrincipal principal,
        OrganizationService service,
        HttpContext context,
        CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.ListProjectsAsync(userId, companyId, ct), context);
    }

    private static async Task<IResult> CreateProjectAsync(
        CreateProjectRequest request,
        ClaimsPrincipal principal,
        OrganizationService service,
        AuditService auditService,
        ILoggerFactory loggerFactory,
        HttpContext context,
        CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.CreateProjectAsync(userId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "ORGANIZATION_PROJECT_CREATED", result.Succeeded, "PROJECT", result.Value?.Id.ToString(), result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListCostCentersAsync(
        Guid companyId,
        ClaimsPrincipal principal,
        OrganizationService service,
        HttpContext context,
        CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.ListCostCentersAsync(userId, companyId, ct), context);
    }

    private static async Task<IResult> CreateCostCenterAsync(
        CreateCostCenterRequest request,
        ClaimsPrincipal principal,
        OrganizationService service,
        AuditService auditService,
        ILoggerFactory loggerFactory,
        HttpContext context,
        CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.CreateCostCenterAsync(userId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "ORGANIZATION_COST_CENTER_CREATED", result.Succeeded, "COST_CENTER", result.Value?.Id.ToString(), result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task AuditAsync(
        AuditService auditService,
        ILoggerFactory loggerFactory,
        ClaimsPrincipal principal,
        HttpContext context,
        string eventType,
        bool succeeded,
        string targetType,
        string? targetId,
        string? errorCode,
        string? message,
        CancellationToken ct)
    {
        try
        {
            await auditService.WriteAsync(
                new AuditEvent(
                    AuditCategories.Administration,
                    eventType,
                    succeeded,
                    succeeded ? AuditSeverities.Info : AuditSeverities.Warning,
                    TryActor(principal, out var actor) ? actor : null,
                    principal.FindFirstValue("unique_name"),
                    context.Connection.RemoteIpAddress?.ToString(),
                    context.Request.Headers["User-Agent"].ToString(),
                    context.TraceIdentifier,
                    targetType,
                    targetId,
                    errorCode,
                    message),
                ct);
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("OrganizationAudit").LogError(exception, "Organization audit write failed for {EventType}.", eventType);
        }
    }

    private static bool TryActor(ClaimsPrincipal principal, out Guid userId) => Guid.TryParse(principal.FindFirstValue("sub"), out userId);
    private static IResult Unauthorized(HttpContext context) => Error(context, StatusCodes.Status401Unauthorized, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");

    private static IResult ToResult<T>(OrganizationResult<T> result, HttpContext context, int successStatus = StatusCodes.Status200OK) where T : class
    {
        if (result.Succeeded && result.Value is not null) return Results.Json(result.Value, statusCode: successStatus);
        var code = result.ErrorCode ?? "ORGANIZATION_OPERATION_FAILED";
        var status = code == "SCOPE_DENIED" ? StatusCodes.Status403Forbidden
            : code.EndsWith("_NOT_FOUND", StringComparison.Ordinal) ? StatusCodes.Status404NotFound
            : code.EndsWith("_ALREADY_EXISTS", StringComparison.Ordinal) ? StatusCodes.Status409Conflict
            : StatusCodes.Status422UnprocessableEntity;
        return Error(context, status, code, result.ErrorMessage ?? "İşlem tamamlanamadı.");
    }

    private static IResult Error(HttpContext context, int statusCode, string code, string message) =>
        Results.Json(ApiErrorResponse.Create(code, message, context.TraceIdentifier), statusCode: statusCode);
}
