using PersonnelPlatform.Domain.Attendance;

namespace PersonnelPlatform.Application.Attendance;

public interface IAttendanceSetupRepository
{
    Task<IReadOnlyList<WorkCalendar>> ListCalendarsAsync(Guid? companyId, bool globalAccess, IReadOnlyCollection<Guid> allowedCompanyIds, CancellationToken cancellationToken);
    Task<WorkCalendar?> FindCalendarAsync(Guid calendarId, CancellationToken cancellationToken);
    Task<bool> CalendarCodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken);
    void AddCalendar(WorkCalendar calendar);

    Task<IReadOnlyList<WorkCalendarDay>> ListCalendarDaysAsync(Guid calendarId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
    Task<WorkCalendarDay?> FindCalendarDayAsync(Guid calendarId, DateOnly date, CancellationToken cancellationToken);
    void AddCalendarDay(WorkCalendarDay day);

    Task<IReadOnlyList<ShiftDefinition>> ListShiftsAsync(Guid? companyId, bool globalAccess, IReadOnlyCollection<Guid> allowedCompanyIds, CancellationToken cancellationToken);
    Task<ShiftDefinition?> FindShiftAsync(Guid shiftId, CancellationToken cancellationToken);
    Task<bool> ShiftCodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken);
    void AddShift(ShiftDefinition shift);

    Task<IReadOnlyList<EmployeeShiftAssignmentSummary>> ListEmployeeAssignmentsAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<bool> HasAssignmentOverlapAsync(Guid employeeId, DateOnly validFrom, DateOnly? validUntil, CancellationToken cancellationToken);
    void AddAssignment(EmployeeShiftAssignment assignment);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
