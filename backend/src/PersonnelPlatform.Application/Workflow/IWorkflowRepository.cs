using PersonnelPlatform.Domain.Identity;
using PersonnelPlatform.Domain.Personnel;
using PersonnelPlatform.Domain.Workflow;

namespace PersonnelPlatform.Application.Workflow;

public interface IWorkflowRepository
{
    Task<User?> FindUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<Role?> FindRoleAsync(Guid roleId, CancellationToken cancellationToken);
    Task<bool> UserHasRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken);
    Task<Employee?> FindEmployeeAsync(Guid employeeId, CancellationToken cancellationToken);

    Task<WorkflowRequestType?> FindRequestTypeAsync(Guid requestTypeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkflowRequestTypeSummary>> ListRequestTypesAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, bool? active, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkflowApprovalStepDefinition>> ListStepDefinitionsAsync(Guid requestTypeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkflowApprovalStepSummary>> ListStepSummariesAsync(Guid requestTypeId, CancellationToken cancellationToken);
    void AddRequestType(WorkflowRequestType requestType);
    void AddStepDefinition(WorkflowApprovalStepDefinition step);
    void RemoveStepDefinitions(IEnumerable<WorkflowApprovalStepDefinition> steps);

    Task<string> NextRequestNoAsync(Guid companyId, int year, CancellationToken cancellationToken);
    Task<WorkflowRequest?> FindRequestAsync(Guid requestId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkflowRequestSummary>> ListRequestsAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, Guid? employeeId, Guid? requesterUserId, string? status, int take, CancellationToken cancellationToken);
    Task<WorkflowRequestApproval?> FindCurrentApprovalAsync(Guid requestId, int stepOrder, CancellationToken cancellationToken);
    Task<WorkflowRequestApproval?> FindApprovalAsync(Guid requestId, int stepOrder, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkflowRequestApprovalSummary>> ListApprovalsAsync(Guid requestId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkflowTimelineSummary>> ListTimelineAsync(Guid requestId, int take, CancellationToken cancellationToken);
    void AddRequest(WorkflowRequest request);
    void AddApproval(WorkflowRequestApproval approval);
    void AddHistory(WorkflowRequestHistory history);

    Task<IReadOnlyList<WorkflowSlaCandidate>> BuildSlaCandidatesAsync(DateTimeOffset now, IReadOnlyCollection<Guid>? companyIds, CancellationToken cancellationToken);
    Task<bool> TryInsertSlaEventAsync(WorkflowSlaCandidate candidate, DateTimeOffset createdAt, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkflowSlaEventSummary>> ListSlaEventsAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, Guid? requestId, string? eventType, int take, CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record WorkflowSlaCandidate(Guid CompanyId, Guid RequestId, string EventType, string Severity, string DedupeKey, string Message, string MetadataJson);
