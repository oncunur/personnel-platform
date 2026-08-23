using PersonnelPlatform.Domain.Leave;

namespace PersonnelPlatform.Application.Leave;

public interface ILeaveRepository
{
    Task<IReadOnlyList<LeaveType>> ListLeaveTypesAsync(CancellationToken cancellationToken);
    Task<LeaveType?> FindLeaveTypeAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> LeaveTypeCodeExistsAsync(string code, CancellationToken cancellationToken);
    void AddLeaveType(LeaveType leaveType);

    Task<LeaveRequest?> FindLeaveRequestAsync(Guid id, CancellationToken cancellationToken);
    Task<LeaveRequestSummary?> GetLeaveRequestSummaryAsync(Guid id, CancellationToken cancellationToken);
    Task<LeavePagedResult<LeaveRequestSummary>> SearchLeaveRequestsAsync(LeaveQuery query, bool globalAccess, IReadOnlyCollection<Guid> allowedCompanyIds, CancellationToken cancellationToken);
    Task<bool> HasBlockingOverlapAsync(Guid employeeId, DateOnly startDate, DateOnly endDate, string startDayPart, string endDayPart, Guid? exceptLeaveId, CancellationToken cancellationToken);
    void AddLeaveRequest(LeaveRequest leaveRequest);

    Task<IReadOnlyList<LeaveBalance>> ListBalancesAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<LeaveBalance?> FindBalanceForRangeAsync(Guid employeeId, Guid leaveTypeId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken);
    Task<LeaveBalance?> FindBalanceExactAsync(Guid employeeId, Guid leaveTypeId, DateOnly periodStart, DateOnly periodEnd, CancellationToken cancellationToken);
    void AddLeaveBalance(LeaveBalance balance);

    Task<LeaveEntitlement?> FindEntitlementExactAsync(Guid employeeId, Guid leaveTypeId, DateOnly periodStart, DateOnly periodEnd, CancellationToken cancellationToken);
    Task<bool> HasEntitlementPeriodOverlapAsync(Guid employeeId, Guid leaveTypeId, DateOnly periodStart, DateOnly periodEnd, Guid? exceptEntitlementId, CancellationToken cancellationToken);
    void AddLeaveEntitlement(LeaveEntitlement entitlement);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
