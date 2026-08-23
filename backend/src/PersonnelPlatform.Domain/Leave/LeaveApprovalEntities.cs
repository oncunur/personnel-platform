using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Leave;

public static class LeaveApprovalStepCodes
{
    public const string Manager = "MANAGER";
    public const string Hr = "HR";
}

public static class LeaveApprovalStatuses
{
    public const string Waiting = "WAITING";
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Skipped = "SKIPPED";

    public static bool IsTerminal(string status) => status is Approved or Rejected or Skipped;
}

public static class LeaveApprovalHistoryActions
{
    public const string WorkflowStarted = "WORKFLOW_STARTED";
    public const string StepActivated = "STEP_ACTIVATED";
    public const string StepApproved = "STEP_APPROVED";
    public const string StepRejected = "STEP_REJECTED";
    public const string StepSkipped = "STEP_SKIPPED";
    public const string ApproverLinked = "APPROVER_LINKED";
}

public sealed class LeaveApproval : AuditableEntity
{
    private LeaveApproval() { }

    public Guid LeaveId { get; private set; }
    public int StepOrder { get; private set; }
    public string StepCode { get; private set; } = string.Empty;
    public Guid? ApproverEmployeeId { get; private set; }
    public Guid? AssignedUserId { get; private set; }
    public string Status { get; private set; } = LeaveApprovalStatuses.Waiting;
    public Guid? DecidedByUserId { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public string? DecisionNote { get; private set; }

    public static LeaveApproval Create(Guid leaveId, int stepOrder, string stepCode, Guid? approverEmployeeId, Guid? assignedUserId, bool pending, DateTimeOffset now, Guid? actorUserId)
    {
        if (leaveId == Guid.Empty) throw new ArgumentException("Leave is required.", nameof(leaveId));
        if (stepOrder <= 0) throw new ArgumentOutOfRangeException(nameof(stepOrder));
        if (stepCode is not (LeaveApprovalStepCodes.Manager or LeaveApprovalStepCodes.Hr)) throw new ArgumentException("Approval step code is invalid.", nameof(stepCode));
        if (stepCode == LeaveApprovalStepCodes.Manager && approverEmployeeId is null) throw new ArgumentException("Manager employee is required for manager step.", nameof(approverEmployeeId));

        return new LeaveApproval
        {
            LeaveId = leaveId,
            StepOrder = stepOrder,
            StepCode = stepCode,
            ApproverEmployeeId = approverEmployeeId,
            AssignedUserId = assignedUserId,
            Status = pending ? LeaveApprovalStatuses.Pending : LeaveApprovalStatuses.Waiting,
            CreatedAt = now,
            CreatedBy = actorUserId
        };
    }

    public static LeaveApproval CreateSkipped(Guid leaveId, int stepOrder, string stepCode, DateTimeOffset now, Guid? actorUserId, string? reason = null)
    {
        var approval = new LeaveApproval
        {
            LeaveId = leaveId,
            StepOrder = stepOrder,
            StepCode = stepCode,
            Status = LeaveApprovalStatuses.Skipped,
            DecisionNote = Clean(reason, 1000),
            DecidedAt = now,
            DecidedByUserId = actorUserId,
            CreatedAt = now,
            CreatedBy = actorUserId
        };
        return approval;
    }

    public void Activate(DateTimeOffset now, Guid? actorUserId)
    {
        if (Status != LeaveApprovalStatuses.Waiting) throw new InvalidOperationException("Only waiting approval can be activated.");
        Status = LeaveApprovalStatuses.Pending;
        UpdatedAt = now;
        UpdatedBy = actorUserId;
        Version++;
    }

    public void AssignUser(Guid userId, DateTimeOffset now, Guid? actorUserId)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User is required.", nameof(userId));
        if (LeaveApprovalStatuses.IsTerminal(Status)) throw new InvalidOperationException("Terminal approval cannot be assigned.");
        AssignedUserId = userId;
        UpdatedAt = now;
        UpdatedBy = actorUserId;
        Version++;
    }

    public void Approve(Guid userId, string? note, DateTimeOffset now)
    {
        if (Status != LeaveApprovalStatuses.Pending) throw new InvalidOperationException("Only pending approval can be approved.");
        if (userId == Guid.Empty) throw new ArgumentException("Decision user is required.", nameof(userId));
        AssignedUserId ??= userId;
        DecidedByUserId = userId;
        DecidedAt = now;
        DecisionNote = Clean(note, 1000);
        Status = LeaveApprovalStatuses.Approved;
        UpdatedAt = now;
        UpdatedBy = userId;
        Version++;
    }

    public void Reject(Guid userId, string? note, DateTimeOffset now)
    {
        if (Status != LeaveApprovalStatuses.Pending) throw new InvalidOperationException("Only pending approval can be rejected.");
        if (userId == Guid.Empty) throw new ArgumentException("Decision user is required.", nameof(userId));
        AssignedUserId ??= userId;
        DecidedByUserId = userId;
        DecidedAt = now;
        DecisionNote = Clean(note, 1000);
        Status = LeaveApprovalStatuses.Rejected;
        UpdatedAt = now;
        UpdatedBy = userId;
        Version++;
    }

    public void Skip(Guid? actorUserId, string? reason, DateTimeOffset now)
    {
        if (LeaveApprovalStatuses.IsTerminal(Status)) return;
        Status = LeaveApprovalStatuses.Skipped;
        DecidedByUserId = actorUserId;
        DecidedAt = now;
        DecisionNote = Clean(reason, 1000);
        UpdatedAt = now;
        UpdatedBy = actorUserId;
        Version++;
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim();
        if (cleaned.Length > maxLength) throw new ArgumentException("Value is too long.");
        return cleaned;
    }
}

public sealed class LeaveApprovalHistory : Entity
{
    private LeaveApprovalHistory() { }

    public Guid LeaveId { get; private set; }
    public Guid? ApprovalId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? StepCode { get; private set; }
    public string? FromStatus { get; private set; }
    public string? ToStatus { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string? Note { get; private set; }

    public static LeaveApprovalHistory Create(Guid leaveId, Guid? approvalId, string action, string? stepCode, string? fromStatus, string? toStatus, Guid? actorUserId, DateTimeOffset occurredAt, string? note = null)
    {
        if (leaveId == Guid.Empty) throw new ArgumentException("Leave is required.", nameof(leaveId));
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        return new LeaveApprovalHistory
        {
            LeaveId = leaveId,
            ApprovalId = approvalId,
            Action = action.Trim().ToUpperInvariant(),
            StepCode = string.IsNullOrWhiteSpace(stepCode) ? null : stepCode.Trim().ToUpperInvariant(),
            FromStatus = string.IsNullOrWhiteSpace(fromStatus) ? null : fromStatus.Trim().ToUpperInvariant(),
            ToStatus = string.IsNullOrWhiteSpace(toStatus) ? null : toStatus.Trim().ToUpperInvariant(),
            ActorUserId = actorUserId,
            OccurredAt = occurredAt,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim().Length <= 1000 ? note.Trim() : throw new ArgumentException("History note is too long.", nameof(note))
        };
    }
}
