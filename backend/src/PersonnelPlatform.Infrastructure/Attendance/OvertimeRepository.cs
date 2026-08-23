using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Attendance;
using PersonnelPlatform.Domain.Attendance;
using PersonnelPlatform.Domain.Personnel;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Attendance;

public sealed class OvertimeRepository(ApplicationDbContext dbContext) : IOvertimeRepository
{
    public Task<DailyAttendance?> FindDailyAttendanceByIdAsync(Guid dailyAttendanceId, CancellationToken cancellationToken) =>
        dbContext.DailyAttendances.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dailyAttendanceId && x.DeletedAt == null, cancellationToken);

    public Task<OvertimeRequest?> FindAsync(Guid overtimeId, CancellationToken cancellationToken) =>
        dbContext.OvertimeRequests.FirstOrDefaultAsync(x => x.Id == overtimeId && x.DeletedAt == null, cancellationToken);

    public Task<OvertimeRequest?> FindActiveByDailyAttendanceAsync(Guid dailyAttendanceId, CancellationToken cancellationToken) =>
        dbContext.OvertimeRequests.AsNoTracking().FirstOrDefaultAsync(
            x => x.DailyAttendanceId == dailyAttendanceId
                 && x.DeletedAt == null
                 && x.Status != OvertimeRequestStatuses.Rejected
                 && x.Status != OvertimeRequestStatuses.Cancelled,
            cancellationToken);

    public Task<OvertimeRequestSummary?> GetSummaryAsync(Guid overtimeId, CancellationToken cancellationToken) =>
        SummaryQuery().FirstOrDefaultAsync(x => x.Id == overtimeId, cancellationToken);

    public async Task<OvertimePagedResult<OvertimeRequestSummary>> SearchAsync(OvertimeQuery query, bool globalAccess, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken)
    {
        var source = SummaryQuery();
        if (!globalAccess) source = source.Where(x => companyIds.Contains(x.CompanyId));
        if (query.CompanyId is not null) source = source.Where(x => x.CompanyId == query.CompanyId.Value);
        if (query.EmployeeId is not null) source = source.Where(x => x.EmployeeId == query.EmployeeId.Value);
        if (!string.IsNullOrWhiteSpace(query.Status)) source = source.Where(x => x.Status == query.Status.Trim().ToUpperInvariant());
        if (query.From is not null) source = source.Where(x => x.AttendanceDate >= query.From.Value);
        if (query.To is not null) source = source.Where(x => x.AttendanceDate <= query.To.Value);

        var total = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderByDescending(x => x.AttendanceDate)
            .ThenByDescending(x => x.SubmittedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return new OvertimePagedResult<OvertimeRequestSummary>(items, query.Page, query.PageSize, total);
    }

    public async Task<IReadOnlyList<OvertimeInboxItem>> ListInboxAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? managerEmployeeId, bool canManagerApprove, bool canHrApprove, CancellationToken cancellationToken)
    {
        var query =
            from overtime in dbContext.OvertimeRequests.AsNoTracking()
            join employee in dbContext.Employees.AsNoTracking() on overtime.EmployeeId equals employee.Id
            where overtime.DeletedAt == null && employee.DeletedAt == null
            select new { overtime, employee };

        if (!globalAccess) query = query.Where(x => companyIds.Contains(x.overtime.CompanyId));

        query = query.Where(x =>
            (canManagerApprove
             && managerEmployeeId != null
             && x.overtime.Status == OvertimeRequestStatuses.PendingManager
             && x.employee.ManagerEmployeeId == managerEmployeeId)
            || (canHrApprove && x.overtime.Status == OvertimeRequestStatuses.PendingHr));

        return await query
            .OrderBy(x => x.overtime.AttendanceDate)
            .ThenBy(x => x.overtime.SubmittedAt)
            .Select(x => new OvertimeInboxItem(
                x.overtime.Id,
                x.overtime.CompanyId,
                x.overtime.EmployeeId,
                x.employee.EmployeeNo,
                x.employee.FirstName + " " + x.employee.LastName,
                x.overtime.AttendanceDate,
                x.overtime.CandidateMinutes,
                x.overtime.RequestedMinutes,
                x.overtime.Status,
                true,
                x.overtime.Version))
            .ToListAsync(cancellationToken);
    }

    public Task<EmployeeUserLink?> FindUserLinkByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.EmployeeUserLinks.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.DeletedAt == null, cancellationToken);

    public void Add(OvertimeRequest request) => dbContext.OvertimeRequests.Add(request);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);

    private IQueryable<OvertimeRequestSummary> SummaryQuery() =>
        from overtime in dbContext.OvertimeRequests.AsNoTracking()
        join employee in dbContext.Employees.AsNoTracking() on overtime.EmployeeId equals employee.Id
        where overtime.DeletedAt == null && employee.DeletedAt == null
        select new OvertimeRequestSummary(
            overtime.Id,
            overtime.CompanyId,
            overtime.EmployeeId,
            employee.EmployeeNo,
            employee.FirstName + " " + employee.LastName,
            overtime.DailyAttendanceId,
            overtime.SourceDailyVersion,
            overtime.AttendanceDate,
            overtime.CandidateMinutes,
            overtime.RequestedMinutes,
            overtime.ApprovedMinutes,
            overtime.Status,
            overtime.Reason,
            overtime.SubmittedAt,
            overtime.DecisionNote,
            overtime.Version);
}
