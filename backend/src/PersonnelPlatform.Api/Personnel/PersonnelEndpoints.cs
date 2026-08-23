using System.Security.Claims;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Audit;
using PersonnelPlatform.Application.Personnel;

namespace PersonnelPlatform.Api.Personnel;

public sealed record EmployeeStatusRequest(int Version);

public static class PersonnelEndpoints
{
    public static IEndpointRouteBuilder MapPersonnelEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/personnel").WithTags("Personnel").RequireAuthorization();
        group.MapGet("/employee-types", ListEmployeeTypesAsync).RequirePermission(PersonnelPermissions.View);
        group.MapGet("/employees", SearchEmployeesAsync).RequirePermission(PersonnelPermissions.View);
        group.MapPost("/employees", CreateEmployeeAsync).RequirePermission(PersonnelPermissions.Create);
        group.MapGet("/employees/{employeeId:guid}", GetEmployeeAsync).RequirePermission(PersonnelPermissions.View);
        group.MapPatch("/employees/{employeeId:guid}", UpdateEmployeeAsync).RequirePermission(PersonnelPermissions.Update);
        group.MapPost("/employees/{employeeId:guid}/suspend", SuspendEmployeeAsync).RequirePermission(PersonnelPermissions.Update);
        group.MapPost("/employees/{employeeId:guid}/activate", ActivateEmployeeAsync).RequirePermission(PersonnelPermissions.Update);
        group.MapGet("/employees/{employeeId:guid}/project-assignments", ListProjectAssignmentsAsync).RequirePermission(PersonnelPermissions.ProjectView);
        group.MapPost("/employees/{employeeId:guid}/project-assignments", AssignProjectAsync).RequirePermission(PersonnelPermissions.ProjectAssign);
        return endpoints;
    }

    private static async Task<IResult> ListEmployeeTypesAsync(PersonnelService service, CancellationToken ct) => Results.Ok(await service.ListEmployeeTypesAsync(ct));

    private static async Task<IResult> SearchEmployeesAsync(ClaimsPrincipal principal, PersonnelService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var q = context.Request.Query;
        var query = new EmployeeQuery(
            ReadString(q, "search"), ReadGuid(q, "companyId"), ReadGuid(q, "branchId"), ReadGuid(q, "departmentId"), ReadGuid(q, "positionId"),
            ReadGuid(q, "employeeTypeId"), ReadGuid(q, "projectId"), ReadString(q, "status"), ReadInt(q, "page", 1), ReadInt(q, "pageSize", 25), ReadString(q, "sort"));
        return ToResult(await service.SearchAsync(userId, query, ct), context);
    }

    private static async Task<IResult> GetEmployeeAsync(Guid employeeId, ClaimsPrincipal principal, PersonnelService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.GetAsync(userId, employeeId, ct), context);
    }

    private static async Task<IResult> CreateEmployeeAsync(
        CreateEmployeeRequest request, ClaimsPrincipal principal, PersonnelService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.CreateAsync(userId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "PERSONNEL_CREATED", result.Succeeded, result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task<IResult> UpdateEmployeeAsync(
        Guid employeeId, UpdateEmployeeRequest request, ClaimsPrincipal principal, PersonnelService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.UpdateAsync(userId, employeeId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "PERSONNEL_UPDATED", result.Succeeded, employeeId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context);
    }

    private static Task<IResult> SuspendEmployeeAsync(Guid employeeId, EmployeeStatusRequest request, ClaimsPrincipal principal, PersonnelService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct) =>
        SetEmployeeActiveAsync(employeeId, request.Version, false, principal, service, auditService, loggerFactory, context, ct);

    private static Task<IResult> ActivateEmployeeAsync(Guid employeeId, EmployeeStatusRequest request, ClaimsPrincipal principal, PersonnelService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct) =>
        SetEmployeeActiveAsync(employeeId, request.Version, true, principal, service, auditService, loggerFactory, context, ct);

    private static async Task<IResult> SetEmployeeActiveAsync(
        Guid employeeId, int version, bool active, ClaimsPrincipal principal, PersonnelService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.SetActiveAsync(userId, employeeId, active, version, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, active ? "PERSONNEL_ACTIVATED" : "PERSONNEL_SUSPENDED", result.Succeeded, employeeId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context);
    }

    private static async Task<IResult> ListProjectAssignmentsAsync(Guid employeeId, ClaimsPrincipal principal, PersonnelService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.ListProjectAssignmentsAsync(userId, employeeId, ct), context);
    }

    private static async Task<IResult> AssignProjectAsync(
        Guid employeeId, CreateEmployeeProjectAssignmentRequest request, ClaimsPrincipal principal, PersonnelService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.AssignProjectAsync(userId, employeeId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "PERSONNEL_PROJECT_ASSIGNED", result.Succeeded, employeeId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task AuditAsync(AuditService service, ILoggerFactory loggerFactory, ClaimsPrincipal principal, HttpContext context, string eventType, bool succeeded, Guid? employeeId, string? errorCode, string? message, CancellationToken ct)
    {
        try
        {
            await service.WriteAsync(new AuditEvent(AuditCategories.Administration, eventType, succeeded, succeeded ? AuditSeverities.Info : AuditSeverities.Warning,
                TryActor(principal, out var actor) ? actor : null, principal.FindFirstValue("unique_name"), context.Connection.RemoteIpAddress?.ToString(), context.Request.Headers["User-Agent"].ToString(), context.TraceIdentifier,
                "EMPLOYEE", employeeId?.ToString(), errorCode, message), ct);
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("PersonnelAudit").LogError(exception, "Personnel audit write failed for {EventType}.", eventType);
        }
    }

    private static Guid? ReadGuid(IQueryCollection query, string key) => Guid.TryParse(query[key].ToString(), out var value) ? value : null;
    private static int ReadInt(IQueryCollection query, string key, int fallback) => int.TryParse(query[key].ToString(), out var value) ? value : fallback;
    private static string? ReadString(IQueryCollection query, string key) => string.IsNullOrWhiteSpace(query[key].ToString()) ? null : query[key].ToString();
    private static bool TryActor(ClaimsPrincipal principal, out Guid userId) => Guid.TryParse(principal.FindFirstValue("sub"), out userId);
    private static IResult Unauthorized(HttpContext context) => Error(context, StatusCodes.Status401Unauthorized, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");

    private static IResult ToResult<T>(PersonnelResult<T> result, HttpContext context, int successStatus = StatusCodes.Status200OK) where T : class
    {
        if (result.Succeeded && result.Value is not null) return Results.Json(result.Value, statusCode: successStatus);
        var code = result.ErrorCode ?? "PERSONNEL_OPERATION_FAILED";
        var status = code == "SCOPE_DENIED" ? StatusCodes.Status403Forbidden
            : code.EndsWith("_NOT_FOUND", StringComparison.Ordinal) ? StatusCodes.Status404NotFound
            : code is "EMPLOYEE_NUMBER_ALREADY_EXISTS" or "PROJECT_ASSIGNMENT_CONFLICT" or "RECORD_MODIFIED_BY_ANOTHER_USER" ? StatusCodes.Status409Conflict
            : StatusCodes.Status422UnprocessableEntity;
        return Error(context, status, code, result.ErrorMessage ?? "İşlem tamamlanamadı.");
    }

    private static IResult Error(HttpContext context, int statusCode, string code, string message) => Results.Json(ApiErrorResponse.Create(code, message, context.TraceIdentifier), statusCode: statusCode);
}
