using System.Security.Claims;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Administration;
using PersonnelPlatform.Application.Audit;

namespace PersonnelPlatform.Api.Administration;

public static class AdministrativeAffairsEndpoints
{
    public static IEndpointRouteBuilder MapAdministrativeAffairsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/administration/affairs").WithTags("Administration · Affairs").RequireAuthorization();
        group.MapGet("/tasks", ListTasksAsync).RequirePermission(AdministrativeAffairsPermissions.TaskView);
        group.MapPost("/tasks", CreateTaskAsync).RequirePermission(AdministrativeAffairsPermissions.TaskManage);
        group.MapPost("/tasks/{taskId:guid}/complete", CompleteTaskAsync).RequirePermission(AdministrativeAffairsPermissions.TaskManage);
        group.MapPost("/tasks/{taskId:guid}/pause", PauseTaskAsync).RequirePermission(AdministrativeAffairsPermissions.TaskManage);
        group.MapPost("/tasks/{taskId:guid}/resume", ResumeTaskAsync).RequirePermission(AdministrativeAffairsPermissions.TaskManage);
        group.MapPost("/tasks/{taskId:guid}/close", CloseTaskAsync).RequirePermission(AdministrativeAffairsPermissions.TaskManage);
        group.MapGet("/tasks/{taskId:guid}/completions", ListTaskCompletionsAsync).RequirePermission(AdministrativeAffairsPermissions.TaskView);
        group.MapGet("/contracts", ListContractsAsync).RequirePermission(AdministrativeAffairsPermissions.ContractView);
        group.MapPost("/contracts", CreateContractAsync).RequirePermission(AdministrativeAffairsPermissions.ContractManage);
        group.MapPost("/contracts/{contractId:guid}/close", CloseContractAsync).RequirePermission(AdministrativeAffairsPermissions.ContractManage);
        group.MapGet("/reminders", ListRemindersAsync).RequirePermission(AdministrativeAffairsPermissions.ReminderView);
        group.MapPost("/reminders/process", ProcessRemindersAsync).RequirePermission(AdministrativeAffairsPermissions.ReminderProcess);
        return endpoints;
    }

    private static async Task<IResult> ListTasksAsync(ClaimsPrincipal principal, AdministrativeAffairsService service, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); var q = context.Request.Query; return ToResult(await service.ListTasksAsync(userId, ReadGuid(q,"companyId"), ReadGuid(q,"responsibleUserId"), ReadString(q,"status"), ct), context); }

    private static async Task<IResult> CreateTaskAsync(CreateAdministrativeTaskRequest request, ClaimsPrincipal principal, AdministrativeAffairsService service, AuditService audit, ILoggerFactory logs, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); var result = await service.CreateTaskAsync(userId, request, ct); await AuditAsync(audit, logs, principal, context, "ADMIN_TASK_CREATED", result, result.Value?.Id, "ADMIN_TASK", ct); return ToResult(result, context, StatusCodes.Status201Created); }

    private static async Task<IResult> CompleteTaskAsync(Guid taskId, AdministrativeTaskActionRequest request, ClaimsPrincipal principal, AdministrativeAffairsService service, AuditService audit, ILoggerFactory logs, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); var result = await service.CompleteTaskAsync(userId, taskId, request, ct); await AuditAsync(audit, logs, principal, context, "ADMIN_TASK_COMPLETED", result, taskId, "ADMIN_TASK", ct); return ToResult(result, context); }

    private static async Task<IResult> PauseTaskAsync(Guid taskId, AdministrativeTaskActionRequest request, ClaimsPrincipal principal, AdministrativeAffairsService service, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); return ToResult(await service.PauseTaskAsync(userId, taskId, request, ct), context); }

    private static async Task<IResult> ResumeTaskAsync(Guid taskId, AdministrativeTaskActionRequest request, ClaimsPrincipal principal, AdministrativeAffairsService service, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); return ToResult(await service.ResumeTaskAsync(userId, taskId, request, ct), context); }

    private static async Task<IResult> CloseTaskAsync(Guid taskId, AdministrativeTaskActionRequest request, ClaimsPrincipal principal, AdministrativeAffairsService service, AuditService audit, ILoggerFactory logs, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); var result = await service.CloseTaskAsync(userId, taskId, request, ct); await AuditAsync(audit, logs, principal, context, "ADMIN_TASK_CLOSED", result, taskId, "ADMIN_TASK", ct); return ToResult(result, context); }

    private static async Task<IResult> ListTaskCompletionsAsync(Guid taskId, ClaimsPrincipal principal, AdministrativeAffairsService service, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); return ToResult(await service.ListTaskCompletionsAsync(userId, taskId, ReadInt(context.Request.Query,"take",100), ct), context); }

    private static async Task<IResult> ListContractsAsync(ClaimsPrincipal principal, AdministrativeAffairsService service, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); var q = context.Request.Query; return ToResult(await service.ListContractsAsync(userId, ReadGuid(q,"companyId"), ReadGuid(q,"responsibleUserId"), ReadString(q,"status"), ct), context); }

    private static async Task<IResult> CreateContractAsync(CreateAdministrativeContractRequest request, ClaimsPrincipal principal, AdministrativeAffairsService service, AuditService audit, ILoggerFactory logs, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); var result = await service.CreateContractAsync(userId, request, ct); await AuditAsync(audit, logs, principal, context, "ADMIN_CONTRACT_CREATED", result, result.Value?.Id, "ADMIN_CONTRACT", ct); return ToResult(result, context, StatusCodes.Status201Created); }

    private static async Task<IResult> CloseContractAsync(Guid contractId, AdministrativeContractActionRequest request, ClaimsPrincipal principal, AdministrativeAffairsService service, AuditService audit, ILoggerFactory logs, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); var result = await service.CloseContractAsync(userId, contractId, request, ct); await AuditAsync(audit, logs, principal, context, "ADMIN_CONTRACT_CLOSED", result, contractId, "ADMIN_CONTRACT", ct); return ToResult(result, context); }

    private static async Task<IResult> ListRemindersAsync(ClaimsPrincipal principal, AdministrativeAffairsService service, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); var q = context.Request.Query; return ToResult(await service.ListRemindersAsync(userId, ReadGuid(q,"companyId"), ReadString(q,"eventType"), ReadDateTime(q,"from"), ReadInt(q,"take",100), ct), context); }

    private static async Task<IResult> ProcessRemindersAsync(ClaimsPrincipal principal, AdministrativeAffairsService service, AuditService audit, ILoggerFactory logs, HttpContext context, CancellationToken ct)
    { if (!TryActor(principal, out var userId)) return Unauthorized(context); var result = await service.ProcessRemindersAsync(userId, ct); await AuditAsync(audit, logs, principal, context, "ADMIN_REMINDERS_PROCESSED", result, null, "ADMIN_REMINDER", ct); return ToResult(result, context); }

    private static IResult ToResult<T>(AdministrativeAffairsResult<T> result, HttpContext context, int successStatus = StatusCodes.Status200OK) where T : class
    {
        if (result.Succeeded && result.Value is not null) return Results.Json(result.Value, statusCode: successStatus);
        var code = result.ErrorCode ?? "ADMIN_OPERATION_FAILED";
        var status = code == "SCOPE_DENIED" ? StatusCodes.Status403Forbidden
            : code.EndsWith("_NOT_FOUND", StringComparison.Ordinal) ? StatusCodes.Status404NotFound
            : code is "ADMIN_TASK_CODE_EXISTS" or "ADMIN_CONTRACT_NO_EXISTS" or "RECORD_MODIFIED_BY_ANOTHER_USER" or "ADMIN_HISTORY_IMMUTABLE" ? StatusCodes.Status409Conflict
            : StatusCodes.Status422UnprocessableEntity;
        return Error(context, status, code, result.ErrorMessage ?? "İşlem tamamlanamadı.");
    }

    private static async Task AuditAsync<T>(AuditService audit, ILoggerFactory logs, ClaimsPrincipal principal, HttpContext context, string eventType, AdministrativeAffairsResult<T> result, Guid? entityId, string targetType, CancellationToken ct) where T : class
    {
        try { await audit.WriteAsync(new AuditEvent(AuditCategories.Administration, eventType, result.Succeeded, result.Succeeded ? AuditSeverities.Info : AuditSeverities.Warning, TryActor(principal, out var actor) ? actor : null, principal.FindFirstValue("unique_name"), context.Connection.RemoteIpAddress?.ToString(), context.Request.Headers["User-Agent"].ToString(), context.TraceIdentifier, targetType, entityId?.ToString(), result.ErrorCode, result.ErrorMessage), ct); }
        catch (Exception ex) { logs.CreateLogger("AdministrativeAffairsAudit").LogError(ex, "Administrative affairs audit write failed for {EventType}.", eventType); }
    }

    private static Guid? ReadGuid(IQueryCollection q, string key) => Guid.TryParse(q[key].ToString(), out var v) ? v : null;
    private static int ReadInt(IQueryCollection q, string key, int fallback) => int.TryParse(q[key].ToString(), out var v) ? v : fallback;
    private static string? ReadString(IQueryCollection q, string key) => string.IsNullOrWhiteSpace(q[key]) ? null : q[key].ToString();
    private static DateTimeOffset? ReadDateTime(IQueryCollection q, string key) => DateTimeOffset.TryParse(q[key].ToString(), out var v) ? v : null;
    private static bool TryActor(ClaimsPrincipal principal, out Guid userId) => Guid.TryParse(principal.FindFirstValue("sub"), out userId);
    private static IResult Unauthorized(HttpContext context) => Error(context, StatusCodes.Status401Unauthorized, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");
    private static IResult Error(HttpContext context, int status, string code, string message) => Results.Json(ApiErrorResponse.Create(code, message, context.TraceIdentifier), statusCode: status);
}
