using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Notification;

public static class NotificationPriorities
{
    public const string Info = "INFO";
    public const string Normal = "NORMAL";
    public const string Important = "IMPORTANT";
    public const string Critical = "CRITICAL";
    public static bool IsKnown(string value) => value is Info or Normal or Important or Critical;
}

public static class NotificationRecipientKinds
{
    public const string User = "USER";
    public const string Role = "ROLE";
    public const string CurrentApprover = "CURRENT_APPROVER";
    public const string Requester = "REQUESTER";
    public const string Responsible = "RESPONSIBLE";
    public const string Manager = "MANAGER";

    public static bool IsRuleRecipient(string value) => value is User or Role or CurrentApprover or Requester or Responsible;
    public static bool IsEscalationRecipient(string value) => value is User or Role or Manager;
}

public static class NotificationStatuses
{
    public const string New = "NEW";
    public const string Seen = "SEEN";
    public const string InProgress = "IN_PROGRESS";
    public const string Completed = "COMPLETED";
    public const string Snoozed = "SNOOZED";
    public const string Escalated = "ESCALATED";
}

public sealed class NotificationTemplate : AuditableEntity
{
    private NotificationTemplate() { }

    public Guid CompanyId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string TitleTemplate { get; private set; } = string.Empty;
    public string BodyTemplate { get; private set; } = string.Empty;
    public string DeepLinkTemplate { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    public static NotificationTemplate Create(Guid companyId, string code, string name, string titleTemplate, string bodyTemplate, string deepLinkTemplate, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Company and actor are required.");
        return new NotificationTemplate
        {
            CompanyId = companyId,
            Code = Required(code, 80).ToUpperInvariant(),
            Name = Required(name, 200),
            TitleTemplate = Required(titleTemplate, 300),
            BodyTemplate = Required(bodyTemplate, 2000),
            DeepLinkTemplate = Required(deepLinkTemplate, 1000),
            IsActive = true,
            CreatedAt = now.ToUniversalTime(),
            CreatedBy = actorUserId
        };
    }

    public void Update(string name, string titleTemplate, string bodyTemplate, string deepLinkTemplate, bool isActive, DateTimeOffset now, Guid actorUserId)
    {
        Name = Required(name, 200);
        TitleTemplate = Required(titleTemplate, 300);
        BodyTemplate = Required(bodyTemplate, 2000);
        DeepLinkTemplate = Required(deepLinkTemplate, 1000);
        IsActive = isActive;
        UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
    }

    private static string Required(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
}

public sealed class NotificationRule : AuditableEntity
{
    private NotificationRule() { }

    public Guid CompanyId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string SourceModule { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string Priority { get; private set; } = NotificationPriorities.Normal;
    public string RecipientKind { get; private set; } = NotificationRecipientKinds.User;
    public Guid? RecipientUserId { get; private set; }
    public Guid? RecipientRoleId { get; private set; }
    public Guid TemplateId { get; private set; }
    public int? EscalateAfterMinutes { get; private set; }
    public string? EscalationRecipientKind { get; private set; }
    public Guid? EscalationUserId { get; private set; }
    public Guid? EscalationRoleId { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static NotificationRule Create(Guid companyId, string code, string name, string sourceModule, string eventType, string priority, string recipientKind, Guid? recipientUserId, Guid? recipientRoleId, Guid templateId, int? escalateAfterMinutes, string? escalationRecipientKind, Guid? escalationUserId, Guid? escalationRoleId, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || templateId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Company, template and actor are required.");
        var normalizedPriority = Required(priority, 20).ToUpperInvariant();
        if (!NotificationPriorities.IsKnown(normalizedPriority)) throw new ArgumentException("Notification priority is invalid.", nameof(priority));
        var recipient = Required(recipientKind, 30).ToUpperInvariant();
        ValidateRecipient(recipient, recipientUserId, recipientRoleId);
        var escalation = NormalizeEscalation(escalateAfterMinutes, escalationRecipientKind, escalationUserId, escalationRoleId);
        return new NotificationRule
        {
            CompanyId = companyId,
            Code = Required(code, 80).ToUpperInvariant(),
            Name = Required(name, 200),
            SourceModule = Required(sourceModule, 50).ToUpperInvariant(),
            EventType = Required(eventType, 80).ToUpperInvariant(),
            Priority = normalizedPriority,
            RecipientKind = recipient,
            RecipientUserId = recipientUserId,
            RecipientRoleId = recipientRoleId,
            TemplateId = templateId,
            EscalateAfterMinutes = escalation.AfterMinutes,
            EscalationRecipientKind = escalation.Kind,
            EscalationUserId = escalation.UserId,
            EscalationRoleId = escalation.RoleId,
            IsActive = true,
            CreatedAt = now.ToUniversalTime(),
            CreatedBy = actorUserId
        };
    }

    public void Update(string name, string sourceModule, string eventType, string priority, string recipientKind, Guid? recipientUserId, Guid? recipientRoleId, Guid templateId, int? escalateAfterMinutes, string? escalationRecipientKind, Guid? escalationUserId, Guid? escalationRoleId, bool isActive, DateTimeOffset now, Guid actorUserId)
    {
        if (templateId == Guid.Empty) throw new ArgumentException("Template is required.", nameof(templateId));
        var normalizedPriority = Required(priority, 20).ToUpperInvariant(); if (!NotificationPriorities.IsKnown(normalizedPriority)) throw new ArgumentException("Notification priority is invalid.");
        var recipient = Required(recipientKind, 30).ToUpperInvariant(); ValidateRecipient(recipient, recipientUserId, recipientRoleId);
        var escalation = NormalizeEscalation(escalateAfterMinutes, escalationRecipientKind, escalationUserId, escalationRoleId);
        Name = Required(name, 200); SourceModule = Required(sourceModule, 50).ToUpperInvariant(); EventType = Required(eventType, 80).ToUpperInvariant(); Priority = normalizedPriority; RecipientKind = recipient; RecipientUserId = recipientUserId; RecipientRoleId = recipientRoleId; TemplateId = templateId; EscalateAfterMinutes = escalation.AfterMinutes; EscalationRecipientKind = escalation.Kind; EscalationUserId = escalation.UserId; EscalationRoleId = escalation.RoleId; IsActive = isActive;
        UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
    }

    private static void ValidateRecipient(string kind, Guid? userId, Guid? roleId)
    {
        if (!NotificationRecipientKinds.IsRuleRecipient(kind)) throw new ArgumentException("Recipient kind is invalid.");
        if (kind == NotificationRecipientKinds.User && (userId is null || userId == Guid.Empty || roleId is not null)) throw new ArgumentException("USER recipient requires only a user.");
        if (kind == NotificationRecipientKinds.Role && (roleId is null || roleId == Guid.Empty || userId is not null)) throw new ArgumentException("ROLE recipient requires only a role.");
        if (kind is not NotificationRecipientKinds.User and not NotificationRecipientKinds.Role && (userId is not null || roleId is not null)) throw new ArgumentException("Dynamic recipient must not contain fixed targets.");
    }

    private static (int? AfterMinutes, string? Kind, Guid? UserId, Guid? RoleId) NormalizeEscalation(int? afterMinutes, string? kind, Guid? userId, Guid? roleId)
    {
        if (afterMinutes is null)
        {
            if (!string.IsNullOrWhiteSpace(kind) || userId is not null || roleId is not null) throw new ArgumentException("Escalation target requires escalation interval.");
            return (null, null, null, null);
        }
        if (afterMinutes is < 1 or > 525600) throw new ArgumentOutOfRangeException(nameof(afterMinutes));
        var normalized = Required(kind ?? string.Empty, 30).ToUpperInvariant();
        if (!NotificationRecipientKinds.IsEscalationRecipient(normalized)) throw new ArgumentException("Escalation recipient kind is invalid.");
        if (normalized == NotificationRecipientKinds.User && (userId is null || userId == Guid.Empty || roleId is not null)) throw new ArgumentException("USER escalation requires only a user.");
        if (normalized == NotificationRecipientKinds.Role && (roleId is null || roleId == Guid.Empty || userId is not null)) throw new ArgumentException("ROLE escalation requires only a role.");
        if (normalized == NotificationRecipientKinds.Manager && (userId is not null || roleId is not null)) throw new ArgumentException("MANAGER escalation must not contain fixed targets.");
        return (afterMinutes, normalized, userId, roleId);
    }

    private static string Required(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
}

public sealed class UserNotification : AuditableEntity
{
    private UserNotification() { }

    public Guid CompanyId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid RuleId { get; private set; }
    public string SourceModule { get; private set; } = string.Empty;
    public string SourceEventType { get; private set; } = string.Empty;
    public Guid SourceEventId { get; private set; }
    public Guid? SourceEntityId { get; private set; }
    public Guid? ParentNotificationId { get; private set; }
    public string DedupeKey { get; private set; } = string.Empty;
    public string Priority { get; private set; } = NotificationPriorities.Normal;
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string DeepLink { get; private set; } = string.Empty;
    public string Status { get; private set; } = NotificationStatuses.New;
    public DateTimeOffset? DueAt { get; private set; }
    public DateTimeOffset? SnoozedUntil { get; private set; }
    public DateTimeOffset? SeenAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? EscalatedAt { get; private set; }
    public int EscalationLevel { get; private set; }

    public static UserNotification Create(Guid companyId, Guid userId, Guid ruleId, string sourceModule, string sourceEventType, Guid sourceEventId, Guid? sourceEntityId, Guid? parentNotificationId, string dedupeKey, string priority, string title, string body, string deepLink, DateTimeOffset? dueAt, int escalationLevel, DateTimeOffset now)
    {
        if (companyId == Guid.Empty || userId == Guid.Empty || ruleId == Guid.Empty || sourceEventId == Guid.Empty) throw new ArgumentException("Required identifiers are missing.");
        var p = Required(priority, 20).ToUpperInvariant(); if (!NotificationPriorities.IsKnown(p)) throw new ArgumentException("Priority is invalid.");
        if (escalationLevel < 0 || escalationLevel > 100) throw new ArgumentOutOfRangeException(nameof(escalationLevel));
        return new UserNotification
        {
            CompanyId = companyId, UserId = userId, RuleId = ruleId, SourceModule = Required(sourceModule, 50).ToUpperInvariant(), SourceEventType = Required(sourceEventType, 80).ToUpperInvariant(), SourceEventId = sourceEventId, SourceEntityId = sourceEntityId, ParentNotificationId = parentNotificationId, DedupeKey = Required(dedupeKey, 400), Priority = p, Title = Required(title, 300), Body = Required(body, 2000), DeepLink = Required(deepLink, 1000), Status = NotificationStatuses.New, DueAt = dueAt?.ToUniversalTime(), EscalationLevel = escalationLevel, CreatedAt = now.ToUniversalTime()
        };
    }

    public void MarkSeen(DateTimeOffset now, Guid actorUserId)
    {
        if (Status == NotificationStatuses.Completed || Status == NotificationStatuses.Escalated) throw new InvalidOperationException("Terminal notification cannot be marked seen.");
        if (Status == NotificationStatuses.New) Status = NotificationStatuses.Seen;
        SeenAt ??= now.ToUniversalTime(); Touch(now, actorUserId);
    }

    public void Start(DateTimeOffset now, Guid actorUserId)
    {
        if (Status is NotificationStatuses.Completed or NotificationStatuses.Escalated) throw new InvalidOperationException("Terminal notification cannot be started.");
        Status = NotificationStatuses.InProgress; StartedAt ??= now.ToUniversalTime(); SnoozedUntil = null; SeenAt ??= now.ToUniversalTime(); Touch(now, actorUserId);
    }

    public void Complete(DateTimeOffset now, Guid actorUserId)
    {
        if (Status is NotificationStatuses.Completed or NotificationStatuses.Escalated) throw new InvalidOperationException("Notification is already terminal.");
        Status = NotificationStatuses.Completed; CompletedAt = now.ToUniversalTime(); SnoozedUntil = null; SeenAt ??= now.ToUniversalTime(); Touch(now, actorUserId);
    }

    public void Snooze(DateTimeOffset until, DateTimeOffset now, Guid actorUserId)
    {
        if (Status is NotificationStatuses.Completed or NotificationStatuses.Escalated) throw new InvalidOperationException("Terminal notification cannot be snoozed.");
        var normalized = until.ToUniversalTime(); if (normalized <= now.ToUniversalTime()) throw new ArgumentException("Snooze time must be in the future.", nameof(until));
        Status = NotificationStatuses.Snoozed; SnoozedUntil = normalized; SeenAt ??= now.ToUniversalTime(); Touch(now, actorUserId);
    }

    public void Escalate(DateTimeOffset now)
    {
        if (Status is NotificationStatuses.Completed or NotificationStatuses.Escalated) throw new InvalidOperationException("Notification cannot be escalated.");
        Status = NotificationStatuses.Escalated; EscalatedAt = now.ToUniversalTime(); SnoozedUntil = null; UpdatedAt = now.ToUniversalTime(); Version++;
    }

    private void Touch(DateTimeOffset now, Guid actorUserId) { UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++; }
    private static string Required(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
}

public sealed class NotificationHistory : Entity
{
    private NotificationHistory() { }
    public Guid CompanyId { get; private set; }
    public Guid NotificationId { get; private set; }
    public Guid UserId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string? FromStatus { get; private set; }
    public string ToStatus { get; private set; } = string.Empty;
    public Guid? ActorUserId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string DetailsJson { get; private set; } = "{}";

    public static NotificationHistory Create(Guid companyId, Guid notificationId, Guid userId, string eventType, string? fromStatus, string toStatus, Guid? actorUserId, DateTimeOffset occurredAt, string detailsJson)
    {
        if (companyId == Guid.Empty || notificationId == Guid.Empty || userId == Guid.Empty) throw new ArgumentException("Required identifiers are missing.");
        return new NotificationHistory { CompanyId = companyId, NotificationId = notificationId, UserId = userId, EventType = Required(eventType, 80).ToUpperInvariant(), FromStatus = string.IsNullOrWhiteSpace(fromStatus) ? null : fromStatus.Trim().ToUpperInvariant(), ToStatus = Required(toStatus, 30).ToUpperInvariant(), ActorUserId = actorUserId, OccurredAt = occurredAt.ToUniversalTime(), DetailsJson = Required(detailsJson, 10000) };
    }

    private static string Required(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
}
