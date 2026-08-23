using PersonnelPlatform.Domain.Leave;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Application.Leave;

public interface ILeaveApprovalRepository
{
    Task<EmployeeUserLink?> FindLinkByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<EmployeeUserLink?> FindLinkByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<bool> ActiveUserExistsAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<EmployeeUserLinkSummary>> ListLinksAsync(bool globalAccess, IReadOnlyCollection<Guid> allowedCompanyIds, CancellationToken cancellationToken);
    void AddEmployeeUserLink(EmployeeUserLink link);

    Task<IReadOnlyList<LeaveApproval>> ListApprovalsAsync(Guid leaveId, CancellationToken cancellationToken);
    Task<LeaveApproval?> FindApprovalAsync(Guid approvalId, CancellationToken cancellationToken);
    Task<LeaveApproval?> FindApprovalByStepAsync(Guid leaveId, string stepCode, CancellationToken cancellationToken);
    Task<IReadOnlyList<LeaveApprovalSummary>> ListApprovalSummariesAsync(Guid leaveId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LeaveApprovalHistorySummary>> ListHistoryAsync(Guid leaveId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LeaveApprovalInboxItem>> ListPendingInboxAsync(bool globalAccess, IReadOnlyCollection<Guid> allowedCompanyIds, Guid? linkedEmployeeId, CancellationToken cancellationToken);
    void AddApproval(LeaveApproval approval);
    void AddHistory(LeaveApprovalHistory history);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
