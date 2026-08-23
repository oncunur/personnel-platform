using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Leave;
using PersonnelPlatform.Domain.Leave;
using PersonnelPlatform.Domain.Personnel;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Leave;

public sealed class LeaveApprovalRepository(ApplicationDbContext dbContext) : ILeaveApprovalRepository
{
    public Task<EmployeeUserLink?> FindLinkByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.EmployeeUserLinks.FirstOrDefaultAsync(x => x.UserId == userId && x.IsActive && x.DeletedAt == null, cancellationToken);

    public Task<EmployeeUserLink?> FindLinkByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken) =>
        dbContext.EmployeeUserLinks.FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.IsActive && x.DeletedAt == null, cancellationToken);

    public Task<bool> ActiveUserExistsAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(x => x.Id == userId && x.IsActive && x.DeletedAt == null, cancellationToken);

    public async Task<IReadOnlyList<EmployeeUserLinkSummary>> ListLinksAsync(bool globalAccess, IReadOnlyCollection<Guid> allowedCompanyIds, CancellationToken cancellationToken)
    {
        var companies = allowedCompanyIds.ToArray();
        var query =
            from link in dbContext.EmployeeUserLinks.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on link.UserId equals user.Id
            join employee in dbContext.Employees.AsNoTracking() on link.EmployeeId equals employee.Id
            where link.DeletedAt == null && employee.DeletedAt == null && user.DeletedAt == null
                && (globalAccess || companies.Contains(employee.CompanyId))
            orderby employee.LastName, employee.FirstName, user.Username
            select new EmployeeUserLinkSummary(
                link.Id,
                link.UserId,
                user.Username,
                link.EmployeeId,
                employee.EmployeeNo,
                (employee.FirstName + " " + employee.LastName).Trim(),
                employee.CompanyId,
                link.IsActive,
                link.Version);
        return await query.ToListAsync(cancellationToken);
    }

    public void AddEmployeeUserLink(EmployeeUserLink link) => dbContext.EmployeeUserLinks.Add(link);

    public async Task<IReadOnlyList<LeaveApproval>> ListApprovalsAsync(Guid leaveId, CancellationToken cancellationToken) =>
        await dbContext.LeaveApprovals.Where(x => x.LeaveId == leaveId && x.DeletedAt == null).OrderBy(x => x.StepOrder).ToListAsync(cancellationToken);

    public Task<LeaveApproval?> FindApprovalAsync(Guid approvalId, CancellationToken cancellationToken) =>
        dbContext.LeaveApprovals.FirstOrDefaultAsync(x => x.Id == approvalId && x.DeletedAt == null, cancellationToken);

    public Task<LeaveApproval?> FindApprovalByStepAsync(Guid leaveId, string stepCode, CancellationToken cancellationToken) =>
        dbContext.LeaveApprovals.FirstOrDefaultAsync(x => x.LeaveId == leaveId && x.StepCode == stepCode && x.DeletedAt == null, cancellationToken);

    public async Task<IReadOnlyList<LeaveApprovalSummary>> ListApprovalSummariesAsync(Guid leaveId, CancellationToken cancellationToken)
    {
        var query =
            from approval in dbContext.LeaveApprovals.AsNoTracking()
            join employee0 in dbContext.Employees.AsNoTracking() on approval.ApproverEmployeeId equals (Guid?)employee0.Id into employeeGroup
            from employee in employeeGroup.DefaultIfEmpty()
            join assigned0 in dbContext.Users.AsNoTracking() on approval.AssignedUserId equals (Guid?)assigned0.Id into assignedGroup
            from assigned in assignedGroup.DefaultIfEmpty()
            join decided0 in dbContext.Users.AsNoTracking() on approval.DecidedByUserId equals (Guid?)decided0.Id into decidedGroup
            from decided in decidedGroup.DefaultIfEmpty()
            where approval.LeaveId == leaveId && approval.DeletedAt == null
            orderby approval.StepOrder
            select new LeaveApprovalSummary(
                approval.Id,
                approval.LeaveId,
                approval.StepOrder,
                approval.StepCode,
                approval.ApproverEmployeeId,
                employee == null ? null : (employee.FirstName + " " + employee.LastName).Trim(),
                approval.AssignedUserId,
                assigned == null ? null : assigned.Username,
                approval.Status,
                approval.DecidedByUserId,
                decided == null ? null : decided.Username,
                approval.DecidedAt,
                approval.DecisionNote,
                approval.Version);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LeaveApprovalHistorySummary>> ListHistoryAsync(Guid leaveId, CancellationToken cancellationToken)
    {
        var query =
            from history in dbContext.LeaveApprovalHistories.AsNoTracking()
            join user0 in dbContext.Users.AsNoTracking() on history.ActorUserId equals (Guid?)user0.Id into userGroup
            from user in userGroup.DefaultIfEmpty()
            where history.LeaveId == leaveId
            orderby history.OccurredAt descending
            select new LeaveApprovalHistorySummary(
                history.Id,
                history.LeaveId,
                history.ApprovalId,
                history.Action,
                history.StepCode,
                history.FromStatus,
                history.ToStatus,
                history.ActorUserId,
                user == null ? null : user.Username,
                history.OccurredAt,
                history.Note);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LeaveApprovalInboxItem>> ListPendingInboxAsync(bool globalAccess, IReadOnlyCollection<Guid> allowedCompanyIds, Guid? linkedEmployeeId, CancellationToken cancellationToken)
    {
        var companies = allowedCompanyIds.ToArray();
        var query =
            from approval in dbContext.LeaveApprovals.AsNoTracking()
            join leave in dbContext.LeaveRequests.AsNoTracking() on approval.LeaveId equals leave.Id
            join employee in dbContext.Employees.AsNoTracking() on leave.EmployeeId equals employee.Id
            join type in dbContext.LeaveTypes.AsNoTracking() on leave.LeaveTypeId equals type.Id
            where approval.DeletedAt == null && leave.DeletedAt == null && employee.DeletedAt == null && type.DeletedAt == null
                && approval.Status == LeaveApprovalStatuses.Pending
                && (globalAccess || companies.Contains(employee.CompanyId))
            orderby leave.StartDate, employee.LastName, employee.FirstName
            select new LeaveApprovalInboxItem(
                approval.Id,
                approval.Version,
                leave.Id,
                leave.Version,
                employee.Id,
                employee.EmployeeNo,
                (employee.FirstName + " " + employee.LastName).Trim(),
                employee.CompanyId,
                type.Code,
                type.Name,
                leave.StartDate,
                leave.EndDate,
                leave.RequestedDays,
                approval.StepCode,
                approval.Status,
                approval.StepCode == LeaveApprovalStepCodes.Hr || (linkedEmployeeId != null && approval.ApproverEmployeeId == linkedEmployeeId));
        return await query.ToListAsync(cancellationToken);
    }

    public void AddApproval(LeaveApproval approval) => dbContext.LeaveApprovals.Add(approval);
    public void AddHistory(LeaveApprovalHistory history) => dbContext.LeaveApprovalHistories.Add(history);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
