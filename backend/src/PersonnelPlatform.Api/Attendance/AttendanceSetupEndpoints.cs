using System.Security.Claims;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Attendance;
using PersonnelPlatform.Application.Audit;

namespace PersonnelPlatform.Api.Attendance;

public static class AttendanceSetupEndpoints
{
    public static IEndpointRouteBuilder MapAttendanceSetupEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/attendance").WithTags("Attendance").RequireAuthorization();
        group.MapGet("/calendars", ListCalendarsAsync).RequirePermission(AttendancePermissions.CalendarView);
        group.MapPost("/calendars", CreateCalendarAsync).RequirePermission(AttendancePermissions.CalendarManage);
        group.MapGet("/calendars/{calendarId:guid}/days", ListCalendarDaysAsync).RequirePermission(AttendancePermissions.CalendarView);
        group.MapPut("/calendars/{calendarId:guid}/days", UpsertCalendarDayAsync).RequirePermission(AttendancePermissions.CalendarManage);
        group.MapGet("/shifts", ListShiftsAsync).RequirePermission(AttendancePermissions.ShiftView);
        group.MapPost("/shifts", CreateShiftAsync).RequirePermission(AttendancePermissions.ShiftManage);
        group.MapGet("/employees/{employeeId:guid}/shift-assignments", ListAssignmentsAsync).RequirePermission(AttendancePermissions.AssignmentView);
        group.MapPost("/employees/{employeeId:guid}/shift-assignments", AssignShiftAsync).RequirePermission(AttendancePermissions.AssignmentManage);
        return endpoints;
    }

    private static async Task<IResult> ListCalendarsAsync(Guid? companyId, ClaimsPrincipal principal, AttendanceSetupService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.ListCalendarsAsync(userId, companyId, ct), context);
    }

    private static async Task<IResult> CreateCalendarAsync(CreateWorkCalendarRequest request, ClaimsPrincipal principal, AttendanceSetupService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.CreateCalendarAsync(userId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "WORK_CALENDAR_CREATED", result.Succeeded, "WORK_CALENDAR", result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListCalendarDaysAsync(Guid calendarId, DateOnly? from, DateOnly? to, ClaimsPrincipal principal, AttendanceSetupService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.ListCalendarDaysAsync(userId, calendarId, from, to, ct), context);
    }

    private static async Task<IResult> UpsertCalendarDayAsync(Guid calendarId, UpsertWorkCalendarDayRequest request, ClaimsPrincipal principal, AttendanceSetupService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.UpsertCalendarDayAsync(userId, calendarId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "WORK_CALENDAR_DAY_UPSERTED", result.Succeeded, "WORK_CALENDAR", calendarId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context);
    }

    private static async Task<IResult> ListShiftsAsync(Guid? companyId, ClaimsPrincipal principal, AttendanceSetupService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.ListShiftsAsync(userId, companyId, ct), context);
    }

    private static async Task<IResult> CreateShiftAsync(CreateShiftRequest request, ClaimsPrincipal principal, AttendanceSetupService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.CreateShiftAsync(userId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "SHIFT_CREATED", result.Succeeded, "SHIFT", result.Value?.Id, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListAssignmentsAsync(Guid employeeId, ClaimsPrincipal principal, AttendanceSetupService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.ListAssignmentsAsync(userId, employeeId, ct), context);
    }

    private static async Task<IResult> AssignShiftAsync(Guid employeeId, CreateEmployeeShiftAssignmentRequest request, ClaimsPrincipal principal, AttendanceSetupService service, AuditService auditService, ILoggerFactory loggerFactory, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.AssignShiftAsync(userId, employeeId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context, "EMPLOYEE_SHIFT_ASSIGNED", result.Succeeded, "EMPLOYEE", employeeId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context, StatusCodes.Status201Created);
    }

    private static IResult ToResult<T>(AttendanceResult<T> result, HttpContext context, int successStatus = StatusCodes.Status200OK) where T : class
    {
        if (result.Succeeded && result.Value is not null) return Results.Json(result.Value, statusCode: successStatus);
        var code = result.ErrorCode ?? "ATTENDANCE_OPERATION_FAILED";
        var status = code == "SCOPE_DENIED" ? StatusCodes.Status403Forbidden
            : code.EndsWith("_NOT_FOUND", StringComparison.Ordinal) ? StatusCodes.Status404NotFound
            : code is "WORK_CALENDAR_CODE_EXISTS" or "DEFAULT_WORK_CALENDAR_EXISTS" or "SHIFT_CODE_EXISTS" or "SHIFT_ASSIGNMENT_DATE_CONFLICT" ? StatusCodes.Status409Conflict
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
            loggerFactory.CreateLogger("AttendanceAudit").LogError(exception, "Attendance audit write failed for {EventType}.", eventType);
        }
    }
}
