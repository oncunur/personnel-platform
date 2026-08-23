using PersonnelPlatform.Domain.Migration;

namespace PersonnelPlatform.Application.Migration;

public interface IMigrationRepository
{
    Task<MigrationRun?> FindRunAsync(Guid runId, CancellationToken ct);
    Task<IReadOnlyList<MigrationRun>> ListRunsAsync(Guid? companyId, IReadOnlyCollection<Guid> allowedCompanyIds, bool global, int take, CancellationToken ct);
    Task<IReadOnlyList<MigrationStageRow>> ListRowsAsync(Guid runId, string? status, int take, CancellationToken ct);
    Task<IReadOnlyList<MigrationLineageRecord>> ListLineagesAsync(Guid companyId, string sourceSystem, string sourceObject, string targetEntity, CancellationToken ct);
    Task<IReadOnlyList<MigrationReconciliation>> ListReconciliationsAsync(Guid runId, CancellationToken ct);
    void AddRun(MigrationRun run);
    void AddRows(IEnumerable<MigrationStageRow> rows);
    void AddLineage(MigrationLineageRecord lineage);
    void AddReconciliations(IEnumerable<MigrationReconciliation> rows);
    Task SaveChangesAsync(CancellationToken ct);
}