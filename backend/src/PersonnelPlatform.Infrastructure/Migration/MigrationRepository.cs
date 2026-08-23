using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Migration;
using PersonnelPlatform.Domain.Migration;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Migration;

public sealed class MigrationRepository(ApplicationDbContext db) : IMigrationRepository
{
    private DbSet<MigrationRun> Runs => db.Set<MigrationRun>();
    private DbSet<MigrationStageRow> StageRows => db.Set<MigrationStageRow>();
    private DbSet<MigrationLineageRecord> Lineages => db.Set<MigrationLineageRecord>();
    private DbSet<MigrationReconciliation> Reconciliations => db.Set<MigrationReconciliation>();

    public Task<MigrationRun?> FindRunAsync(Guid runId, CancellationToken ct) => Runs.SingleOrDefaultAsync(x => x.Id == runId, ct);

    public async Task<IReadOnlyList<MigrationRun>> ListRunsAsync(Guid? companyId, IReadOnlyCollection<Guid> allowedCompanyIds, bool global, int take, CancellationToken ct)
    {
        var query = Runs.AsNoTracking();
        if (companyId is not null) query = query.Where(x => x.CompanyId == companyId.Value);
        else if (!global) query = query.Where(x => allowedCompanyIds.Contains(x.CompanyId));
        return await query.OrderByDescending(x => x.CreatedAt).Take(take).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MigrationStageRow>> ListRowsAsync(Guid runId, string? status, int take, CancellationToken ct)
    {
        var query = StageRows.AsNoTracking().Where(x => x.MigrationRunId == runId);
        if (status is not null) query = query.Where(x => x.Status == status);
        return await query.OrderBy(x => x.RowNumber).Take(take).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MigrationLineageRecord>> ListLineagesAsync(Guid companyId, string sourceSystem, string sourceObject, string targetEntity, CancellationToken ct) =>
        await Lineages.Where(x => x.CompanyId == companyId && x.SourceSystem == sourceSystem && x.SourceObject == sourceObject && x.TargetEntity == targetEntity).ToListAsync(ct);

    public async Task<IReadOnlyList<MigrationReconciliation>> ListReconciliationsAsync(Guid runId, CancellationToken ct) =>
        await Reconciliations.AsNoTracking().Where(x => x.MigrationRunId == runId).OrderBy(x => x.MetricCode).ToListAsync(ct);

    public void AddRun(MigrationRun run) => Runs.Add(run);
    public void AddRows(IEnumerable<MigrationStageRow> rows) => StageRows.AddRange(rows);
    public void AddLineage(MigrationLineageRecord lineage) => Lineages.Add(lineage);
    public void AddReconciliations(IEnumerable<MigrationReconciliation> rows) => Reconciliations.AddRange(rows);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}