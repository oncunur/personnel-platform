namespace PersonnelPlatform.Application.Integration;

public static class ImportErpPermissions
{
    public const string ImportView = "integration.import.view";
    public const string ImportManage = "integration.import.manage";
    public const string ErpAccountView = "erp.account.view";
    public const string ErpAccountManage = "erp.account.manage";
    public const string ErpBatchView = "erp.batch.view";
    public const string ErpBatchManage = "erp.batch.manage";
    public const string ErpReconcile = "erp.reconciliation.manage";
}

public sealed record SpreadsheetPreviewRow(int RowNumber, IReadOnlyDictionary<string, string> Values);
public sealed record ImportJobSummary(
    Guid Id,
    Guid CompanyId,
    Guid IntegrationSystemId,
    string TargetType,
    string OriginalFileName,
    string Status,
    IReadOnlyList<string> Headers,
    IReadOnlyDictionary<string, string> Mapping,
    int TotalRows,
    int SuccessRows,
    int ErrorRows,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    int Version);
public sealed record ImportUploadSummary(ImportJobSummary Job, IReadOnlyDictionary<string, string> SuggestedMapping, IReadOnlyList<SpreadsheetPreviewRow> PreviewRows);
public sealed record ImportValidationSummary(ImportJobSummary Job, int ValidRows, int InvalidRows, IReadOnlyList<ImportRowSummary> Errors);
public sealed record ImportProcessSummary(ImportJobSummary Job, int ImportedRows, int ErrorRows);
public sealed record ImportRowSummary(int RowNumber, string Status, string? ErrorCode, string? ErrorMessage, string? ProcessedEntityType, Guid? ProcessedEntityId, IReadOnlyDictionary<string, string> Values);
public sealed record ApplyImportMappingRequest(int Version, IReadOnlyDictionary<string, string> Mapping);
public sealed record ProcessImportRequest(int Version);

public sealed record ErpAccountMappingSummary(Guid Id, Guid CompanyId, Guid IntegrationSystemId, string CostCategory, string AccountCode, string? CounterAccountCode, bool IsActive, int Version);
public sealed record CreateErpAccountMappingRequest(Guid CompanyId, Guid IntegrationSystemId, string CostCategory, string AccountCode, string? CounterAccountCode);
public sealed record UpdateErpAccountMappingRequest(string AccountCode, string? CounterAccountCode, bool IsActive, int Version);

public sealed record ErpCurrencyTotal(string Currency, decimal SentAmount, decimal AcceptedAmount, decimal VarianceAmount);
public sealed record ErpBatchSummary(Guid Id, Guid CompanyId, Guid IntegrationSystemId, DateOnly FromDate, DateOnly ToDate, string Status, int LineCount, int AcceptedLines, int RejectedLines, IReadOnlyList<ErpCurrencyTotal> Totals, DateTimeOffset CreatedAt, DateTimeOffset? SentAt, DateTimeOffset? ReconciledAt, DateTimeOffset? ClosedAt, int Version);
public sealed record ErpBatchLineSummary(Guid Id, Guid BatchId, Guid CostEntryId, string ExternalLineKey, string SourceType, Guid SourceId, Guid? EmployeeId, Guid? ProjectId, Guid? CostCenterId, DateOnly CostDate, string CostCategory, string AccountCode, string? CounterAccountCode, decimal SentAmount, string Currency, string Status, decimal? AcceptedAmount, decimal? VarianceAmount, string? ExternalReference, string? ErrorCode, string? ErrorMessage, DateTimeOffset? ReconciledAt);
public sealed record CreateErpBatchRequest(Guid CompanyId, Guid IntegrationSystemId, DateOnly FromDate, DateOnly ToDate);
public sealed record ErpBatchActionRequest(int Version);
public sealed record ErpLineReconciliationInput(string ExternalLineKey, string Status, decimal? AcceptedAmount, string? ExternalReference, string? ErrorCode, string? ErrorMessage);
public sealed record ReconcileErpBatchRequest(int Version, IReadOnlyList<ErpLineReconciliationInput> Lines);
public sealed record ErpExportFile(byte[] Content, string ContentType, string FileName);

public sealed record SpreadsheetData(IReadOnlyList<string> Headers, IReadOnlyList<SpreadsheetDataRow> Rows);
public sealed record SpreadsheetDataRow(int RowNumber, IReadOnlyDictionary<string, string> Values);
