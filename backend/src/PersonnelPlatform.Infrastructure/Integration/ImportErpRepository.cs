using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Integration;
using PersonnelPlatform.Domain.Finance;
using PersonnelPlatform.Domain.Integration;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Integration;

public sealed class ImportErpRepository(ApplicationDbContext db) : IImportErpRepository
{
    public Task<ImportJob?> FindImportJobAsync(Guid jobId, CancellationToken ct) =>
        db.ImportJobs.FirstOrDefaultAsync(x => x.Id == jobId && x.DeletedAt == null, ct);

    public async Task<IReadOnlyList<ImportJob>> ListImportJobsAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, int take, CancellationToken ct)
    {
        var query = db.ImportJobs.AsNoTracking().Where(x => x.DeletedAt == null);
        if (!globalAccess) query = query.Where(x => companyIds.Contains(x.CompanyId));
        if (companyId is not null) query = query.Where(x => x.CompanyId == companyId.Value);
        return await query.OrderByDescending(x => x.CreatedAt).Take(take).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ImportRow>> ListImportRowsAsync(Guid jobId, bool errorsOnly, int take, CancellationToken ct)
    {
        var query = db.ImportRows.Where(x => x.ImportJobId == jobId);
        if (errorsOnly) query = query.Where(x => x.Status == ImportRowStatuses.Error);
        return await query.OrderBy(x => x.RowNumber).Take(take).ToListAsync(ct);
    }

    public void AddImportJob(ImportJob job) => db.ImportJobs.Add(job);
    public void AddImportRows(IEnumerable<ImportRow> rows) => db.ImportRows.AddRange(rows);

    public async Task<bool> TryInsertExternalMappingAsync(ExternalEntityMapping x, CancellationToken ct)
    {
        var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO integration.entity_mappings
                (id, company_id, integration_system_id, entity_type, external_code, internal_entity_id, is_active,
                 created_at, created_by, updated_at, updated_by, deleted_at, deleted_by, version)
            VALUES ({x.Id}, {x.CompanyId}, {x.IntegrationSystemId}, {x.EntityType}, {x.ExternalCode}, {x.InternalEntityId}, {x.IsActive},
                    {x.CreatedAt}, {x.CreatedBy}, NULL, NULL, NULL, NULL, {x.Version})
            ON CONFLICT (integration_system_id, entity_type, external_code) WHERE deleted_at IS NULL DO NOTHING
            """, ct);
        return affected > 0;
    }

    public async Task<IReadOnlyList<ErpAccountMapping>> ListErpAccountMappingsAsync(Guid systemId, CancellationToken ct) =>
        await db.ErpAccountMappings.AsNoTracking().Where(x => x.IntegrationSystemId == systemId && x.DeletedAt == null).OrderBy(x => x.CostCategory).ToListAsync(ct);

    public Task<ErpAccountMapping?> FindErpAccountMappingAsync(Guid mappingId, CancellationToken ct) =>
        db.ErpAccountMappings.FirstOrDefaultAsync(x => x.Id == mappingId && x.DeletedAt == null, ct);

    public Task<ErpAccountMapping?> FindActiveErpAccountMappingAsync(Guid systemId, string costCategory, CancellationToken ct) =>
        db.ErpAccountMappings.AsNoTracking().FirstOrDefaultAsync(x => x.IntegrationSystemId == systemId && x.CostCategory == costCategory && x.IsActive && x.DeletedAt == null, ct);

    public Task<bool> ErpAccountMappingExistsAsync(Guid systemId, string costCategory, CancellationToken ct) =>
        db.ErpAccountMappings.AsNoTracking().AnyAsync(x => x.IntegrationSystemId == systemId && x.CostCategory == costCategory && x.DeletedAt == null, ct);

    public async Task<bool> TryInsertErpAccountMappingAsync(ErpAccountMapping x, CancellationToken ct)
    {
        var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO integration.erp_account_mappings
                (id, company_id, integration_system_id, cost_category, account_code, counter_account_code, is_active,
                 created_at, created_by, updated_at, updated_by, deleted_at, deleted_by, version)
            VALUES ({x.Id}, {x.CompanyId}, {x.IntegrationSystemId}, {x.CostCategory}, {x.AccountCode}, {x.CounterAccountCode}, {x.IsActive},
                    {x.CreatedAt}, {x.CreatedBy}, NULL, NULL, NULL, NULL, {x.Version})
            ON CONFLICT (integration_system_id, cost_category) WHERE deleted_at IS NULL DO NOTHING
            """, ct);
        return affected > 0;
    }

    public async Task<IReadOnlyList<CostEntry>> ListEligibleCostEntriesAsync(Guid companyId, Guid systemId, DateOnly fromDate, DateOnly toDate, CancellationToken ct) =>
        await db.CostEntries.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.CostDate >= fromDate && x.CostDate <= toDate)
            .Where(x => !db.ErpExportLines.Any(line => line.CostEntryId == x.Id && db.ErpExportBatches.Any(batch => batch.Id == line.BatchId && batch.IntegrationSystemId == systemId && batch.Status != ErpBatchStatuses.Rejected && batch.DeletedAt == null)))
            .OrderBy(x => x.CostDate).ThenBy(x => x.Id)
            .Take(100_000)
            .ToListAsync(ct);

    public void AddErpBatch(ErpExportBatch batch) => db.ErpExportBatches.Add(batch);
    public void AddErpLines(IEnumerable<ErpExportLine> lines) => db.ErpExportLines.AddRange(lines);

    public Task<ErpExportBatch?> FindErpBatchAsync(Guid batchId, CancellationToken ct) =>
        db.ErpExportBatches.FirstOrDefaultAsync(x => x.Id == batchId && x.DeletedAt == null, ct);

    public async Task<IReadOnlyList<ErpExportBatch>> ListErpBatchesAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, Guid? systemId, int take, CancellationToken ct)
    {
        var query = db.ErpExportBatches.AsNoTracking().Where(x => x.DeletedAt == null);
        if (!globalAccess) query = query.Where(x => companyIds.Contains(x.CompanyId));
        if (companyId is not null) query = query.Where(x => x.CompanyId == companyId.Value);
        if (systemId is not null) query = query.Where(x => x.IntegrationSystemId == systemId.Value);
        return await query.OrderByDescending(x => x.CreatedAt).Take(take).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ErpExportLine>> ListErpLinesAsync(Guid batchId, CancellationToken ct) =>
        await db.ErpExportLines.Where(x => x.BatchId == batchId).OrderBy(x => x.CostDate).ThenBy(x => x.ExternalLineKey).ToListAsync(ct);

    public void AddReconciliationEvent(ErpReconciliationEvent row) => db.ErpReconciliationEvents.Add(row);

    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
