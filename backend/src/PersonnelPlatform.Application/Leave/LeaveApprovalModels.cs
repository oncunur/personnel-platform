namespace PersonnelPlatform.Application.Leave;

public sealed record EmployeeUserLinkSummary(
    Guid Id,
    Guid UserId,
    string Username,
    Guid EmployeeId,
    string EmployeeNo,
    string EmployeeName,
    Guid CompanyId,
    bool IsActive,
    int Version);

public sealed record SetEmployeeUserLinkRequest(Guid EmployeeId);

public sealed record LeaveApprovalSummary(
    Guid Id,
    Guid LeaveId,
    int StepOrder,
    string StepCode,
    Guid? ApproverEmployeeId,
    string? ApproverEmployeeName,
    Guid? AssignedUserId,
    string? AssignedUsername,
    string Status,
    Guid? DecidedByUserId,
    string? DecidedByUsername,
    DateTimeOffset? DecidedAt,
    string? DecisionNote,
    int Version);

public sealed record LeaveApprovalHistorySummary(
    Guid Id,
    Guid LeaveId,
    Guid? ApprovalId,
    string Action,
    string? StepCode,
    string? FromStatus,
    string? ToStatus,
    Guid? ActorUserId,
    string? ActorUsername,
    DateTimeOffset OccurredAt,
    string? Note);

public sealed record LeaveApprovalInboxItem(
    Guid ApprovalId,
    int ApprovalVersion,
    Guid LeaveId,
    int LeaveVersion,
    Guid EmployeeId,
    string EmployeeNo,
    string EmployeeName,
    Guid CompanyId,
    string LeaveTypeCode,
    string LeaveTypeName,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal RequestedDays,
    string StepCode,
    string Status,
    bool CanDecide);

public sealed record LeaveApprovalDecisionRequest(
    int ApprovalVersion,
    int LeaveVersion,
    bool Approve,
    string? Note);

public sealed record LeaveApprovalWorkflowDetail(
    LeaveRequestSummary Leave,
    IReadOnlyList<LeaveApprovalSummary> Steps,
    IReadOnlyList<LeaveApprovalHistorySummary> History);
