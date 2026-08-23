using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Migration;
using PersonnelPlatform.Domain.Migration;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Migration;

public sealed class MigrationRepository(ApplicationDbContext db) : IMigrationRepository
{
    public Task<MigrationRun?> FindRunAsync(Guid runId, CancellationToken ct) => db.MigrationRuns.SingleOrDefaultAsync(x => x.Id == runId, ct);

    public async Task<IReadOnlyList<MigrationRun>> ListRunsAsync(Guid? companyId, IReadOnlyCollection<Guid> allowedCompanyIds, bool global, int take, CancellationToken ct)
    {
        var query = db.MigrationRuns.AsNoTracking();
        if (companyId is not null) query = query.Where(x => x.CompanyId == companyId.Value);
        else if (!global) query = query.Where(x => allowedCompanyIds.Contains(x.CompanyId));
        return await query.OrderByDescending(x => x.CreatedAt).Take(take).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MigrationStageRow>> ListRowsAsync(Guid runId, string? status, int take, CancellationToken ct)
    {
        var query = db.MigrationStageRows.AsNoTracking().Where(x => x.MigrationRunId == runId);
        if (status is not null) query = query.Where(x => x.Status == status);
        return await query.OrderBy(x => x.RowNumber).Take(take).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MigrationLineageRecord>> ListLineagesAsync(Guid companyId, string sourceSystem, string sourceObject, string targetEntity, CancellationToken ct) =>
        await db.MigrationLineageRecords.Where(x => x.CompanyId == companyId && x.SourceSystem == sourceSystem && x.SourceObject == sourceObject && x.TargetEntity == targetEntity).ToListAsync(ct);

    public async Task<IReadOnlyList<MigrationReconciliation>> ListReconciliationsAsync(Guid runId, CancellationToken ct) =>
        await db.MigrationReconciliations.AsNoTracking().Where(x => x.MigrationRunId == runId).OrderBy(x => x.MetricCode).ToListAsync(ct);

    public void AddRun(MigrationRun run) => db.MigrationRuns.Add(run);
    public void AddRows(IEnumerable<MigrationStageRow> rows) => db.MigrationStageRows.AddRange(rows);
    public void AddLineage(MigrationLineageRecord lineage) => db.MigrationLineageRecords.Add(lineage);
    public void AddReconciliations(IEnumerable<MigrationReconciliation> rows) => db.MigrationReconciliations.AddRange(rows);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}