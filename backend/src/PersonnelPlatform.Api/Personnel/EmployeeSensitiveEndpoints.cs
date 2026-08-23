using System.Security.Claims;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Audit;
using PersonnelPlatform.Application.Personnel;

namespace PersonnelPlatform.Api.Personnel;

public static class EmployeeSensitiveEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeSensitiveEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/personnel/employees/{employeeId:guid}/sensitive")
            .WithTags("Personnel Sensitive")
            .RequireAuthorization();

        group.MapGet("/", GetAsync).RequirePermission(PersonnelPermissions.SensitiveView);
        group.MapPut("/", UpsertAsync).RequirePermission(PersonnelPermissions.SensitiveManage);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        Guid employeeId,
        bool? reveal,
        ClaimsPrincipal principal,
        EmployeeSensitiveService service,
        AuditService audit,
        ILoggerFactory loggerFactory,
        HttpContext context,
        CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Error(context, StatusCodes.Status401Unauthorized, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");
        var result = await service.GetAsync(userId, employeeId, reveal == true, ct);
        if (reveal == true)
            await TryAuditAsync(audit, loggerFactory, principal, context, "PERSONNEL_SENSITIVE_REVEAL", result.Succeeded, employeeId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context);
    }

    private static async Task<IResult> UpsertAsync(
        Guid employeeId,
        UpsertEmployeeSensitiveProfileRequest request,
        ClaimsPrincipal principal,
        EmployeeSensitiveService service,
        AuditService audit,
        ILoggerFactory loggerFactory,
        HttpContext context,
        CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Error(context, StatusCodes.Status401Unauthorized, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");
        var result = await service.UpsertAsync(userId, employeeId, request, ct);
        await TryAuditAsync(audit, loggerFactory, principal, context, "PERSONNEL_SENSITIVE_UPDATED", result.Succeeded, employeeId, result.ErrorCode, result.ErrorMessage, ct);
        return ToResult(result, context);
    }

    private static IResult ToResult<T>(PersonnelResult<T> result, HttpContext context) where T : class
    {
        if (result.Succeeded && result.Value is not null) return Results.Ok(result.Value);
        var code = result.ErrorCode ?? "PERSONNEL_SENSITIVE_OPERATION_FAILED";
        var status = code is "SCOPE_DENIED" or "PERMISSION_DENIED" or "SENSITIVE_REVEAL_DENIED"
            ? StatusCodes.Status403Forbidden
            : code.EndsWith("_NOT_FOUND", StringComparison.Ordinal)
                ? StatusCodes.Status404NotFound
                : code == "RECORD_MODIFIED_BY_ANOTHER_USER"
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status422UnprocessableEntity;
        return Error(context, status, code, result.ErrorMessage ?? "İşlem tamamlanamadı.");
    }

    private static async Task TryAuditAsync(
        AuditService audit,
        ILoggerFactory loggerFactory,
        ClaimsPrincipal principal,
        HttpContext context,
        string eventType,
        bool succeeded,
        Guid employeeId,
        string? errorCode,
        string? message,
        CancellationToken ct)
    {
        try
        {
            await audit.WriteAsync(new AuditEvent(
                AuditCategories.Security,
                eventType,
                succeeded,
                succeeded ? AuditSeverities.Info : AuditSeverities.Warning,
                TryActor(principal, out var actor) ? actor : null,
                principal.FindFirstValue("unique_name"),
                context.Connection.RemoteIpAddress?.ToString(),
                context.Request.Headers["User-Agent"].ToString(),
                context.TraceIdentifier,
                "EMPLOYEE",
                employeeId.ToString(),
                errorCode,
                message), ct);
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("SensitivePersonnelAudit").LogError(exception, "Sensitive personnel audit write failed for {EventType}.", eventType);
        }
    }

    private static bool TryActor(ClaimsPrincipal principal, out Guid userId) => Guid.TryParse(principal.FindFirstValue("sub"), out userId);
    private static IResult Error(HttpContext context, int status, string code, string message) =>
        Results.Json(ApiErrorResponse.Create(code, message, context.TraceIdentifier), statusCode: status);
}
