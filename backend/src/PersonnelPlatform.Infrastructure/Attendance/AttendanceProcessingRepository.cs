using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Attendance;
using PersonnelPlatform.Domain.Attendance;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Attendance;

public sealed class AttendanceProcessingRepository(ApplicationDbContext dbContext) : IAttendanceProcessingRepository
{
    public Task<RawAttendanceEvent?> FindRawByExternalIdAsync(Guid companyId, string source, string externalEventId, CancellationToken cancellationToken) =>
        dbContext.RawAttendanceEvents.AsNoTracking().FirstOrDefaultAsync(
            x => x.CompanyId == companyId && x.Source == source && x.ExternalEventId == externalEventId,
            cancellationToken);

    public async Task<IReadOnlyList<RawAttendanceEvent>> ListRawEventsAsync(Guid employeeId, DateOnly fromLocalDate, DateOnly toLocalDate, CancellationToken cancellationToken) =>
        await dbContext.RawAttendanceEvents.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.LocalDate >= fromLocalDate && x.LocalDate <= toLocalDate)
            .OrderBy(x => x.LocalDate)
            .ThenBy(x => x.LocalTime)
            .ThenBy(x => x.ReceivedAt)
            .ToListAsync(cancellationToken);

    public void AddRawEvent(RawAttendanceEvent rawEvent) => dbContext.RawAttendanceEvents.Add(rawEvent);

    public Task<AttendanceScheduleSnapshot?> FindScheduleAsync(Guid employeeId, DateOnly attendanceDate, CancellationToken cancellationToken) =>
        (from assignment in dbContext.EmployeeShiftAssignments.AsNoTracking()
         join shift in dbContext.Shifts.AsNoTracking() on assignment.ShiftId equals shift.Id
         join calendar in dbContext.WorkCalendars.AsNoTracking() on assignment.WorkCalendarId equals calendar.Id
         where assignment.EmployeeId == employeeId
               && assignment.DeletedAt == null
               && shift.DeletedAt == null && shift.IsActive
               && calendar.DeletedAt == null && calendar.IsActive
               && assignment.ValidFrom <= attendanceDate
               && (assignment.ValidUntil == null || assignment.ValidUntil >= attendanceDate)
         select new AttendanceScheduleSnapshot(
             assignment.Id,
             assignment.EmployeeId,
             shift.Id,
             calendar.Id,
             shift.StartTime,
             shift.EndTime,
             shift.BreakMinutes,
             shift.PlannedMinutes,
             shift.GraceInMinutes,
             shift.GraceOutMinutes,
             shift.CrossesMidnight))
        .SingleOrDefaultAsync(cancellationToken);

    public Task<WorkCalendarDay?> FindCalendarDayAsync(Guid workCalendarId, DateOnly attendanceDate, CancellationToken cancellationToken) =>
        dbContext.WorkCalendarDays.AsNoTracking().FirstOrDefaultAsync(
            x => x.WorkCalendarId == workCalendarId && x.Date == attendanceDate && x.DeletedAt == null,
            cancellationToken);

    public Task<ApprovedLeaveSnapshot?> FindApprovedLeaveAsync(Guid employeeId, DateOnly attendanceDate, CancellationToken cancellationToken) =>
        (from leave in dbContext.LeaveRequests.AsNoTracking()
         join leaveType in dbContext.LeaveTypes.AsNoTracking() on leave.LeaveTypeId equals leaveType.Id
         where leave.EmployeeId == employeeId
               && leave.DeletedAt == null
               && leaveType.DeletedAt == null
               && (leave.Status == "APPROVED" || leave.Status == "COMPLETED")
               && leave.StartDate <= attendanceDate
               && leave.EndDate >= attendanceDate
         select new ApprovedLeaveSnapshot(
             leave.Id,
             leaveType.Code,
             leave.StartDate,
             leave.EndDate,
             leave.StartDayPart,
             leave.EndDayPart))
        .FirstOrDefaultAsync(cancellationToken);

    public Task<DailyAttendance?> FindDailyAsync(Guid employeeId, DateOnly attendanceDate, CancellationToken cancellationToken) =>
        dbContext.DailyAttendances.FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.AttendanceDate == attendanceDate && x.DeletedAt == null, cancellationToken);

    public async Task<IReadOnlyList<DailyAttendance>> ListDailyAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await dbContext.DailyAttendances.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.AttendanceDate >= from && x.AttendanceDate <= to && x.DeletedAt == null)
            .OrderBy(x => x.AttendanceDate)
            .ToListAsync(cancellationToken);

    public void AddDaily(DailyAttendance attendance) => dbContext.DailyAttendances.Add(attendance);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
