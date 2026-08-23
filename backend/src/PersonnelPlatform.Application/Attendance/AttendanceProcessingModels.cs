namespace PersonnelPlatform.Application.Attendance;

public static class AttendanceProcessingPermissions
{
    public const string RawView = "attendance.raw.view";
    public const string RawIngest = "attendance.raw.ingest";
    public const string DailyView = "attendance.daily.view";
    public const string DailyCalculate = "attendance.daily.calculate";
}

public sealed record CreateRawAttendanceEventRequest(
    Guid CompanyId,
    Guid EmployeeId,
    string Source,
    string Direction,
    DateTimeOffset EventAt,
    string? DeviceCode,
    string? ExternalEventId,
    string? RawPayloadJson);

public sealed record RawAttendanceEventSummary(
    Guid Id,
    Guid CompanyId,
    Guid EmployeeId,
    string Source,
    string Direction,
    DateTimeOffset EventAt,
    DateOnly LocalDate,
    TimeOnly LocalTime,
    int UtcOffsetMinutes,
    string? DeviceCode,
    string? ExternalEventId,
    DateTimeOffset ReceivedAt);

public sealed record AttendanceScheduleSnapshot(
    Guid AssignmentId,
    Guid EmployeeId,
    Guid ShiftId,
    Guid WorkCalendarId,
    TimeOnly ShiftStartTime,
    TimeOnly ShiftEndTime,
    int BreakMinutes,
    int ShiftPlannedMinutes,
    int GraceInMinutes,
    int GraceOutMinutes,
    bool CrossesMidnight);

public sealed record ApprovedLeaveSnapshot(
    Guid LeaveId,
    string LeaveTypeCode,
    DateOnly StartDate,
    DateOnly EndDate,
    string StartDayPart,
    string EndDayPart);

public sealed record CalculateDailyAttendanceRequest(Guid EmployeeId, DateOnly AttendanceDate);

public sealed record DailyAttendanceSummary(
    Guid Id,
    Guid CompanyId,
    Guid EmployeeId,
    DateOnly AttendanceDate,
    Guid? ShiftAssignmentId,
    Guid? ShiftId,
    Guid? WorkCalendarId,
    Guid? LeaveId,
    string Status,
    string ProcessingStatus,
    int PlannedMinutes,
    int LeaveMinutes,
    int WorkedMinutes,
    int LateMinutes,
    int EarlyLeaveMinutes,
    int OvertimeCandidateMinutes,
    DateTimeOffset? FirstInAt,
    DateTimeOffset? LastOutAt,
    string? CalculationMessage,
    DateTimeOffset CalculatedAt,
    int Version);
