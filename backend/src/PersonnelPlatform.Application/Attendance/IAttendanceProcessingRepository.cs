using PersonnelPlatform.Domain.Attendance;

namespace PersonnelPlatform.Application.Attendance;

public interface IAttendanceProcessingRepository
{
    Task<RawAttendanceEvent?> FindRawByExternalIdAsync(Guid companyId, string source, string externalEventId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RawAttendanceEvent>> ListRawEventsAsync(Guid employeeId, DateOnly fromLocalDate, DateOnly toLocalDate, CancellationToken cancellationToken);
    void AddRawEvent(RawAttendanceEvent rawEvent);

    Task<AttendanceScheduleSnapshot?> FindScheduleAsync(Guid employeeId, DateOnly attendanceDate, CancellationToken cancellationToken);
    Task<WorkCalendarDay?> FindCalendarDayAsync(Guid workCalendarId, DateOnly attendanceDate, CancellationToken cancellationToken);
    Task<ApprovedLeaveSnapshot?> FindApprovedLeaveAsync(Guid employeeId, DateOnly attendanceDate, CancellationToken cancellationToken);

    Task<DailyAttendance?> FindDailyAsync(Guid employeeId, DateOnly attendanceDate, CancellationToken cancellationToken);
    Task<IReadOnlyList<DailyAttendance>> ListDailyAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    void AddDaily(DailyAttendance attendance);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
