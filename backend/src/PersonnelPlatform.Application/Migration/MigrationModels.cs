namespace PersonnelPlatform.Application.Migration;

public static class MigrationPermissions
{
    public const string View = "migration.view";
    public const string Manage = "migration.manage";
    public const string Reconcile = "migration.reconcile";
}

public sealed record CreateMigrationRunRequest(Guid CompanyId, string SourceSystem, string SourceObject, string TargetEntity, string SourceFileName, string SourceContentHash, string MappingHash);

public sealed record StageMigrationRowInput(
    int RowNumber,
    string SourceKey,
    string SourceRowHash,
    string SourcePayloadJson,
    string TransformedPayloadJson,
    string? WarningCode,
    string? WarningMessage,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record StageMigrationRowsRequest(int Version, IReadOnlyList<StageMigrationRowInput> Rows);
public sealed record ValidateMigrationRunRequest(int Version);
public sealed record MigrationReconciliationInput(string MetricCode, string MetricName, decimal SourceValue, decimal TargetValue, decimal Tolerance, string? Notes);
public sealed record ReconcileMigrationRunRequest(int Version, IReadOnlyList<MigrationReconciliationInput> Metrics);

public sealed record MigrationRunSummary(
    Guid Id, Guid CompanyId, string SourceSystem, string SourceObject, string TargetEntity, string SourceFileName,
    string SourceContentHash, string MappingHash, string Status, int TotalRows, int ValidRows, int WarningRows,
    int ErrorRows, int DuplicateRows, int ReconciliationMismatchCount, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, int Version);

public sealed record MigrationStageRowSummary(
    Guid Id, int RowNumber, string SourceKey, string LineageKey, string SourceRowHash, string Status, string IdempotenceStatus,
    string? WarningCode, string? WarningMessage, string? ErrorCode, string? ErrorMessage, Guid? PreviousRunId, Guid? PreviousStageRowId, DateTimeOffset StagedAt);

public sealed record MigrationStageSummary(MigrationRunSummary Run, int NewRows, int ChangedRows, int UnchangedRows);
public sealed record MigrationValidationSummary(MigrationRunSummary Run, bool CanProceed, int BlockingErrors, int Warnings, int Duplicates);
public sealed record MigrationReconciliationSummary(Guid Id, string MetricCode, string MetricName, decimal SourceValue, decimal TargetValue, decimal Difference, decimal Tolerance, string Status, string? Notes, DateTimeOffset RecordedAt);
public sealed record MigrationReconcileSummary(MigrationRunSummary Run, IReadOnlyList<MigrationReconciliationSummary> Metrics, int MismatchCount);

public sealed record MigrationResult<T>(bool Succeeded, T? Value, string? ErrorCode, string? ErrorMessage) where T : class
{
    public static MigrationResult<T> Success(T value) => new(true, value, null, null);
    public static MigrationResult<T> Failure(string code, string message) => new(false, null, code, message);
}