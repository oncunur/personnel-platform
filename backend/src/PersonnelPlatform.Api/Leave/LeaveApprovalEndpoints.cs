using System.Security.Claims;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Audit;
using PersonnelPlatform.Application.Leave;

namespace PersonnelPlatform.Api.Leave;

public static class LeaveApprovalEndpoints
{
    public static IEndpointRouteBuilder MapLeaveApprovalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/leave").WithTags("Leave Approval").RequireAuthorization();
        group.MapGet("/approvals/inbox", ListInboxAsync).RequirePermission(LeavePermissions.View);
        group.MapGet("/requests/{leaveId:guid}/workflow", GetWorkflowAsync).RequirePermission(LeavePermissions.View);
        group.MapPost("/requests/{leaveId:guid}/approvals/{approvalId:guid}/decision", DecideAsync).RequirePermission(LeavePermissions.View);
        group.MapGet("/approver-links", ListLinksAsync).RequirePermission(LeavePermissions.ApproverManage);
        group.MapPut("/approver-links/{userId:guid}", SetLinkAsync).RequirePermission(LeavePermissions.ApproverManage);
        return endpoints;
    }

    private static async Task<IResult> ListInboxAsync(ClaimsPrincipal principal, LeaveApprovalService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.ListInboxAsync(userId, ct), context);
    }

    private static async Task<IResult> GetWorkflowAsync(Guid leaveId, ClaimsPrincipal principal, LeaveApprovalService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.GetWorkflowAsync(userId, leaveId, ct), context);
    }

    private static async Task<IResult> DecideAsync(
        Guid leaveId,
        Guid approvalId,
        LeaveApprovalDecisionRequest request,
        ClaimsPrincipal principal,
        LeaveApprovalService service,
        AuditService auditService,
        ILoggerFactory loggerFactory,
        HttpContext context,
        CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        var result = await service.DecideAsync(userId, leaveId, approvalId, request, ct);
        await AuditAsync(auditService, loggerFactory, principal, context,
            request.Approve ? "LEAVE_APPROVAL_APPROVED" : "LEAVE_APPROVAL_REJECTED",
            result.Succeeded,
            "LEAVE_APPROVAL",
            approvalId,
            result.ErrorCode,
            result.ErrorMessage,
            ct);
        return ToResult(result, context);
    }

    private static async Task<IResult> ListLinksAsync(ClaimsPrincipal principal, LeaveApprovalService service, HttpContext context, CancellationToken ct)
    {
        if (!TryActor(principal, out var userId)) return Unauthorized(context);
        return ToResult(await service.ListApproverLinksAsync(userId, ct), context);
    }

    private static async Task<IResult> SetLinkAsync(
        Guid userId,
        SetEmployeeUserLinkRequest request,
        ClaimsPrincipal principal,
        LeaveApprovalService service,
        AuditService auditService,
        ILoggerFactory loggerFactory,
        HttpContext context,
        CancellationToken ct)
    {
        if (!TryActor(principal, out var actorUserId)) return Unauthorized(context);
        var result = await service.SetApproverLinkAsync(actorUserId, userId, request.EmployeeId, ct);
        await AuditAsync(auditService, loggerFactory, principal, context,
            "LEAVE_APPROVER_LINK_CHANGED",
            result.Succeeded,
            "USER",
            userId,
            result.ErrorCode,
            result.ErrorMessage,
            ct);
        return ToResult(result, context);
    }

    private static bool TryActor(ClaimsPrincipal principal, out Guid userId) => Guid.TryParse(principal.FindFirstValue("sub"), out userId);
    private static IResult Unauthorized(HttpContext context) => Error(context, StatusCodes.Status401Unauthorized, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");

    private static IResult ToResult<T>(LeaveResult<T> result, HttpContext context) where T : class
    {
        if (result.Succeeded && result.Value is not null) return Results.Ok(result.Value);
        var code = result.ErrorCode ?? "LEAVE_APPROVAL_OPERATION_FAILED";
        var status = code == "SCOPE_DENIED" || code.EndsWith("_DENIED", StringComparison.Ordinal) || code is "LEAVE_MANAGER_IDENTITY_MISMATCH" or "LEAVE_APPROVAL_ASSIGNED_TO_ANOTHER_USER"
            ? StatusCodes.Status403Forbidden
            : code.EndsWith("_NOT_FOUND", StringComparison.Ordinal) ? StatusCodes.Status404NotFound
            : code is "EMPLOYEE_ALREADY_LINKED" or "RECORD_MODIFIED_BY_ANOTHER_USER" or "LEAVE_APPROVAL_NOT_PENDING" ? StatusCodes.Status409Conflict
            : StatusCodes.Status422UnprocessableEntity;
        return Error(context, status, code, result.ErrorMessage ?? "İşlem tamamlanamadı.");
    }

    private static async Task AuditAsync(
        AuditService service,
        ILoggerFactory loggerFactory,
        ClaimsPrincipal principal,
        HttpContext context,
        string eventType,
        bool succeeded,
        string targetType,
        Guid targetId,
        string? errorCode,
        string? message,
        CancellationToken ct)
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
                targetType,
                targetId.ToString(),
                errorCode,
                message), ct);
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("LeaveApprovalAudit").LogError(exception, "Leave approval audit write failed for {EventType}.", eventType);
        }
    }

    private static IResult Error(HttpContext context, int statusCode, string code, string message) =>
        Results.Json(ApiErrorResponse.Create(code, message, context.TraceIdentifier), statusCode: statusCode);
}
