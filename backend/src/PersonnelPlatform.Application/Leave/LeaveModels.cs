namespace PersonnelPlatform.Application.Leave;

public static class LeavePermissions
{
    public const string TypeView = "leave.type.view";
    public const string TypeManage = "leave.type.manage";
    public const string View = "leave.view";
    public const string Create = "leave.create";
    public const string Submit = "leave.submit";
    public const string BalanceView = "leave.balance.view";
    public const string BalanceManage = "leave.balance.manage";
    public const string Approve = "leave.approve";
}

public sealed record LeaveTypeSummary(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsPaid,
    bool BalanceRequired,
    bool AllowHalfDay,
    bool AttachmentRequired,
    bool IsActive,
    int DisplayOrder);

public sealed record CreateLeaveTypeRequest(
    string Code,
    string Name,
    string? Description,
    bool IsPaid,
    bool BalanceRequired,
    bool AllowHalfDay,
    bool AttachmentRequired,
    int DisplayOrder);

public sealed record LeaveRequestSummary(
    Guid Id,
    Guid EmployeeId,
    string EmployeeNo,
    string EmployeeName,
    Guid CompanyId,
    Guid LeaveTypeId,
    string LeaveTypeCode,
    string LeaveTypeName,
    DateOnly StartDate,
    DateOnly EndDate,
    string StartDayPart,
    string EndDayPart,
    decimal RequestedDays,
    string? Reason,
    string Status,
    DateTimeOffset? SubmittedAt,
    int Version);

public sealed record CreateLeaveRequest(
    Guid EmployeeId,
    Guid LeaveTypeId,
    DateOnly StartDate,
    DateOnly EndDate,
    string StartDayPart,
    string EndDayPart,
    string? Reason);

public sealed record LeaveActionRequest(int Version);

public sealed record LeaveBalanceSummary(
    Guid Id,
    Guid EmployeeId,
    Guid LeaveTypeId,
    string LeaveTypeCode,
    string LeaveTypeName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal EntitledDays,
    decimal CarryOverDays,
    decimal AdjustmentDays,
    decimal ReservedDays,
    decimal UsedDays,
    decimal AvailableDays,
    int Version);

public sealed record UpsertLeaveEntitlementRequest(
    Guid LeaveTypeId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal EntitledDays,
    decimal CarryOverDays,
    decimal AdjustmentDays,
    string? Note);

public sealed record LeaveQuery(
    Guid? EmployeeId,
    Guid? CompanyId,
    Guid? LeaveTypeId,
    string? Status,
    DateOnly? From,
    DateOnly? To,
    int Page,
    int PageSize);

public sealed record LeavePagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record LeaveResult<T>(bool Succeeded, T? Value, string? ErrorCode, string? ErrorMessage) where T : class
{
    public static LeaveResult<T> Success(T value) => new(true, value, null, null);
    public static LeaveResult<T> Failure(string code, string message) => new(false, null, code, message);
}
