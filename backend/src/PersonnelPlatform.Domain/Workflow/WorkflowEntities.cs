using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Workflow;

public static class WorkflowPriorities
{
    public const string Info = "INFO";
    public const string Normal = "NORMAL";
    public const string Important = "IMPORTANT";
    public const string Critical = "CRITICAL";
    public static bool IsKnown(string value) => value is Info or Normal or Important or Critical;
}

public static class ApprovalTargetKinds
{
    public const string User = "USER";
    public const string Role = "ROLE";
    public static bool IsKnown(string value) => value is User or Role;
}

public static class WorkflowRequestStatuses
{
    public const string Draft = "DRAFT";
    public const string InApproval = "IN_APPROVAL";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Cancelled = "CANCELLED";
}

public static class WorkflowApprovalStatuses
{
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Waiting = "WAITING";
}

public sealed class WorkflowRequestType : AuditableEntity
{
    private WorkflowRequestType() { }

    public Guid CompanyId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int SlaMinutes { get; private set; }
    public string RequiredFieldsJson { get; private set; } = "[]";
    public bool IsActive { get; private set; } = true;

    public static WorkflowRequestType Create(Guid companyId, string code, string name, string? description, int slaMinutes, string requiredFieldsJson, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Company and actor are required.");
        if (slaMinutes is < 1 or > 525600) throw new ArgumentOutOfRangeException(nameof(slaMinutes));
        return new WorkflowRequestType
        {
            CompanyId = companyId,
            Code = Required(code, 80).ToUpperInvariant(),
            Name = Required(name, 200),
            Description = Optional(description, 2000),
            SlaMinutes = slaMinutes,
            RequiredFieldsJson = Required(requiredFieldsJson, 10000),
            IsActive = true,
            CreatedAt = now.ToUniversalTime(),
            CreatedBy = actorUserId
        };
    }

    public void Update(string name, string? description, int slaMinutes, string requiredFieldsJson, DateTimeOffset now, Guid actorUserId)
    {
        if (slaMinutes is < 1 or > 525600) throw new ArgumentOutOfRangeException(nameof(slaMinutes));
        Name = Required(name, 200);
        Description = Optional(description, 2000);
        SlaMinutes = slaMinutes;
        RequiredFieldsJson = Required(requiredFieldsJson, 10000);
        UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
    }

    public void SetActive(bool active, DateTimeOffset now, Guid actorUserId)
    {
        if (IsActive == active) return;
        IsActive = active; UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
    }

    private static string Required(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
    private static string? Optional(string? value, int max) { if (string.IsNullOrWhiteSpace(value)) return null; var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
}

public sealed class WorkflowApprovalStepDefinition : AuditableEntity
{
    private WorkflowApprovalStepDefinition() { }

    public Guid CompanyId { get; private set; }
    public Guid RequestTypeId { get; private set; }
    public int StepOrder { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string TargetKind { get; private set; } = string.Empty;
    public Guid? ApproverUserId { get; private set; }
    public Guid? ApproverRoleId { get; private set; }

    public static WorkflowApprovalStepDefinition Create(Guid companyId, Guid requestTypeId, int stepOrder, string name, string targetKind, Guid? approverUserId, Guid? approverRoleId, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || requestTypeId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Required identifiers are missing.");
        if (stepOrder is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(stepOrder));
        var kind = Required(targetKind, 20).ToUpperInvariant();
        if (!ApprovalTargetKinds.IsKnown(kind)) throw new ArgumentException("Approval target kind is invalid.", nameof(targetKind));
        if (kind == ApprovalTargetKinds.User && (approverUserId is null || approverUserId == Guid.Empty || approverRoleId is not null)) throw new ArgumentException("USER approval step requires only approver user.");
        if (kind == ApprovalTargetKinds.Role && (approverRoleId is null || approverRoleId == Guid.Empty || approverUserId is not null)) throw new ArgumentException("ROLE approval step requires only approver role.");
        return new WorkflowApprovalStepDefinition
        {
            CompanyId = companyId,
            RequestTypeId = requestTypeId,
            StepOrder = stepOrder,
            Name = Required(name, 150),
            TargetKind = kind,
            ApproverUserId = approverUserId,
            ApproverRoleId = approverRoleId,
            CreatedAt = now.ToUniversalTime(),
            CreatedBy = actorUserId
        };
    }

    private static string Required(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
}

public sealed class WorkflowRequest : AuditableEntity
{
    private WorkflowRequest() { }

    public Guid CompanyId { get; private set; }
    public string RequestNo { get; private set; } = string.Empty;
    public Guid RequestTypeId { get; private set; }
    public Guid RequesterUserId { get; private set; }
    public Guid? EmployeeId { get; private set; }
    public string Priority { get; private set; } = WorkflowPriorities.Normal;
    public string RequestDataJson { get; private set; } = "{}";
    public string Status { get; private set; } = WorkflowRequestStatuses.Draft;
    public int CurrentStepOrder { get; private set; }
    public int SlaMinutesSnapshot { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public DateTimeOffset? DueAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    public static WorkflowRequest Create(Guid companyId, string requestNo, Guid requestTypeId, Guid requesterUserId, Guid? employeeId, string priority, string requestDataJson, DateTimeOffset now)
    {
        if (companyId == Guid.Empty || requestTypeId == Guid.Empty || requesterUserId == Guid.Empty) throw new ArgumentException("Required identifiers are missing.");
        var normalizedPriority = Required(priority, 20).ToUpperInvariant();
        if (!WorkflowPriorities.IsKnown(normalizedPriority)) throw new ArgumentException("Priority is invalid.", nameof(priority));
        return new WorkflowRequest
        {
            CompanyId = companyId,
            RequestNo = Required(requestNo, 40).ToUpperInvariant(),
            RequestTypeId = requestTypeId,
            RequesterUserId = requesterUserId,
            EmployeeId = employeeId,
            Priority = normalizedPriority,
            RequestDataJson = Required(requestDataJson, 50000),
            Status = WorkflowRequestStatuses.Draft,
            CurrentStepOrder = 0,
            CreatedAt = now.ToUniversalTime(),
            CreatedBy = requesterUserId
        };
    }

    public void Submit(int slaMinutes, int stepCount, DateTimeOffset now, Guid actorUserId)
    {
        if (Status != WorkflowRequestStatuses.Draft) throw new InvalidOperationException("Only draft requests can be submitted.");
        if (slaMinutes is < 1 or > 525600) throw new ArgumentOutOfRangeException(nameof(slaMinutes));
        if (stepCount < 0) throw new ArgumentOutOfRangeException(nameof(stepCount));
        SlaMinutesSnapshot = slaMinutes;
        SubmittedAt = now.ToUniversalTime();
        DueAt = now.ToUniversalTime().AddMinutes(slaMinutes);
        if (stepCount == 0)
        {
            Status = WorkflowRequestStatuses.Approved;
            CurrentStepOrder = 0;
            ResolvedAt = now.ToUniversalTime();
        }
        else
        {
            Status = WorkflowRequestStatuses.InApproval;
            CurrentStepOrder = 1;
        }
        UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
    }

    public void AdvanceApproval(int approvedStepOrder, bool hasNextStep, DateTimeOffset now, Guid actorUserId)
    {
        if (Status != WorkflowRequestStatuses.InApproval || CurrentStepOrder != approvedStepOrder) throw new InvalidOperationException("Request is not at the expected approval step.");
        if (hasNextStep) CurrentStepOrder++;
        else { Status = WorkflowRequestStatuses.Approved; ResolvedAt = now.ToUniversalTime(); }
        UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
    }

    public void Reject(int rejectedStepOrder, DateTimeOffset now, Guid actorUserId)
    {
        if (Status != WorkflowRequestStatuses.InApproval || CurrentStepOrder != rejectedStepOrder) throw new InvalidOperationException("Request is not at the expected approval step.");
        Status = WorkflowRequestStatuses.Rejected; ResolvedAt = now.ToUniversalTime(); UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
    }

    public void Cancel(DateTimeOffset now, Guid actorUserId)
    {
        if (Status is WorkflowRequestStatuses.Approved or WorkflowRequestStatuses.Rejected or WorkflowRequestStatuses.Cancelled) throw new InvalidOperationException("Terminal request cannot be cancelled.");
        Status = WorkflowRequestStatuses.Cancelled; ResolvedAt = now.ToUniversalTime(); UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
    }

    private static string Required(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
}

public sealed class WorkflowRequestApproval : Entity
{
    private WorkflowRequestApproval() { }

    public Guid CompanyId { get; private set; }
    public Guid RequestId { get; private set; }
    public int StepOrder { get; private set; }
    public string StepNameSnapshot { get; private set; } = string.Empty;
    public string TargetKindSnapshot { get; private set; } = string.Empty;
    public Guid? ApproverUserIdSnapshot { get; private set; }
    public Guid? ApproverRoleIdSnapshot { get; private set; }
    public string Status { get; private set; } = WorkflowApprovalStatuses.Waiting;
    public Guid? ActionByUserId { get; private set; }
    public DateTimeOffset? ActionAt { get; private set; }
    public string? Comment { get; private set; }

    public static WorkflowRequestApproval Create(Guid companyId, Guid requestId, int stepOrder, string stepName, string targetKind, Guid? approverUserId, Guid? approverRoleId, bool pending)
    {
        if (companyId == Guid.Empty || requestId == Guid.Empty) throw new ArgumentException("Required identifiers are missing.");
        return new WorkflowRequestApproval
        {
            CompanyId = companyId,
            RequestId = requestId,
            StepOrder = stepOrder,
            StepNameSnapshot = Required(stepName, 150),
            TargetKindSnapshot = Required(targetKind, 20).ToUpperInvariant(),
            ApproverUserIdSnapshot = approverUserId,
            ApproverRoleIdSnapshot = approverRoleId,
            Status = pending ? WorkflowApprovalStatuses.Pending : WorkflowApprovalStatuses.Waiting
        };
    }

    public void Approve(Guid actorUserId, string? comment, DateTimeOffset now)
    {
        if (Status != WorkflowApprovalStatuses.Pending) throw new InvalidOperationException("Only pending approval can be approved.");
        Status = WorkflowApprovalStatuses.Approved; ActionByUserId = actorUserId; ActionAt = now.ToUniversalTime(); Comment = Optional(comment, 1000);
    }

    public void Reject(Guid actorUserId, string? comment, DateTimeOffset now)
    {
        if (Status != WorkflowApprovalStatuses.Pending) throw new InvalidOperationException("Only pending approval can be rejected.");
        Status = WorkflowApprovalStatuses.Rejected; ActionByUserId = actorUserId; ActionAt = now.ToUniversalTime(); Comment = Optional(comment, 1000);
    }

    public void Activate()
    {
        if (Status != WorkflowApprovalStatuses.Waiting) throw new InvalidOperationException("Only waiting approval can become pending.");
        Status = WorkflowApprovalStatuses.Pending;
    }

    private static string Required(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
    private static string? Optional(string? value, int max) { if (string.IsNullOrWhiteSpace(value)) return null; var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
}

public sealed class WorkflowRequestHistory : Entity
{
    private WorkflowRequestHistory() { }
    public Guid CompanyId { get; private set; }
    public Guid RequestId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string? FromStatus { get; private set; }
    public string ToStatus { get; private set; } = string.Empty;
    public Guid ActorUserId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string DetailsJson { get; private set; } = "{}";

    public static WorkflowRequestHistory Create(Guid companyId, Guid requestId, string eventType, string? fromStatus, string toStatus, Guid actorUserId, DateTimeOffset occurredAt, string detailsJson)
    {
        if (companyId == Guid.Empty || requestId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Required identifiers are missing.");
        return new WorkflowRequestHistory { CompanyId = companyId, RequestId = requestId, EventType = Required(eventType, 80).ToUpperInvariant(), FromStatus = string.IsNullOrWhiteSpace(fromStatus) ? null : fromStatus.Trim().ToUpperInvariant(), ToStatus = Required(toStatus, 30).ToUpperInvariant(), ActorUserId = actorUserId, OccurredAt = occurredAt.ToUniversalTime(), DetailsJson = Required(detailsJson, 10000) };
    }

    private static string Required(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
}

public sealed class WorkflowSlaEvent : Entity
{
    private WorkflowSlaEvent() { }
    public Guid CompanyId { get; private set; }
    public Guid RequestId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Severity { get; private set; } = WorkflowPriorities.Normal;
    public string DedupeKey { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string MetadataJson { get; private set; } = "{}";
    public DateTimeOffset CreatedAt { get; private set; }

    public static WorkflowSlaEvent Create(Guid companyId, Guid requestId, string eventType, string severity, string dedupeKey, string message, string metadataJson, DateTimeOffset createdAt)
    {
        if (companyId == Guid.Empty || requestId == Guid.Empty) throw new ArgumentException("Required identifiers are missing.");
        var normalizedSeverity = Required(severity, 20).ToUpperInvariant();
        if (!WorkflowPriorities.IsKnown(normalizedSeverity)) throw new ArgumentException("Severity is invalid.", nameof(severity));
        return new WorkflowSlaEvent { CompanyId = companyId, RequestId = requestId, EventType = Required(eventType, 80).ToUpperInvariant(), Severity = normalizedSeverity, DedupeKey = Required(dedupeKey, 300), Message = Required(message, 1000), MetadataJson = Required(metadataJson, 10000), CreatedAt = createdAt.ToUniversalTime() };
    }

    private static string Required(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
}
