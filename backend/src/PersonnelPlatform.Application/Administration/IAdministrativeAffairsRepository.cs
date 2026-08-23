using PersonnelPlatform.Domain.Administration;
using PersonnelPlatform.Domain.Identity;

namespace PersonnelPlatform.Application.Administration;

public interface IAdministrativeAffairsRepository
{
    Task<User?> FindUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<AdministrativeTask?> FindTaskAsync(Guid taskId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdministrativeTaskSummary>> ListTasksAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, Guid? responsibleUserId, string? status, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdministrativeTaskCompletionSummary>> ListTaskCompletionsAsync(Guid taskId, int take, CancellationToken cancellationToken);
    void AddTask(AdministrativeTask task);
    void AddTaskCompletion(AdministrativeTaskCompletion completion);

    Task<AdministrativeContract?> FindContractAsync(Guid contractId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdministrativeContractSummary>> ListContractsAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, Guid? responsibleUserId, string? status, DateOnly today, CancellationToken cancellationToken);
    void AddContract(AdministrativeContract contract);

    Task<IReadOnlyList<AdministrativeReminderCandidate>> BuildReminderCandidatesAsync(DateOnly today, int vehicleDateHorizonDays, int taskDefaultHorizonDays, int maintenanceKmThreshold, CancellationToken cancellationToken);
    Task<bool> TryInsertReminderAsync(AdministrativeReminderCandidate candidate, DateTimeOffset createdAt, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdministrativeReminderSummary>> ListRemindersAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, string? eventType, DateTimeOffset? from, int take, CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
