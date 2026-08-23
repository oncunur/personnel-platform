using PersonnelPlatform.Domain.Attendance;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Application.Attendance;

public interface IOvertimeRepository
{
    Task<OvertimeRequest?> FindAsync(Guid overtimeId, CancellationToken cancellationToken);
    Task<OvertimeRequest?> FindActiveByDailyAttendanceAsync(Guid dailyAttendanceId, CancellationToken cancellationToken);
    Task<OvertimeRequestSummary?> GetSummaryAsync(Guid overtimeId, CancellationToken cancellationToken);
    Task<OvertimePagedResult<OvertimeRequestSummary>> SearchAsync(OvertimeQuery query, bool globalAccess, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<OvertimeInboxItem>> ListInboxAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? managerEmployeeId, bool canManagerApprove, bool canHrApprove, CancellationToken cancellationToken);
    Task<EmployeeUserLink?> FindUserLinkByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    void Add(OvertimeRequest request);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
