using System.Security.Claims;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Audit;
using PersonnelPlatform.Application.Payroll;

namespace PersonnelPlatform.Api.Payroll;

public static class PayrollEndpoints
{
    public static IEndpointRouteBuilder MapPayrollEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/payroll").WithTags("Payroll").RequireAuthorization();
        group.MapGet("/compensations", ListCompensationsAsync).RequirePermission(PayrollPermissions.CompensationView);
        group.MapPost("/compensations", CreateCompensationAsync).RequirePermission(PayrollPermissions.CompensationManage);
        group.MapGet("/periods", ListPeriodsAsync).RequirePermission(PayrollPermissions.PeriodView);
        group.MapPost("/periods", CreatePeriodAsync).RequirePermission(PayrollPermissions.PeriodManage);
        group.MapPost("/periods/{periodId:guid}/open", OpenAsync).RequirePermission(PayrollPermissions.PeriodManage);
        group.MapPost("/periods/{periodId:guid}/calculate", CalculateAsync).RequirePermission(PayrollPermissions.Calculate);
        group.MapPost("/periods/{periodId:guid}/review", ReviewAsync).RequirePermission(PayrollPermissions.Review);
        group.MapPost("/periods/{periodId:guid}/approve", ApproveAsync).RequirePermission(PayrollPermissions.Approve);
        group.MapPost("/periods/{periodId:guid}/close", CloseAsync).RequirePermission(PayrollPermissions.Close);
        group.MapGet("/periods/{periodId:guid}/results", ListResultsAsync).RequirePermission(PayrollPermissions.PeriodView);
        return endpoints;
    }

    private static async Task<IResult> ListCompensationsAsync(ClaimsPrincipal principal, PayrollService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        if (!Guid.TryParse(context.Request.Query["employeeId"].ToString(), out var employeeId))
            return Error(context, StatusCodes.Status422UnprocessableEntity, "EMPLOYEE_ID_REQUIRED", "Personel seçilmelidir.");
        return ToResult(await service.ListCompensationsAsync(userId, employeeId, ct), context);
    }

    private static async Task<IResult> CreateCompensationAsync(CreateEmployeeCompensationRequest request, ClaimsPrincipal principal, PayrollService service, AuditService audit, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.CreateCompensationAsync(userId, request, ct);
        await AuditAsync(audit, loggerFactory, principal, context, "PAYROLL_COMPENSATION_CREATED", result.Succeeded, result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListPeriodsAsync(ClaimsPrincipal principal, PayrollService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var year = int.TryParse(context.Request.Query["year"].ToString(), out var value) ? value : (int?)null;
        return ToResult(await service.ListPeriodsAsync(userId, year, ct), context);
    }

    private static async Task<IResult> CreatePeriodAsync(CreatePayrollPeriodRequest request, ClaimsPrincipal principal, PayrollService service, AuditService audit, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.CreatePeriodAsync(userId, request, ct);
        await AuditAsync(audit, loggerFactory, principal, context, "PAYROLL_PERIOD_CREATED", result.Succeeded, result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static Task<IResult> OpenAsync(Guid periodId, PayrollPeriodActionRequest request, ClaimsPrincipal principal, PayrollService service, AuditService audit, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct) =>
        RunActionAsync(periodId, request, principal, service.OpenPeriodAsync, audit, loggerFactory, context, "PAYROLL_PERIOD_OPENED", ct);

    private static Task<IResult> CalculateAsync(Guid periodId, PayrollPeriodActionRequest request, ClaimsPrincipal principal, PayrollService service, AuditService audit, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct) =>
        RunActionAsync(periodId, request, principal, service.CalculateAsync, audit, loggerFactory, context, "PAYROLL_PERIOD_CALCULATED", ct);

    private static Task<IResult> ReviewAsync(Guid periodId, PayrollPeriodActionRequest request, ClaimsPrincipal principal, PayrollService service, AuditService audit, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct) =>
        RunActionAsync(periodId, request, principal, service.StartReviewAsync, audit, loggerFactory, context, "PAYROLL_PERIOD_REVIEW_STARTED", ct);

    private static Task<IResult> ApproveAsync(Guid periodId, PayrollPeriodActionRequest request, ClaimsPrincipal principal, PayrollService service, AuditService audit, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct) =>
        RunActionAsync(periodId, request, principal, service.ApproveAsync, audit, loggerFactory, context, "PAYROLL_PERIOD_APPROVED", ct);

    private static Task<IResult> CloseAsync(Guid periodId, PayrollPeriodActionRequest request, ClaimsPrincipal principal, PayrollService service, AuditService audit, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct) =>
        RunActionAsync(periodId, request, principal, service.CloseAsync, audit, loggerFactory, context, "PAYROLL_PERIOD_CLOSED", ct);

    private static async Task<IResult> ListResultsAsync(Guid periodId, ClaimsPrincipal principal, PayrollService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.ListResultsAsync(userId, periodId, ct), context);
    }

    private static async Task<IResult> RunActionAsync(
        Guid periodId,
        PayrollPeriodActionRequest request,
        ClaimsPrincipal principal,
        Func<Guid, Guid, PayrollPeriodActionRequest, CancellationToken, Task<PayrollResult<PayrollPeriodSummary>>> action,
        AuditService audit,
        ILoggerFactory loggerFactory,
        HttpContext context,
        string eventType,
        CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await action(userId, periodId, request, ct);
        await AuditAsync(audit, loggerFactory, principal, context, eventType, result.Succeeded, periodId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context);
    }

    private static IResult ToResult<T>(PayrollResult<T> result, HttpContext context, int successStatus = StatusCodes.Status200OK) where T : class
    {
        if (result.Succeeded && result.Value is not null) return Results.Json(result.Value, statusCode: successStatus);
        var code = result.ErrorCode ?? "PAYROLL_OPERATION_FAILED";
        var status = code == "SCOPE_DENIED" || code.EndsWith("_DENIED", StringComparison.Ordinal)
            ? StatusCodes.Status403Forbidden
            : code.EndsWith("_NOT_FOUND", StringComparison.Ordinal) ? StatusCodes.Status404NotFound
            : code is "PAYROLL_COMPENSATION_DATE_CONFLICT" or "PAYROLL_PERIOD_ALREADY_ACTIVE" or "PAYROLL_PERIOD_STATE_INVALID" or "RECORD_MODIFIED_BY_ANOTHER_USER"
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status422UnprocessableEntity;
        return Error(context, status, code, result.ErrorMessage ?? "İşlem tamamlanamadı.");
    }

    private static bool TryActor(ClaimsPrincipal principal, out Guid userId) => Guid.TryParse(principal.FindFirstValue("sub"), out userId);
    private static IResult Unauthorized(HttpContext context) => Error(context, StatusCodes.Status401Unauthorized, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");
    private static IResult Error(HttpContext context, int statusCode, string code, string message) => Results.Json(ApiErrorResponse.Create(code, message, context.TraceIdentifier), statusCode: statusCode);

    private static async Task AuditAsync(AuditService service, ILoggerFactory loggerFactory, ClaimsPrincipal principal, HttpContext context, string eventType, bool succeeded, Guid? entityId, string? errorCode, string? message, CancellationToken ct)
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
                "PAYROLL",
                entityId?.ToString(),
                errorCode,
                message), ct);
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("PayrollAudit").LogError(exception, "Payroll audit write failed for {EventType}.", eventType);
        }
    }
}
