namespace PersonnelPlatform.Application.Attendance;

public static class OvertimePermissions
{
    public const string View = "attendance.overtime.view";
    public const string Request = "attendance.overtime.request";
    public const string ManagerApprove = "attendance.overtime.manager.approve";
    public const string HrApprove = "attendance.overtime.hr.approve";
}

public sealed record CreateOvertimeRequest(Guid DailyAttendanceId, int RequestedMinutes, string? Reason);
public sealed record OvertimeDecisionRequest(bool Approve, int? ApprovedMinutes, string? Note, int Version);
public sealed record OvertimeCancelRequest(int Version);

public sealed record OvertimeRequestSummary(
    Guid Id,
    Guid CompanyId,
    Guid EmployeeId,
    string EmployeeNo,
    string EmployeeName,
    Guid DailyAttendanceId,
    DateOnly AttendanceDate,
    int CandidateMinutes,
    int RequestedMinutes,
    int ApprovedMinutes,
    string Status,
    string? Reason,
    DateTimeOffset SubmittedAt,
    string? DecisionNote,
    int Version);

public sealed record OvertimeInboxItem(
    Guid Id,
    Guid CompanyId,
    Guid EmployeeId,
    string EmployeeNo,
    string EmployeeName,
    DateOnly AttendanceDate,
    int CandidateMinutes,
    int RequestedMinutes,
    string Status,
    bool CanDecide,
    int Version);

public sealed record OvertimeQuery(Guid? EmployeeId, Guid? CompanyId, string? Status, DateOnly? From, DateOnly? To, int Page, int PageSize);
public sealed record OvertimePagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
