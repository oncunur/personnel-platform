using PersonnelPlatform.Domain.Finance;
using PersonnelPlatform.Domain.Integration;

namespace PersonnelPlatform.Application.Integration;

public interface IImportErpRepository
{
    Task<ImportJob?> FindImportJobAsync(Guid jobId, CancellationToken ct);
    Task<IReadOnlyList<ImportJob>> ListImportJobsAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, int take, CancellationToken ct);
    Task<IReadOnlyList<ImportRow>> ListImportRowsAsync(Guid jobId, bool errorsOnly, int take, CancellationToken ct);
    void AddImportJob(ImportJob job);
    void AddImportRows(IEnumerable<ImportRow> rows);
    Task<bool> TryInsertExternalMappingAsync(ExternalEntityMapping mapping, CancellationToken ct);

    Task<IReadOnlyList<ErpAccountMapping>> ListErpAccountMappingsAsync(Guid systemId, CancellationToken ct);
    Task<ErpAccountMapping?> FindErpAccountMappingAsync(Guid mappingId, CancellationToken ct);
    Task<ErpAccountMapping?> FindActiveErpAccountMappingAsync(Guid systemId, string costCategory, CancellationToken ct);
    Task<bool> ErpAccountMappingExistsAsync(Guid systemId, string costCategory, CancellationToken ct);
    Task<bool> TryInsertErpAccountMappingAsync(ErpAccountMapping mapping, CancellationToken ct);

    Task<IReadOnlyList<CostEntry>> ListEligibleCostEntriesAsync(Guid companyId, Guid systemId, DateOnly fromDate, DateOnly toDate, CancellationToken ct);
    void AddErpBatch(ErpExportBatch batch);
    void AddErpLines(IEnumerable<ErpExportLine> lines);
    Task<ErpExportBatch?> FindErpBatchAsync(Guid batchId, CancellationToken ct);
    Task<IReadOnlyList<ErpExportBatch>> ListErpBatchesAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, Guid? systemId, int take, CancellationToken ct);
    Task<IReadOnlyList<ErpExportLine>> ListErpLinesAsync(Guid batchId, CancellationToken ct);
    void AddReconciliationEvent(ErpReconciliationEvent row);

    Task<int> SaveChangesAsync(CancellationToken ct);
}
