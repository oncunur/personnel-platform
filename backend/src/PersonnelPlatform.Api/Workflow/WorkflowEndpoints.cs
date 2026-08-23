using System.Security.Claims;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Audit;
using PersonnelPlatform.Application.Workflow;

namespace PersonnelPlatform.Api.Workflow;

public static class WorkflowEndpoints
{
    public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/workflow").WithTags("Workflow").RequireAuthorization();
        group.MapGet("/request-types", ListTypesAsync).RequirePermission(WorkflowPermissions.RequestTypeView);
        group.MapPost("/request-types", CreateTypeAsync).RequirePermission(WorkflowPermissions.RequestTypeManage);
        group.MapPut("/request-types/{requestTypeId:guid}", UpdateTypeAsync).RequirePermission(WorkflowPermissions.RequestTypeManage);
        group.MapGet("/request-types/{requestTypeId:guid}/steps", ListStepsAsync).RequirePermission(WorkflowPermissions.RequestTypeView);
        group.MapPut("/request-types/{requestTypeId:guid}/steps", ReplaceStepsAsync).RequirePermission(WorkflowPermissions.RequestTypeManage);

        group.MapGet("/requests", ListRequestsAsync).RequirePermission(WorkflowPermissions.RequestView);
        group.MapPost("/requests", CreateRequestAsync).RequirePermission(WorkflowPermissions.RequestCreate);
        group.MapGet("/requests/{requestId:guid}", GetRequestAsync).RequirePermission(WorkflowPermissions.RequestView);
        group.MapPost("/requests/{requestId:guid}/submit", SubmitAsync).RequirePermission(WorkflowPermissions.RequestCreate);
        group.MapPost("/requests/{requestId:guid}/cancel", CancelAsync).RequirePermission(WorkflowPermissions.RequestCreate);
        group.MapPost("/requests/{requestId:guid}/approve", ApproveAsync).RequirePermission(WorkflowPermissions.RequestApprove);
        group.MapPost("/requests/{requestId:guid}/reject", RejectAsync).RequirePermission(WorkflowPermissions.RequestApprove);

        group.MapGet("/sla/events", ListSlaAsync).RequirePermission(WorkflowPermissions.SlaView);
        group.MapPost("/sla/process", ProcessSlaAsync).RequirePermission(WorkflowPermissions.SlaProcess);
        return endpoints;
    }

    private static async Task<IResult> ListTypesAsync(ClaimsPrincipal p, WorkflowService service, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var q = c.Request.Query; return ToResult(await service.ListRequestTypesAsync(u, GuidValue(q,"companyId"), BoolValue(q,"active"), ct), c); }

    private static async Task<IResult> CreateTypeAsync(CreateWorkflowRequestTypeRequest request, ClaimsPrincipal p, WorkflowService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var r = await service.CreateRequestTypeAsync(u, request, ct); await AuditAsync(audit, logs, p, c, "WORKFLOW_REQUEST_TYPE_CREATED", r.Succeeded, r.Value?.Id, r.ErrorCode, r.ErrorMessage, ct); return ToResult(r, c, StatusCodes.Status201Created); }

    private static async Task<IResult> UpdateTypeAsync(Guid requestTypeId, UpdateWorkflowRequestTypeRequest request, ClaimsPrincipal p, WorkflowService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var r = await service.UpdateRequestTypeAsync(u, requestTypeId, request, ct); await AuditAsync(audit, logs, p, c, "WORKFLOW_REQUEST_TYPE_UPDATED", r.Succeeded, requestTypeId, r.ErrorCode, r.ErrorMessage, ct); return ToResult(r, c); }

    private static async Task<IResult> ListStepsAsync(Guid requestTypeId, ClaimsPrincipal p, WorkflowService service, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); return ToResult(await service.ListStepsAsync(u, requestTypeId, ct), c); }

    private static async Task<IResult> ReplaceStepsAsync(Guid requestTypeId, ReplaceWorkflowApprovalStepsRequest request, ClaimsPrincipal p, WorkflowService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var r = await service.ReplaceStepsAsync(u, requestTypeId, request, ct); await AuditAsync(audit, logs, p, c, "WORKFLOW_STEPS_REPLACED", r.Succeeded, requestTypeId, r.ErrorCode, r.ErrorMessage, ct); return ToResult(r, c); }

    private static async Task<IResult> ListRequestsAsync(ClaimsPrincipal p, WorkflowService service, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var q = c.Request.Query; return ToResult(await service.ListRequestsAsync(u, GuidValue(q,"companyId"), GuidValue(q,"employeeId"), GuidValue(q,"requesterUserId"), Text(q,"status"), IntValue(q,"take",100), ct), c); }

    private static async Task<IResult> CreateRequestAsync(CreateWorkflowRequestRequest request, ClaimsPrincipal p, WorkflowService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var r = await service.CreateRequestAsync(u, request, ct); await AuditAsync(audit, logs, p, c, "WORKFLOW_REQUEST_CREATED", r.Succeeded, r.Value?.Id, r.ErrorCode, r.ErrorMessage, ct); return ToResult(r, c, StatusCodes.Status201Created); }

    private static async Task<IResult> GetRequestAsync(Guid requestId, ClaimsPrincipal p, WorkflowService service, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); return ToResult(await service.GetRequestAsync(u, requestId, ct), c); }

    private static async Task<IResult> SubmitAsync(Guid requestId, WorkflowRequestActionRequest request, ClaimsPrincipal p, WorkflowService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var r = await service.SubmitAsync(u, requestId, request, ct); await AuditAsync(audit, logs, p, c, "WORKFLOW_REQUEST_SUBMITTED", r.Succeeded, requestId, r.ErrorCode, r.ErrorMessage, ct); return ToResult(r, c); }

    private static async Task<IResult> CancelAsync(Guid requestId, WorkflowRequestActionRequest request, ClaimsPrincipal p, WorkflowService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var r = await service.CancelAsync(u, requestId, request, ct); await AuditAsync(audit, logs, p, c, "WORKFLOW_REQUEST_CANCELLED", r.Succeeded, requestId, r.ErrorCode, r.ErrorMessage, ct); return ToResult(r, c); }

    private static async Task<IResult> ApproveAsync(Guid requestId, WorkflowRequestActionRequest request, ClaimsPrincipal p, WorkflowService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var r = await service.ApproveAsync(u, requestId, request, ct); await AuditAsync(audit, logs, p, c, "WORKFLOW_REQUEST_APPROVED", r.Succeeded, requestId, r.ErrorCode, r.ErrorMessage, ct); return ToResult(r, c); }

    private static async Task<IResult> RejectAsync(Guid requestId, WorkflowRequestActionRequest request, ClaimsPrincipal p, WorkflowService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var r = await service.RejectAsync(u, requestId, request, ct); await AuditAsync(audit, logs, p, c, "WORKFLOW_REQUEST_REJECTED", r.Succeeded, requestId, r.ErrorCode, r.ErrorMessage, ct); return ToResult(r, c); }

    private static async Task<IResult> ListSlaAsync(ClaimsPrincipal p, WorkflowService service, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var q = c.Request.Query; return ToResult(await service.ListSlaEventsAsync(u, GuidValue(q,"companyId"), GuidValue(q,"requestId"), Text(q,"eventType"), IntValue(q,"take",100), ct), c); }

    private static async Task<IResult> ProcessSlaAsync(ClaimsPrincipal p, WorkflowService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var r = await service.ProcessSlaAsync(u, ct); await AuditAsync(audit, logs, p, c, "WORKFLOW_SLA_PROCESSED", r.Succeeded, null, r.ErrorCode, r.ErrorMessage, ct); return ToResult(r, c); }

    private static IResult ToResult<T>(WorkflowResult<T> r, HttpContext c, int success = StatusCodes.Status200OK) where T : class
    {
        if (r.Succeeded && r.Value is not null) return Results.Json(r.Value, statusCode: success);
        var code = r.ErrorCode ?? "WORKFLOW_OPERATION_FAILED";
        var status = code == "SCOPE_DENIED" || code.EndsWith("_DENIED", StringComparison.Ordinal) ? StatusCodes.Status403Forbidden
            : code.EndsWith("_NOT_FOUND", StringComparison.Ordinal) ? StatusCodes.Status404NotFound
            : code is "RECORD_MODIFIED_BY_ANOTHER_USER" or "WORKFLOW_REQUEST_TYPE_CODE_EXISTS" or "WORKFLOW_REQUEST_NO_EXISTS" or "WORKFLOW_STEP_ORDER_EXISTS" or "WORKFLOW_APPROVAL_ALREADY_DECIDED" or "WORKFLOW_REQUEST_STATE_INVALID" or "WORKFLOW_APPROVAL_STATE_INVALID" ? StatusCodes.Status409Conflict
            : StatusCodes.Status422UnprocessableEntity;
        return Error(c, status, code, r.ErrorMessage ?? "Workflow işlemi tamamlanamadı.");
    }

    private static Guid? GuidValue(IQueryCollection q, string k) => Guid.TryParse(q[k].ToString(), out var v) ? v : null;
    private static int IntValue(IQueryCollection q, string k, int fallback) => int.TryParse(q[k].ToString(), out var v) ? v : fallback;
    private static bool? BoolValue(IQueryCollection q, string k) => bool.TryParse(q[k].ToString(), out var v) ? v : null;
    private static string? Text(IQueryCollection q, string k) => string.IsNullOrWhiteSpace(q[k]) ? null : q[k].ToString();
    private static bool Actor(ClaimsPrincipal p, out Guid userId) => Guid.TryParse(p.FindFirstValue("sub"), out userId);
    private static IResult Unauthorized(HttpContext c) => Error(c, StatusCodes.Status401Unauthorized, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");
    private static IResult Error(HttpContext c, int status, string code, string message) => Results.Json(ApiErrorResponse.Create(code, message, c.TraceIdentifier), statusCode: status);

    private static async Task AuditAsync(AuditService audit, ILoggerFactory logs, ClaimsPrincipal p, HttpContext c, string eventType, bool succeeded, Guid? entityId, string? errorCode, string? message, CancellationToken ct)
    {
        try { await audit.WriteAsync(new AuditEvent(AuditCategories.Administration, eventType, succeeded, succeeded ? AuditSeverities.Info : AuditSeverities.Warning, Actor(p, out var actor) ? actor : null, p.FindFirstValue("unique_name"), c.Connection.RemoteIpAddress?.ToString(), c.Request.Headers["User-Agent"].ToString(), c.TraceIdentifier, "WORKFLOW_REQUEST", entityId?.ToString(), errorCode, message), ct); }
        catch (Exception ex) { logs.CreateLogger("WorkflowAudit").LogError(ex, "Workflow audit write failed for {EventType}.", eventType); }
    }
}
