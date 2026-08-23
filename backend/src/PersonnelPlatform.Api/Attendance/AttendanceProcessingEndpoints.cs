using System.Security.Claims;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Attendance;
using PersonnelPlatform.Application.Audit;

namespace PersonnelPlatform.Api.Attendance;

public static class AttendanceProcessingEndpoints
{
    public static IEndpointRouteBuilder MapAttendanceProcessingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/attendance").WithTags("Attendance Processing").RequireAuthorization();
        group.MapPost("/raw-events", IngestRawAsync).RequirePermission(AttendanceProcessingPermissions.RawIngest);
        group.MapGet("/employees/{employeeId:guid}/raw-events", ListRawAsync).RequirePermission(AttendanceProcessingPermissions.RawView);
        group.MapPost("/daily/calculate", CalculateDailyAsync).RequirePermission(AttendanceProcessingPermissions.DailyCalculate);
        group.MapGet("/employees/{employeeId:guid}/daily", ListDailyAsync).RequirePermission(AttendanceProcessingPermissions.DailyView);
        return endpoints;
    }

    private static async Task<IResult> IngestRawAsync(CreateRawAttendanceEventRequest request, ClaimsPrincipal principal, AttendanceProcessingService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.IngestRawAsync(userId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "RAW_ATTENDANCE_EVENT_INGESTED", result.Succeeded, "RAW_ATTENDANCE_EVENT", result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListRawAsync(Guid employeeId, DateOnly? from, DateOnly? to, ClaimsPrincipal principal, AttendanceProcessingService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? end.AddDays(-7);
        return ToResult(await service.ListRawAsync(userId, employeeId, start, end, ct), context);
    }

    private static async Task<IResult> CalculateDailyAsync(CalculateDailyAttendanceRequest request, ClaimsPrincipal principal, AttendanceProcessingService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.CalculateDailyAsync(userId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "DAILY_ATTENDANCE_CALCULATED", result.Succeeded, "DAILY_ATTENDANCE", result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context);
    }

    private static async Task<IResult> ListDailyAsync(Guid employeeId, DateOnly? from, DateOnly? to, ClaimsPrincipal principal, AttendanceProcessingService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? new DateOnly(end.Year, end.Month, 1);
        return ToResult(await service.ListDailyAsync(userId, employeeId, start, end, ct), context);
    }

    private static IResult ToResult<T>(AttendanceResult<T> result, HttpContext context, int successStatus = StatusCodes.Status200OK) where T : class
    {
        if (result.Succeeded && result.Value is not null) return Results.Json(result.Value, statusCode: successStatus);
        var code = result.ErrorCode ?? "ATTENDANCE_PROCESSING_FAILED";
        var status = code == "SCOPE_DENIED" ? StatusCodes.Status403Forbidden
            : code.EndsWith("_NOT_FOUND", StringComparison.Ordinal) ? StatusCodes.Status404NotFound
            : code is "DAILY_ATTENDANCE_LOCKED" ? StatusCodes.Status409Conflict
            : StatusCodes.Status422UnprocessableEntity;
        return Error(context, status, code, result.ErrorMessage ?? "İşlem tamamlanamadı.");
    }

    private static bool TryActor(ClaimsPrincipal principal, out Guid userId) => Guid.TryParse(principal.FindFirstValue("sub"), out userId);
    private static IResult Unauthorized(HttpContext context) => Error(context, StatusCodes.Status401Unauthorized, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");
    private static IResult Error(HttpContext context, int statusCode, string code, string message) => Results.Json(ApiErrorResponse.Create(code, message, context.TraceIdentifier), statusCode: statusCode);

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
            loggerFactory.CreateLogger("AttendanceProcessingAudit").LogError(exception, "Attendance processing audit write failed for {EventType}.", eventType);
        }
    }
}
