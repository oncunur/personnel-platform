using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Attendance;
using PersonnelPlatform.Domain.Attendance;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Attendance;

public sealed class AttendanceSetupRepository(ApplicationDbContext dbContext) : IAttendanceSetupRepository
{
    public async Task<IReadOnlyList<WorkCalendar>> ListCalendarsAsync(Guid? companyId, bool globalAccess, IReadOnlyCollection<Guid> allowedCompanyIds, CancellationToken cancellationToken)
    {
        var query = dbContext.WorkCalendars.AsNoTracking().Where(x => x.DeletedAt == null);
        if (companyId is not null) query = query.Where(x => x.CompanyId == companyId.Value);
        else if (!globalAccess) query = query.Where(x => allowedCompanyIds.Contains(x.CompanyId));
        return await query.OrderBy(x => x.CompanyId).ThenByDescending(x => x.IsDefault).ThenBy(x => x.Code).ToListAsync(cancellationToken);
    }

    public Task<WorkCalendar?> FindCalendarAsync(Guid calendarId, CancellationToken cancellationToken) =>
        dbContext.WorkCalendars.FirstOrDefaultAsync(x => x.Id == calendarId && x.DeletedAt == null, cancellationToken);

    public Task<bool> CalendarCodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken) =>
        dbContext.WorkCalendars.AnyAsync(x => x.CompanyId == companyId && x.Code == code && x.DeletedAt == null, cancellationToken);

    public Task<bool> HasDefaultCalendarAsync(Guid companyId, CancellationToken cancellationToken) =>
        dbContext.WorkCalendars.AnyAsync(x => x.CompanyId == companyId && x.IsDefault && x.IsActive && x.DeletedAt == null, cancellationToken);

    public void AddCalendar(WorkCalendar calendar) => dbContext.WorkCalendars.Add(calendar);

    public async Task<IReadOnlyList<WorkCalendarDay>> ListCalendarDaysAsync(Guid calendarId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var query = dbContext.WorkCalendarDays.AsNoTracking().Where(x => x.WorkCalendarId == calendarId && x.DeletedAt == null);
        if (from is not null) query = query.Where(x => x.Date >= from.Value);
        if (to is not null) query = query.Where(x => x.Date <= to.Value);
        return await query.OrderBy(x => x.Date).ToListAsync(cancellationToken);
    }

    public Task<WorkCalendarDay?> FindCalendarDayAsync(Guid calendarId, DateOnly date, CancellationToken cancellationToken) =>
        dbContext.WorkCalendarDays.FirstOrDefaultAsync(x => x.WorkCalendarId == calendarId && x.Date == date && x.DeletedAt == null, cancellationToken);

    public void AddCalendarDay(WorkCalendarDay day) => dbContext.WorkCalendarDays.Add(day);

    public async Task<IReadOnlyList<ShiftDefinition>> ListShiftsAsync(Guid? companyId, bool globalAccess, IReadOnlyCollection<Guid> allowedCompanyIds, CancellationToken cancellationToken)
    {
        var query = dbContext.Shifts.AsNoTracking().Where(x => x.DeletedAt == null);
        if (companyId is not null) query = query.Where(x => x.CompanyId == companyId.Value);
        else if (!globalAccess) query = query.Where(x => allowedCompanyIds.Contains(x.CompanyId));
        return await query.OrderBy(x => x.CompanyId).ThenBy(x => x.Code).ToListAsync(cancellationToken);
    }

    public Task<ShiftDefinition?> FindShiftAsync(Guid shiftId, CancellationToken cancellationToken) =>
        dbContext.Shifts.FirstOrDefaultAsync(x => x.Id == shiftId && x.DeletedAt == null, cancellationToken);

    public Task<bool> ShiftCodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken) =>
        dbContext.Shifts.AnyAsync(x => x.CompanyId == companyId && x.Code == code && x.DeletedAt == null, cancellationToken);

    public void AddShift(ShiftDefinition shift) => dbContext.Shifts.Add(shift);

    public async Task<IReadOnlyList<EmployeeShiftAssignmentSummary>> ListEmployeeAssignmentsAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var query =
            from assignment in dbContext.EmployeeShiftAssignments.AsNoTracking()
            join shift in dbContext.Shifts.AsNoTracking() on assignment.ShiftId equals shift.Id
            join calendar in dbContext.WorkCalendars.AsNoTracking() on assignment.WorkCalendarId equals calendar.Id
            where assignment.EmployeeId == employeeId && assignment.DeletedAt == null && shift.DeletedAt == null && calendar.DeletedAt == null
            orderby assignment.ValidFrom descending
            select new EmployeeShiftAssignmentSummary(
                assignment.Id,
                assignment.EmployeeId,
                assignment.ShiftId,
                shift.Code,
                shift.Name,
                assignment.WorkCalendarId,
                calendar.Code,
                calendar.Name,
                assignment.ValidFrom,
                assignment.ValidUntil,
                assignment.Note,
                shift.CrossesMidnight,
                shift.StartTime,
                shift.EndTime,
                shift.PlannedMinutes,
                assignment.Version);
        return await query.ToListAsync(cancellationToken);
    }

    public Task<bool> HasAssignmentOverlapAsync(Guid employeeId, DateOnly validFrom, DateOnly? validUntil, CancellationToken cancellationToken) =>
        dbContext.EmployeeShiftAssignments.AnyAsync(x =>
            x.EmployeeId == employeeId
            && x.DeletedAt == null
            && (validUntil == null || x.ValidFrom <= validUntil.Value)
            && (x.ValidUntil == null || x.ValidUntil.Value >= validFrom),
            cancellationToken);

    public void AddAssignment(EmployeeShiftAssignment assignment) => dbContext.EmployeeShiftAssignments.Add(assignment);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
