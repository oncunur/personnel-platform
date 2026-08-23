using System.Security.Claims;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Audit;
using PersonnelPlatform.Application.Leave;

namespace PersonnelPlatform.Api.Leave;

public static class LeaveEndpoints
{
    public static IEndpointRouteBuilder MapLeaveEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/leave").WithTags("Leave").RequireAuthorization();
        group.MapGet("/types", ListTypesAsync).RequirePermission(LeavePermissions.TypeView);
        group.MapPost("/types", CreateTypeAsync).RequirePermission(LeavePermissions.TypeManage);
        group.MapGet("/requests", SearchAsync).RequirePermission(LeavePermissions.View);
        group.MapGet("/requests/{leaveId:guid}", GetAsync).RequirePermission(LeavePermissions.View);
        group.MapPost("/requests", CreateDraftAsync).RequirePermission(LeavePermissions.Create);
        group.MapPost("/requests/{leaveId:guid}/submit", SubmitAsync).RequirePermission(LeavePermissions.Submit);
        group.MapPost("/requests/{leaveId:guid}/withdraw", WithdrawAsync).RequirePermission(LeavePermissions.Submit);
        group.MapGet("/employees/{employeeId:guid}/balances", ListBalancesAsync).RequirePermission(LeavePermissions.BalanceView);
        group.MapPut("/employees/{employeeId:guid}/entitlements", UpsertEntitlementAsync).RequirePermission(LeavePermissions.BalanceManage);
        return endpoints;
    }

    private static async Task<IResult> ListTypesAsync(LeaveService service, CancellationToken ct) => Results.Ok(await service.ListTypesAsync(ct));

    private static async Task<IResult> CreateTypeAsync(CreateLeaveTypeRequest request, ClaimsPrincipal principal, LeaveService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.CreateTypeAsync(userId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "LEAVE_TYPE_CREATED", result.Succeeded, "LEAVE_TYPE", result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task<IResult> SearchAsync(ClaimsPrincipal principal, LeaveService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var q = context.Request.Query;
        var query = new LeaveQuery(
            ReadGuid(q, "employeeId"),
            ReadGuid(q, "companyId"),
            ReadGuid(q, "leaveTypeId"),
            ReadString(q, "status"),
            ReadDate(q, "from"),
            ReadDate(q, "to"),
            ReadInt(q, "page", 1),
            ReadInt(q, "pageSize", 25));
        return ToResult(await service.SearchAsync(userId, query, ct), context);
    }

    private static async Task<IResult> GetAsync(Guid leaveId, ClaimsPrincipal principal, LeaveService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.GetAsync(userId, leaveId, ct), context);
    }

    private static async Task<IResult> CreateDraftAsync(CreateLeaveRequest request, ClaimsPrincipal principal, LeaveService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.CreateDraftAsync(userId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "LEAVE_DRAFT_CREATED", result.Succeeded, "LEAVE", result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task<IResult> SubmitAsync(Guid leaveId, LeaveActionRequest request, ClaimsPrincipal principal, LeaveService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.SubmitAsync(userId, leaveId, request.Version, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "LEAVE_SUBMITTED", result.Succeeded, "LEAVE", leaveId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context);
    }

    private static async Task<IResult> WithdrawAsync(Guid leaveId, LeaveActionRequest request, ClaimsPrincipal principal, LeaveService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.WithdrawAsync(userId, leaveId, request.Version, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "LEAVE_WITHDRAWN", result.Succeeded, "LEAVE", leaveId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context);
    }

    private static async Task<IResult> ListBalancesAsync(Guid employeeId, ClaimsPrincipal principal, LeaveService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.ListBalancesAsync(userId, employeeId, ct), context);
    }

    private static async Task<IResult> UpsertEntitlementAsync(Guid employeeId, UpsertLeaveEntitlementRequest request, ClaimsPrincipal principal, LeaveService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.UpsertEntitlementAsync(userId, employeeId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "LEAVE_ENTITLEMENT_UPSERTED", result.Succeeded, "EMPLOYEE", employeeId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context);
    }

    private static Guid? ReadGuid(IQueryCollection query, string key) => Guid.TryParse(query[key].ToString(), out var value) ? value : null;
    private static DateOnly? ReadDate(IQueryCollection query, string key) => DateOnly.TryParse(query[key].ToString(), out var value) ? value : null;
    private static int ReadInt(IQueryCollection query, string key, int fallback) => int.TryParse(query[key].ToString(), out var value) ? value : fallback;
    private static string? ReadString(IQueryCollection query, string key) => string.IsNullOrWhiteSpace(query[key].ToString()) ? null : query[key].ToString();
    private static bool TryActor(ClaimsPrincipal principal, out Guid userId) => Guid.TryParse(principal.FindFirstValue("sub"), out userId);
    private static IResult Unauthorized(HttpContext context) => Error(context, StatusCodes.Status401Unauthorized, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");

    private static IResult ToResult<T>(LeaveResult<T> result, HttpContext context, int successStatus = StatusCodes.Status200OK) where T : class
    {
        if (result.Succeeded && result.Value is not null) return Results.Json(result.Value, statusCode: successStatus);
        var code = result.ErrorCode ?? "LEAVE_OPERATION_FAILED";
        var status = code == "SCOPE_DENIED" ? StatusCodes.Status403Forbidden
            : code.EndsWith("_NOT_FOUND", StringComparison.Ordinal) ? StatusCodes.Status404NotFound
            : code is "LEAVE_TYPE_ALREADY_EXISTS" or "LEAVE_DATE_CONFLICT" or "LEAVE_ENTITLEMENT_PERIOD_CONFLICT" or "RECORD_MODIFIED_BY_ANOTHER_USER" ? StatusCodes.Status409Conflict
            : StatusCodes.Status422UnprocessableEntity;
        return Error(context, status, code, result.ErrorMessage ?? "İşlem tamamlanamadı.");
    }

    private static async Task AuditAsync(AuditService service, ILoggerFactory loggerFactory, ClaimsPrincipal principal, HttpContext context, string eventType, bool succeeded, string entityType, Guid? entityId, string? errorCode, string? message, CancellationToken ct)
    {
        try
        {
            await service.WriteAsync(new AuditEvent(
                AuditCategories.Administration,
                eventType,
                succeeded,
                succeeded ? AuditSeverities.Info : AuditSeverities.Warning,
                TryActor(principal, out var actor) ? actor : null,
                principal.FindFirstValue("unique_name"),
                context.Connection.RemoteIpAddress?.ToString(),
                context.Request.Headers["User-Agent"].ToString(),
                context.TraceIdentifier,
                entityType,
                entityId?.ToString(),
                errorCode,
                message), ct);
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("LeaveAudit").LogError(exception, "Leave audit write failed for {EventType}.", eventType);
        }
    }

    private static IResult Error(HttpContext context, int statusCode, string code, string message) =>
        Results.Json(ApiErrorResponse.Create(code, message, context.TraceIdentifier), statusCode: statusCode);
}
