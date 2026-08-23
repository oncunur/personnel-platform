using System.Security.Claims;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Attendance;
using PersonnelPlatform.Application.Audit;

namespace PersonnelPlatform.Api.Attendance;

public static class OvertimeEndpoints
{
    public static IEndpointRouteBuilder MapOvertimeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/attendance/overtime").WithTags("Overtime").RequireAuthorization();
        group.MapGet("/", SearchAsync).RequirePermission(OvertimePermissions.View);
        group.MapPost("/", CreateAsync).RequirePermission(OvertimePermissions.Request);
        group.MapGet("/inbox", InboxAsync);
        group.MapPost("/{overtimeId:guid}/decision", DecideAsync);
        group.MapPost("/{overtimeId:guid}/cancel", CancelAsync).RequirePermission(OvertimePermissions.Request);
        return endpoints;
    }

    private static async Task<IResult> SearchAsync(ClaimsPrincipal principal, OvertimeService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var q = context.Request.Query;
        var query = new OvertimeQuery(
            ReadGuid(q, "employeeId"),
            ReadGuid(q, "companyId"),
            ReadString(q, "status"),
            ReadDate(q, "from"),
            ReadDate(q, "to"),
            ReadInt(q, "page", 1),
            ReadInt(q, "pageSize", 50));
        return ToResult(await service.SearchAsync(userId, query, ct), context);
    }

    private static async Task<IResult> CreateAsync(CreateOvertimeRequest request, ClaimsPrincipal principal, OvertimeService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.CreateAsync(userId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "OVERTIME_REQUEST_CREATED", result.Succeeded, result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task<IResult> InboxAsync(ClaimsPrincipal principal, OvertimeService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.ListInboxAsync(userId, ct), context);
    }

    private static async Task<IResult> DecideAsync(Guid overtimeId, OvertimeDecisionRequest request, ClaimsPrincipal principal, OvertimeService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.DecideAsync(userId, overtimeId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, request.Approve ? "OVERTIME_APPROVED_STEP" : "OVERTIME_REJECTED", result.Succeeded, overtimeId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context);
    }

    private static async Task<IResult> CancelAsync(Guid overtimeId, OvertimeCancelRequest request, ClaimsPrincipal principal, OvertimeService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.CancelAsync(userId, overtimeId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "OVERTIME_REQUEST_CANCELLED", result.Succeeded, overtimeId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context);
    }

    private static IResult ToResult<T>(AttendanceResult<T> result, HttpContext context, int successStatus = StatusCodes.Status200OK) where T : class
    {
        if (result.Succeeded && result.Value is not null) return Results.Json(result.Value, statusCode: successStatus);
        var code = result.ErrorCode ?? "OVERTIME_OPERATION_FAILED";
        var status = code == "SCOPE_DENIED" || code.EndsWith("_DENIED", StringComparison.Ordinal) || code.EndsWith("_IDENTITY_MISMATCH", StringComparison.Ordinal)
            ? StatusCodes.Status403Forbidden
            : code.EndsWith("_NOT_FOUND", StringComparison.Ordinal) ? StatusCodes.Status404NotFound
            : code is "OVERTIME_REQUEST_ALREADY_EXISTS" or "OVERTIME_REQUEST_NOT_PENDING" or "OVERTIME_STATE_INVALID" or "OVERTIME_CANCEL_NOT_ALLOWED" or "RECORD_MODIFIED_BY_ANOTHER_USER"
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status422UnprocessableEntity;
        return Error(context, status, code, result.ErrorMessage ?? "İşlem tamamlanamadı.");
    }

    private static Guid? ReadGuid(IQueryCollection query, string key) => Guid.TryParse(query[key].ToString(), out var value) ? value : null;
    private static DateOnly? ReadDate(IQueryCollection query, string key) => DateOnly.TryParse(query[key].ToString(), out var value) ? value : null;
    private static int ReadInt(IQueryCollection query, string key, int fallback) => int.TryParse(query[key].ToString(), out var value) ? value : fallback;
    private static string? ReadString(IQueryCollection query, string key) => string.IsNullOrWhiteSpace(query[key].ToString()) ? null : query[key].ToString();
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
                "OVERTIME_REQUEST",
                entityId?.ToString(),
                errorCode,
                message), ct);
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("OvertimeAudit").LogError(exception, "Overtime audit write failed for {EventType}.", eventType);
        }
    }
}
