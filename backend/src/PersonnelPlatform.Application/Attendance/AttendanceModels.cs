namespace PersonnelPlatform.Application.Attendance;

public static class AttendancePermissions
{
    public const string CalendarView = "attendance.calendar.view";
    public const string CalendarManage = "attendance.calendar.manage";
    public const string ShiftView = "attendance.shift.view";
    public const string ShiftManage = "attendance.shift.manage";
    public const string AssignmentView = "attendance.assignment.view";
    public const string AssignmentManage = "attendance.assignment.manage";
}

public sealed record WorkCalendarSummary(Guid Id, Guid CompanyId, string Code, string Name, bool IsDefault, bool IsActive, int Version);
public sealed record CreateWorkCalendarRequest(Guid CompanyId, string Code, string Name, bool IsDefault);

public sealed record WorkCalendarDaySummary(Guid Id, Guid WorkCalendarId, DateOnly Date, string DayType, int PlannedMinutes, bool IsPaid, string? Description, int Version);
public sealed record UpsertWorkCalendarDayRequest(DateOnly Date, string DayType, int PlannedMinutes, bool IsPaid, string? Description);

public sealed record ShiftSummary(
    Guid Id,
    Guid CompanyId,
    string Code,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int BreakMinutes,
    int PlannedMinutes,
    int GraceInMinutes,
    int GraceOutMinutes,
    bool CrossesMidnight,
    bool IsActive,
    int Version);

public sealed record CreateShiftRequest(
    Guid CompanyId,
    string Code,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int BreakMinutes,
    int GraceInMinutes,
    int GraceOutMinutes);

public sealed record EmployeeShiftAssignmentSummary(
    Guid Id,
    Guid EmployeeId,
    Guid ShiftId,
    string ShiftCode,
    string ShiftName,
    Guid WorkCalendarId,
    string CalendarCode,
    string CalendarName,
    DateOnly ValidFrom,
    DateOnly? ValidUntil,
    string? Note,
    bool CrossesMidnight,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int PlannedMinutes,
    int Version);

public sealed record CreateEmployeeShiftAssignmentRequest(Guid ShiftId, Guid WorkCalendarId, DateOnly ValidFrom, DateOnly? ValidUntil, string? Note);

public sealed record AttendanceResult<T>(bool Succeeded, T? Value, string? ErrorCode, string? ErrorMessage) where T : class
{
    public static AttendanceResult<T> Success(T value) => new(true, value, null, null);
    public static AttendanceResult<T> Failure(string code, string message) => new(false, null, code, message);
}
