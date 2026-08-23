namespace PersonnelPlatform.Application.Notification;

public static class NotificationPermissions
{
    public const string RuleView = "notification.rule.view";
    public const string RuleManage = "notification.rule.manage";
    public const string View = "notification.view";
    public const string Action = "notification.action";
    public const string Process = "notification.process";
}

public sealed record CreateNotificationTemplateRequest(Guid CompanyId, string Code, string Name, string TitleTemplate, string BodyTemplate, string DeepLinkTemplate);
public sealed record UpdateNotificationTemplateRequest(int Version, string Name, string TitleTemplate, string BodyTemplate, string DeepLinkTemplate, bool IsActive);
public sealed record NotificationTemplateSummary(Guid Id, Guid CompanyId, string Code, string Name, string TitleTemplate, string BodyTemplate, string DeepLinkTemplate, bool IsActive, int Version);

public sealed record CreateNotificationRuleRequest(Guid CompanyId, string Code, string Name, string SourceModule, string EventType, string Priority, string RecipientKind, Guid? RecipientUserId, Guid? RecipientRoleId, Guid TemplateId, int? EscalateAfterMinutes, string? EscalationRecipientKind, Guid? EscalationUserId, Guid? EscalationRoleId);
public sealed record UpdateNotificationRuleRequest(int Version, string Name, string SourceModule, string EventType, string Priority, string RecipientKind, Guid? RecipientUserId, Guid? RecipientRoleId, Guid TemplateId, int? EscalateAfterMinutes, string? EscalationRecipientKind, Guid? EscalationUserId, Guid? EscalationRoleId, bool IsActive);
public sealed record NotificationRuleSummary(Guid Id, Guid CompanyId, string Code, string Name, string SourceModule, string EventType, string Priority, string RecipientKind, Guid? RecipientUserId, string? RecipientUsername, Guid? RecipientRoleId, string? RecipientRoleCode, Guid TemplateId, string TemplateCode, int? EscalateAfterMinutes, string? EscalationRecipientKind, Guid? EscalationUserId, string? EscalationUsername, Guid? EscalationRoleId, string? EscalationRoleCode, bool IsActive, int Version);

public sealed record NotificationSummary(Guid Id, Guid CompanyId, Guid UserId, string Username, Guid RuleId, string RuleCode, string SourceModule, string SourceEventType, Guid SourceEventId, Guid? SourceEntityId, Guid? ParentNotificationId, string DedupeKey, string Priority, string Title, string Body, string DeepLink, string Status, DateTimeOffset? DueAt, DateTimeOffset? SnoozedUntil, DateTimeOffset? SeenAt, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, DateTimeOffset? EscalatedAt, int EscalationLevel, DateTimeOffset CreatedAt, int Version);
public sealed record NotificationHistorySummary(Guid Id, Guid NotificationId, string EventType, string? FromStatus, string ToStatus, Guid? ActorUserId, string? ActorUsername, DateTimeOffset OccurredAt, string DetailsJson);
public sealed record NotificationDetail(NotificationSummary Notification, IReadOnlyList<NotificationHistorySummary> Timeline);
public sealed record NotificationActionRequest(int Version);
public sealed record SnoozeNotificationRequest(int Version, DateTimeOffset Until);
public sealed record NotificationActionCenterSummary(int CriticalCount, int PendingCount, int OverdueCount, IReadOnlyList<NotificationSummary> Critical, IReadOnlyList<NotificationSummary> Pending, IReadOnlyList<NotificationSummary> Overdue);

public sealed record NotificationSourceEvent(Guid Id, Guid CompanyId, string SourceModule, string EventType, string SourcePriority, Guid? SourceEntityId, Guid? RequesterUserId, Guid? ResponsibleUserId, Guid? CurrentApproverUserId, Guid? CurrentApproverRoleId, DateTimeOffset? DueAt, string Message, string MetadataJson, DateTimeOffset CreatedAt);
public sealed record NotificationRunResult(int SourceEvents, int RuleMatches, int Created, int Duplicates, int Escalated);

public sealed record NotificationResult<T>(bool Succeeded, T? Value, string? ErrorCode, string? ErrorMessage) where T : class
{
    public static NotificationResult<T> Success(T value) => new(true, value, null, null);
    public static NotificationResult<T> Failure(string code, string message) => new(false, null, code, message);
}
