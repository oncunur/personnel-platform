using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Leave;
using PersonnelPlatform.Domain.Leave;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Leave;

public sealed class LeaveRepository(ApplicationDbContext dbContext) : ILeaveRepository
{
    public async Task<IReadOnlyList<LeaveType>> ListLeaveTypesAsync(CancellationToken cancellationToken) =>
        await dbContext.LeaveTypes.Where(x => x.DeletedAt == null).OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<LeaveType?> FindLeaveTypeAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.LeaveTypes.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, cancellationToken);

    public Task<bool> LeaveTypeCodeExistsAsync(string code, CancellationToken cancellationToken) =>
        dbContext.LeaveTypes.AnyAsync(x => x.Code == code && x.DeletedAt == null, cancellationToken);

    public void AddLeaveType(LeaveType leaveType) => dbContext.LeaveTypes.Add(leaveType);

    public Task<LeaveRequest?> FindLeaveRequestAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.LeaveRequests.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, cancellationToken);

    public async Task<LeaveRequestSummary?> GetLeaveRequestSummaryAsync(Guid id, CancellationToken cancellationToken) =>
        await SummaryQuery().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<LeavePagedResult<LeaveRequestSummary>> SearchLeaveRequestsAsync(LeaveQuery query, bool globalAccess, IReadOnlyCollection<Guid> allowedCompanyIds, CancellationToken cancellationToken)
    {
        var companies = allowedCompanyIds.ToArray();
        var source = SummaryQuery();
        if (!globalAccess) source = source.Where(x => companies.Contains(x.CompanyId));
        if (query.CompanyId is not null) source = source.Where(x => x.CompanyId == query.CompanyId.Value);
        if (query.EmployeeId is not null) source = source.Where(x => x.EmployeeId == query.EmployeeId.Value);
        if (query.LeaveTypeId is not null) source = source.Where(x => x.LeaveTypeId == query.LeaveTypeId.Value);
        if (!string.IsNullOrWhiteSpace(query.Status)) source = source.Where(x => x.Status == query.Status);
        if (query.From is not null) source = source.Where(x => x.EndDate >= query.From.Value);
        if (query.To is not null) source = source.Where(x => x.StartDate <= query.To.Value);

        var total = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderByDescending(x => x.StartDate)
            .ThenBy(x => x.EmployeeName)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return new LeavePagedResult<LeaveRequestSummary>(items, query.Page, query.PageSize, total);
    }

    public Task<bool> HasBlockingOverlapAsync(Guid employeeId, DateOnly startDate, DateOnly endDate, Guid? exceptLeaveId, CancellationToken cancellationToken)
    {
        var statuses = new[] { LeaveRequestStatuses.Submitted, LeaveRequestStatuses.PendingApproval, LeaveRequestStatuses.Approved, LeaveRequestStatuses.Completed };
        return dbContext.LeaveRequests.AnyAsync(x => x.EmployeeId == employeeId && x.DeletedAt == null
            && statuses.Contains(x.Status)
            && x.StartDate <= endDate && x.EndDate >= startDate
            && (exceptLeaveId == null || x.Id != exceptLeaveId.Value), cancellationToken);
    }

    public void AddLeaveRequest(LeaveRequest leaveRequest) => dbContext.LeaveRequests.Add(leaveRequest);

    public async Task<IReadOnlyList<LeaveBalance>> ListBalancesAsync(Guid employeeId, CancellationToken cancellationToken) =>
        await dbContext.LeaveBalances.Where(x => x.EmployeeId == employeeId && x.DeletedAt == null).OrderByDescending(x => x.PeriodStart).ToListAsync(cancellationToken);

    public Task<LeaveBalance?> FindBalanceForRangeAsync(Guid employeeId, Guid leaveTypeId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken) =>
        dbContext.LeaveBalances.FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.LeaveTypeId == leaveTypeId && x.DeletedAt == null
            && x.PeriodStart <= startDate && x.PeriodEnd >= endDate, cancellationToken);

    public Task<LeaveBalance?> FindBalanceExactAsync(Guid employeeId, Guid leaveTypeId, DateOnly periodStart, DateOnly periodEnd, CancellationToken cancellationToken) =>
        dbContext.LeaveBalances.FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.LeaveTypeId == leaveTypeId && x.DeletedAt == null
            && x.PeriodStart == periodStart && x.PeriodEnd == periodEnd, cancellationToken);

    public void AddLeaveBalance(LeaveBalance balance) => dbContext.LeaveBalances.Add(balance);

    public Task<LeaveEntitlement?> FindEntitlementExactAsync(Guid employeeId, Guid leaveTypeId, DateOnly periodStart, DateOnly periodEnd, CancellationToken cancellationToken) =>
        dbContext.LeaveEntitlements.FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.LeaveTypeId == leaveTypeId && x.DeletedAt == null
            && x.PeriodStart == periodStart && x.PeriodEnd == periodEnd, cancellationToken);

    public Task<bool> HasEntitlementPeriodOverlapAsync(Guid employeeId, Guid leaveTypeId, DateOnly periodStart, DateOnly periodEnd, Guid? exceptEntitlementId, CancellationToken cancellationToken) =>
        dbContext.LeaveEntitlements.AnyAsync(x => x.EmployeeId == employeeId && x.LeaveTypeId == leaveTypeId && x.DeletedAt == null
            && x.PeriodStart <= periodEnd && x.PeriodEnd >= periodStart
            && (exceptEntitlementId == null || x.Id != exceptEntitlementId.Value), cancellationToken);

    public void AddLeaveEntitlement(LeaveEntitlement entitlement) => dbContext.LeaveEntitlements.Add(entitlement);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);

    private IQueryable<LeaveRequestSummary> SummaryQuery() =>
        from leave in dbContext.LeaveRequests.AsNoTracking()
        join employee in dbContext.Employees.AsNoTracking() on leave.EmployeeId equals employee.Id
        join type in dbContext.LeaveTypes.AsNoTracking() on leave.LeaveTypeId equals type.Id
        where leave.DeletedAt == null && employee.DeletedAt == null && type.DeletedAt == null
        select new LeaveRequestSummary(
            leave.Id,
            leave.EmployeeId,
            employee.EmployeeNo,
            (employee.FirstName + " " + employee.LastName).Trim(),
            employee.CompanyId,
            leave.LeaveTypeId,
            type.Code,
            type.Name,
            leave.StartDate,
            leave.EndDate,
            leave.StartDayPart,
            leave.EndDayPart,
            leave.RequestedDays,
            leave.Reason,
            leave.Status,
            leave.SubmittedAt,
            leave.Version);
}
