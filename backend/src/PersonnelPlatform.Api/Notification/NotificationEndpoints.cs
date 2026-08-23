using System.Security.Claims;
using PersonnelPlatform.Api.Authorization;
using PersonnelPlatform.Api.Contracts;
using PersonnelPlatform.Application.Audit;
using PersonnelPlatform.Application.Notification;

namespace PersonnelPlatform.Api.Notification;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/notifications").WithTags("Notification Center").RequireAuthorization();

        group.MapGet("/templates", ListTemplatesAsync).RequirePermission(NotificationPermissions.RuleView);
        group.MapPost("/templates", CreateTemplateAsync).RequirePermission(NotificationPermissions.RuleManage);
        group.MapPut("/templates/{templateId:guid}", UpdateTemplateAsync).RequirePermission(NotificationPermissions.RuleManage);
        group.MapGet("/rules", ListRulesAsync).RequirePermission(NotificationPermissions.RuleView);
        group.MapPost("/rules", CreateRuleAsync).RequirePermission(NotificationPermissions.RuleManage);
        group.MapPut("/rules/{ruleId:guid}", UpdateRuleAsync).RequirePermission(NotificationPermissions.RuleManage);

        group.MapGet("/", ListNotificationsAsync).RequirePermission(NotificationPermissions.View);
        group.MapGet("/action-center", ActionCenterAsync).RequirePermission(NotificationPermissions.View);
        group.MapGet("/{notificationId:guid}", GetNotificationAsync).RequirePermission(NotificationPermissions.View);
        group.MapPost("/{notificationId:guid}/seen", MarkSeenAsync).RequirePermission(NotificationPermissions.Action);
        group.MapPost("/{notificationId:guid}/start", StartAsync).RequirePermission(NotificationPermissions.Action);
        group.MapPost("/{notificationId:guid}/complete", CompleteAsync).RequirePermission(NotificationPermissions.Action);
        group.MapPost("/{notificationId:guid}/snooze", SnoozeAsync).RequirePermission(NotificationPermissions.Action);
        group.MapPost("/process", ProcessAsync).RequirePermission(NotificationPermissions.Process);
        return endpoints;
    }

    private static async Task<IResult> ListTemplatesAsync(ClaimsPrincipal p, NotificationService service, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var q = c.Request.Query; return ToResult(await service.ListTemplatesAsync(u, GuidValue(q, "companyId"), BoolValue(q, "active"), ct), c); }

    private static async Task<IResult> CreateTemplateAsync(CreateNotificationTemplateRequest request, ClaimsPrincipal p, NotificationService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var r = await service.CreateTemplateAsync(u, request, ct); await AuditAsync(audit, logs, p, c, "NOTIFICATION_TEMPLATE_CREATED", r.Succeeded, r.Value?.Id, "NOTIFICATION_TEMPLATE", r.ErrorCode, r.ErrorMessage, ct); return ToResult(r, c, StatusCodes.Status201Created); }

    private static async Task<IResult> UpdateTemplateAsync(Guid templateId, UpdateNotificationTemplateRequest request, ClaimsPrincipal p, NotificationService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var r = await service.UpdateTemplateAsync(u, templateId, request, ct); await AuditAsync(audit, logs, p, c, "NOTIFICATION_TEMPLATE_UPDATED", r.Succeeded, templateId, "NOTIFICATION_TEMPLATE", r.ErrorCode, r.ErrorMessage, ct); return ToResult(r, c); }

    private static async Task<IResult> ListRulesAsync(ClaimsPrincipal p, NotificationService service, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var q = c.Request.Query; return ToResult(await service.ListRulesAsync(u, GuidValue(q, "companyId"), BoolValue(q, "active"), Text(q, "sourceModule"), Text(q, "eventType"), ct), c); }

    private static async Task<IResult> CreateRuleAsync(CreateNotificationRuleRequest request, ClaimsPrincipal p, NotificationService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var r = await service.CreateRuleAsync(u, request, ct); await AuditAsync(audit, logs, p, c, "NOTIFICATION_RULE_CREATED", r.Succeeded, r.Value?.Id, "NOTIFICATION_RULE", r.ErrorCode, r.ErrorMessage, ct); return ToResult(r, c, StatusCodes.Status201Created); }

    private static async Task<IResult> UpdateRuleAsync(Guid ruleId, UpdateNotificationRuleRequest request, ClaimsPrincipal p, NotificationService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var r = await service.UpdateRuleAsync(u, ruleId, request, ct); await AuditAsync(audit, logs, p, c, "NOTIFICATION_RULE_UPDATED", r.Succeeded, ruleId, "NOTIFICATION_RULE", r.ErrorCode, r.ErrorMessage, ct); return ToResult(r, c); }

    private static async Task<IResult> ListNotificationsAsync(ClaimsPrincipal p, NotificationService service, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var q = c.Request.Query; return ToResult(await service.ListNotificationsAsync(u, GuidValue(q, "companyId"), Text(q, "status"), Text(q, "priority"), IntValue(q, "take", 200), ct), c); }

    private static async Task<IResult> ActionCenterAsync(ClaimsPrincipal p, NotificationService service, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); return ToResult(await service.GetActionCenterAsync(u, GuidValue(c.Request.Query, "companyId"), ct), c); }

    private static async Task<IResult> GetNotificationAsync(Guid notificationId, ClaimsPrincipal p, NotificationService service, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); return ToResult(await service.GetNotificationAsync(u, notificationId, ct), c); }

    private static async Task<IResult> MarkSeenAsync(Guid notificationId, NotificationActionRequest request, ClaimsPrincipal p, NotificationService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var r = await service.MarkSeenAsync(u, notificationId, request, ct); await AuditAsync(audit, logs, p, c, "NOTIFICATION_SEEN", r.Succeeded, notificationId, "NOTIFICATION", r.ErrorCode, r.ErrorMessage, ct); return ToResult(r, c); }

    private static async Task<IResult> StartAsync(Guid notificationId, NotificationActionRequest request, ClaimsPrincipal p, NotificationService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var r = await service.StartAsync(u, notificationId, request, ct); await AuditAsync(audit, logs, p, c, "NOTIFICATION_STARTED", r.Succeeded, notificationId, "NOTIFICATION", r.ErrorCode, r.ErrorMessage, ct); return ToResult(r, c); }

    private static async Task<IResult> CompleteAsync(Guid notificationId, NotificationActionRequest request, ClaimsPrincipal p, NotificationService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var r = await service.CompleteAsync(u, notificationId, request, ct); await AuditAsync(audit, logs, p, c, "NOTIFICATION_COMPLETED", r.Succeeded, notificationId, "NOTIFICATION", r.ErrorCode, r.ErrorMessage, ct); return ToResult(r, c); }

    private static async Task<IResult> SnoozeAsync(Guid notificationId, SnoozeNotificationRequest request, ClaimsPrincipal p, NotificationService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var r = await service.SnoozeAsync(u, notificationId, request, ct); await AuditAsync(audit, logs, p, c, "NOTIFICATION_SNOOZED", r.Succeeded, notificationId, "NOTIFICATION", r.ErrorCode, r.ErrorMessage, ct); return ToResult(r, c); }

    private static async Task<IResult> ProcessAsync(ClaimsPrincipal p, NotificationService service, AuditService audit, ILoggerFactory logs, HttpContext c, CancellationToken ct)
    { if (!Actor(p, out var u)) return Unauthorized(c); var r = await service.ProcessAsync(u, ct); await AuditAsync(audit, logs, p, c, "NOTIFICATIONS_PROCESSED", r.Succeeded, null, "NOTIFICATION_PROCESS", r.ErrorCode, r.ErrorMessage, ct); return ToResult(r, c); }

    private static IResult ToResult<T>(NotificationResult<T> r, HttpContext c, int success = StatusCodes.Status200OK) where T : class
    {
        if (r.Succeeded && r.Value is not null) return Results.Json(r.Value, statusCode: success);
        var code = r.ErrorCode ?? "NOTIFICATION_OPERATION_FAILED";
        var status = code == "SCOPE_DENIED" || code.EndsWith("_DENIED", StringComparison.Ordinal) ? StatusCodes.Status403Forbidden
            : code.EndsWith("_NOT_FOUND", StringComparison.Ordinal) ? StatusCodes.Status404NotFound
            : code is "RECORD_MODIFIED_BY_ANOTHER_USER" or "NOTIFICATION_TEMPLATE_CODE_EXISTS" or "NOTIFICATION_RULE_CODE_EXISTS" or "NOTIFICATION_DUPLICATE" ? StatusCodes.Status409Conflict
            : StatusCodes.Status422UnprocessableEntity;
        return Error(c, status, code, r.ErrorMessage ?? "Bildirim işlemi tamamlanamadı.");
    }

    private static async Task AuditAsync(AuditService audit, ILoggerFactory logs, ClaimsPrincipal p, HttpContext c, string eventType, bool succeeded, Guid? entityId, string targetType, string? errorCode, string? message, CancellationToken ct)
    {
        try { await audit.WriteAsync(new AuditEvent(AuditCategories.Administration, eventType, succeeded, succeeded ? AuditSeverities.Info : AuditSeverities.Warning, Actor(p, out var actor) ? actor : null, p.FindFirstValue("unique_name"), c.Connection.RemoteIpAddress?.ToString(), c.Request.Headers["User-Agent"].ToString(), c.TraceIdentifier, targetType, entityId?.ToString(), errorCode, message), ct); }
        catch (Exception ex) { logs.CreateLogger("NotificationAudit").LogError(ex, "Notification audit write failed for {EventType}.", eventType); }
    }

    private static Guid? GuidValue(IQueryCollection q, string key) => Guid.TryParse(q[key].ToString(), out var v) ? v : null;
    private static int IntValue(IQueryCollection q, string key, int fallback) => int.TryParse(q[key].ToString(), out var v) ? v : fallback;
    private static bool? BoolValue(IQueryCollection q, string key) => bool.TryParse(q[key].ToString(), out var v) ? v : null;
    private static string? Text(IQueryCollection q, string key) => string.IsNullOrWhiteSpace(q[key]) ? null : q[key].ToString();
    private static bool Actor(ClaimsPrincipal p, out Guid userId) => Guid.TryParse(p.FindFirstValue("sub"), out userId);
    private static IResult Unauthorized(HttpContext c) => Error(c, StatusCodes.Status401Unauthorized, "AUTH_TOKEN_INVALID", "Oturum bilgisi geçersiz.");
    private static IResult Error(HttpContext c, int status, string code, string message) => Results.Json(ApiErrorResponse.Create(code, message, c.TraceIdentifier), statusCode: status);
}
