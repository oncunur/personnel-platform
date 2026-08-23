using System.Security.Claims;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Audit;
using PersonnelPlatform.Application.Finance;

namespace PersonnelPlatform.Api.Finance;

public static class FinanceEndpoints
{
    public static IEndpointRouteBuilder MapFinanceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance").WithTags("Finance").RequireAuthorization();
        group.MapGet("/cost-ledger", ListCostLedgerAsync).RequirePermission(FinancePermissions.CostView);
        group.MapPost("/cost-ledger/sync", SyncCostLedgerAsync).RequirePermission(FinancePermissions.CostProcess);
        group.MapGet("/payroll-periods/{payrollPeriodId:guid}/employees/{employeeId:guid}/allocation", ListAllocationAsync).RequirePermission(FinancePermissions.AllocationView);
        group.MapPut("/payroll-periods/{payrollPeriodId:guid}/employees/{employeeId:guid}/allocation", ReplaceAllocationAsync).RequirePermission(FinancePermissions.AllocationManage);
        return endpoints;
    }

    private static async Task<IResult> ListCostLedgerAsync(ClaimsPrincipal p, FinanceService service, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        var q = c.Request.Query;
        return ToResult(await service.ListCostLedgerAsync(userId, GuidValue(q, "companyId"), GuidValue(q, "projectId"), GuidValue(q, "costCenterId"), GuidValue(q, "employeeId"), Text(q, "sourceType"), DateValue(q, "from"), DateValue(q, "to"), IntValue(q, "take", 500), ct), c);
    }

    private static async Task<IResult> SyncCostLedgerAsync(ClaimsPrincipal p, FinanceService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        var result = await service.SyncAsync(userId, GuidValue(c.Request.Query, "companyId"), ct);
        await AuditAsync(audit, logs, p, c, "FINANCE_COST_LEDGER_SYNCED", result.Succeeded, null, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, c);
    }

    private static async Task<IResult> ListAllocationAsync(Guid payrollPeriodId, Guid employeeId, ClaimsPrincipal p, FinanceService service, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        return ToResult(await service.ListManualAllocationAsync(userId, payrollPeriodId, employeeId, ct), c);
    }

    private static async Task<IResult> ReplaceAllocationAsync(Guid payrollPeriodId, Guid employeeId, ReplacePayrollAllocationRequest request, ClaimsPrincipal p, FinanceService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    {
        if (!Actor(p, out var userId)) return Unauthorized(c);
        var result = await service.ReplaceManualAllocationAsync(userId, payrollPeriodId, employeeId, request, ct);
        await AuditAsync(audit, logs, p, c, "FINANCE_PAYROLL_ALLOCATION_UPDATED", result.Succeeded, payrollPeriodId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, c);
    }

    private static IResult ToResult<T>(FinanceResult<T> result, HttpContext c, int success = StatusCodes.Status200OK) where T : class
    {
        if (result.Succeeded && result.Value is not null) return Results.Json(result.Value, statusCode: success);
        var code = result.ErrorCode ?? "FINANCE_OPERATION_FAILED";
        var status = code == "SCOPE_DENIED" || code.EndsWith("_DENIED", StringComparison.Ordinal) ? StatusCodes.Status403Forbidden
            : code.EndsWith("_NOT_FOUND", StringComparison.Ordinal) ? StatusCodes.Status404NotFound
            : code is "RECORD_MODIFIED_BY_ANOTHER_USER" or "COST_LEDGER_SOURCE_LOCKED" or "COST_ALLOCATION_DUPLICATE" ? StatusCodes.Status409Conflict
            : StatusCodes.Status422UnprocessableEntity;
        return Error(c, status, code, result.ErrorMessage ?? "Finans işlemi tamamlanamadı.");
    }

    private static async Task AuditAsync(AuditService audit, ILoggerFactory logs, ClaimsPrincipal p, HttpContext c, string eventType, bool succeeded, Guid? entityId, string? errorCode, string? message, CancellationToken ct)
    {
        try { await audit.WriteAsync(new AuditEvent(AuditCategories.Administration, eventType, succeeded, succeeded ? AuditSeverities.Info : AuditSeverities.Warning, Actor(p, out var actor) ? actor : null, p.FindFirstValue("unique_name"), c.Connection.RemoteIpAddress?.ToString(), c.Request.Headers["User-Agent"].ToString(), c.TraceIdentifier, "FINANCE", entityId?.ToString(), errorCode, message), ct); }
        catch (Exception ex) { logs.CreateLogger("FinanceAudit").LogError(ex, "Finance audit write failed for {EventType}.", eventType); }
    }

    private static Guid? GuidValue(IQueryCollection q, string key) => Guid.TryParse(q[key].ToString(), out var value) ? value : null;
    private static DateOnly? DateValue(IQueryCollection q, string key) => DateOnly.TryParse(q[key].ToString(), out var value) ? value : null;
    private static int IntValue(IQueryCollection q, string key, int fallback) => int.TryParse(q[key].ToString(), out var value) ? value : fallback;
    private static string? Text(IQueryCollection q, string key) => string.IsNullOrWhiteSpace(q[key]) ? null : q[key].ToString();
    private static bool Actor(ClaimsPrincipal p, out Guid userId) => Guid.TryParse(p.FindFirstValue("sub"), out userId);
    private static IResult Unauthorized(HttpContext c) => Error(c, StatusCodes.Status401Unauthorized, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");
    private static IResult Error(HttpContext c, int status, string code, string message) => Results.Json(ApiErrorResponse.Create(code, message, c.TraceIdentifier), statusCode: status);
}
