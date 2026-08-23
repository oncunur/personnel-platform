using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Migration;

public static class MigrationRunStatuses
{
    public const string Created = "CREATED";
    public const string Staged = "STAGED";
    public const string Validated = "VALIDATED";
    public const string Reconciled = "RECONCILED";
    public const string Blocked = "BLOCKED";
    public static bool IsKnown(string value) => value is Created or Staged or Validated or Reconciled or Blocked;
}

public static class MigrationStageRowStatuses
{
    public const string Valid = "VALID";
    public const string Warning = "WARNING";
    public const string Error = "ERROR";
    public const string Duplicate = "DUPLICATE";
    public static bool IsKnown(string value) => value is Valid or Warning or Error or Duplicate;
}

public static class MigrationIdempotenceStatuses
{
    public const string New = "NEW";
    public const string Changed = "CHANGED";
    public const string Unchanged = "UNCHANGED";
    public static bool IsKnown(string value) => value is New or Changed or Unchanged;
}

public static class MigrationReconciliationStatuses
{
    public const string Match = "MATCH";
    public const string Mismatch = "MISMATCH";
}

public sealed class MigrationRun : AuditableEntity
{
    private MigrationRun() { }

    public Guid CompanyId { get; private set; }
    public string SourceSystem { get; private set; } = string.Empty;
    public string SourceObject { get; private set; } = string.Empty;
    public string TargetEntity { get; private set; } = string.Empty;
    public string SourceFileName { get; private set; } = string.Empty;
    public string SourceContentHash { get; private set; } = string.Empty;
    public string MappingHash { get; private set; } = string.Empty;
    public string Status { get; private set; } = MigrationRunStatuses.Created;
    public int TotalRows { get; private set; }
    public int ValidRows { get; private set; }
    public int WarningRows { get; private set; }
    public int ErrorRows { get; private set; }
    public int DuplicateRows { get; private set; }
    public int ReconciliationMismatchCount { get; private set; }

    public static MigrationRun Create(Guid companyId, string sourceSystem, string sourceObject, string targetEntity, string sourceFileName, string sourceContentHash, string mappingHash, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Company and actor are required.");
        return new MigrationRun
        {
            CompanyId = companyId,
            SourceSystem = Required(sourceSystem, 100),
            SourceObject = Required(sourceObject, 150),
            TargetEntity = Required(targetEntity, 150),
            SourceFileName = Required(sourceFileName, 260),
            SourceContentHash = Hash(sourceContentHash),
            MappingHash = Hash(mappingHash),
            Status = MigrationRunStatuses.Created,
            CreatedAt = now.ToUniversalTime(),
            CreatedBy = actorUserId
        };
    }

    public void CompleteStaging(int totalRows, int validRows, int warningRows, int errorRows, int duplicateRows, DateTimeOffset now, Guid actorUserId)
    {
        if (Status != MigrationRunStatuses.Created) throw new InvalidOperationException("Migration run must be CREATED before staging completes.");
        if (totalRows < 0 || validRows < 0 || warningRows < 0 || errorRows < 0 || duplicateRows < 0 || validRows + warningRows + errorRows + duplicateRows != totalRows)
            throw new ArgumentOutOfRangeException(nameof(totalRows));
        TotalRows = totalRows;
        ValidRows = validRows;
        WarningRows = warningRows;
        ErrorRows = errorRows;
        DuplicateRows = duplicateRows;
        Status = MigrationRunStatuses.Staged;
        Touch(now, actorUserId);
    }

    public void CompleteValidation(DateTimeOffset now, Guid actorUserId)
    {
        if (Status != MigrationRunStatuses.Staged) throw new InvalidOperationException("Migration run must be STAGED before validation completes.");
        Status = ErrorRows > 0 ? MigrationRunStatuses.Blocked : MigrationRunStatuses.Validated;
        Touch(now, actorUserId);
    }

    public void CompleteReconciliation(int mismatchCount, DateTimeOffset now, Guid actorUserId)
    {
        if (Status is not (MigrationRunStatuses.Validated or MigrationRunStatuses.Blocked)) throw new InvalidOperationException("Migration run must be VALIDATED or BLOCKED before reconciliation.");
        if (mismatchCount < 0) throw new ArgumentOutOfRangeException(nameof(mismatchCount));
        ReconciliationMismatchCount = mismatchCount;
        Status = ErrorRows > 0 || mismatchCount > 0 ? MigrationRunStatuses.Blocked : MigrationRunStatuses.Reconciled;
        Touch(now, actorUserId);
    }

    private void Touch(DateTimeOffset now, Guid actorUserId)
    {
        if (actorUserId == Guid.Empty) throw new ArgumentException("Actor is required.", nameof(actorUserId));
        UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
    }

    internal static string Required(string value, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength) throw new ArgumentOutOfRangeException(nameof(value));
        return trimmed;
    }

    internal static string Hash(string value)
    {
        var normalized = Required(value, 64).ToUpperInvariant();
        if (normalized.Length != 64 || normalized.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("SHA-256 hash must be 64 hexadecimal characters.", nameof(value));
        return normalized;
    }
}

public sealed class MigrationStageRow : Entity
{
    private MigrationStageRow() { }
    public Guid MigrationRunId { get; private set; }
    public int RowNumber { get; private set; }
    public string SourceKey { get; private set; } = string.Empty;
    public string LineageKey { get; private set; } = string.Empty;
    public string SourceRowHash { get; private set; } = string.Empty;
    public string SourcePayloadCiphertext { get; private set; } = string.Empty;
    public string TransformedPayloadCiphertext { get; private set; } = string.Empty;
    public string Status { get; private set; } = MigrationStageRowStatuses.Valid;
    public string IdempotenceStatus { get; private set; } = MigrationIdempotenceStatuses.New;
    public string? WarningCode { get; private set; }
    public string? WarningMessage { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Guid? PreviousRunId { get; private set; }
    public Guid? PreviousStageRowId { get; private set; }
    public DateTimeOffset StagedAt { get; private set; }

    public static MigrationStageRow Create(Guid runId, int rowNumber, string sourceKey, string lineageKey, string sourceRowHash, string sourcePayloadCiphertext, string transformedPayloadCiphertext, string idempotenceStatus, Guid? previousRunId, Guid? previousStageRowId, string? warningCode, string? warningMessage, string? errorCode, string? errorMessage, DateTimeOffset now)
    {
        if (runId == Guid.Empty || rowNumber < 1) throw new ArgumentException("Run and positive row number are required.");
        if (!MigrationIdempotenceStatuses.IsKnown(idempotenceStatus)) throw new ArgumentException("Unknown idempotence status.", nameof(idempotenceStatus));
        var status = !string.IsNullOrWhiteSpace(errorCode) ? MigrationStageRowStatuses.Error
            : idempotenceStatus == MigrationIdempotenceStatuses.Unchanged ? MigrationStageRowStatuses.Duplicate
            : !string.IsNullOrWhiteSpace(warningCode) ? MigrationStageRowStatuses.Warning
            : MigrationStageRowStatuses.Valid;
        return new MigrationStageRow
        {
            MigrationRunId = runId,
            RowNumber = rowNumber,
            SourceKey = MigrationRun.Required(sourceKey, 240),
            LineageKey = MigrationRun.Required(lineageKey, 500),
            SourceRowHash = MigrationRun.Hash(sourceRowHash),
            SourcePayloadCiphertext = MigrationRun.Required(sourcePayloadCiphertext, 100000),
            TransformedPayloadCiphertext = MigrationRun.Required(transformedPayloadCiphertext, 100000),
            Status = status,
            IdempotenceStatus = idempotenceStatus,
            PreviousRunId = previousRunId,
            PreviousStageRowId = previousStageRowId,
            WarningCode = Optional(warningCode, 120),
            WarningMessage = Optional(warningMessage, 2000),
            ErrorCode = Optional(errorCode, 120),
            ErrorMessage = Optional(errorMessage, 2000),
            StagedAt = now.ToUniversalTime()
        };
    }

    private static string? Optional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength) throw new ArgumentOutOfRangeException(nameof(value));
        return trimmed;
    }
}

public sealed class MigrationLineageRecord : AuditableEntity
{
    private MigrationLineageRecord() { }
    public Guid CompanyId { get; private set; }
    public string SourceSystem { get; private set; } = string.Empty;
    public string SourceObject { get; private set; } = string.Empty;
    public string SourceKey { get; private set; } = string.Empty;
    public string TargetEntity { get; private set; } = string.Empty;
    public string LastSourceRowHash { get; private set; } = string.Empty;
    public Guid LastRunId { get; private set; }
    public Guid LastStageRowId { get; private set; }
    public Guid? TargetEntityId { get; private set; }
    public int SeenCount { get; private set; }

    public static MigrationLineageRecord Create(Guid companyId, string sourceSystem, string sourceObject, string sourceKey, string targetEntity, string sourceRowHash, Guid runId, Guid stageRowId, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || runId == Guid.Empty || stageRowId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Migration lineage context is required.");
        return new MigrationLineageRecord
        {
            CompanyId = companyId,
            SourceSystem = MigrationRun.Required(sourceSystem, 100),
            SourceObject = MigrationRun.Required(sourceObject, 150),
            SourceKey = MigrationRun.Required(sourceKey, 240),
            TargetEntity = MigrationRun.Required(targetEntity, 150),
            LastSourceRowHash = MigrationRun.Hash(sourceRowHash),
            LastRunId = runId,
            LastStageRowId = stageRowId,
            SeenCount = 1,
            CreatedAt = now.ToUniversalTime(), CreatedBy = actorUserId
        };
    }

    public string Classify(string sourceRowHash) => LastSourceRowHash == MigrationRun.Hash(sourceRowHash) ? MigrationIdempotenceStatuses.Unchanged : MigrationIdempotenceStatuses.Changed;

    public void Observe(string sourceRowHash, Guid runId, Guid stageRowId, DateTimeOffset now, Guid actorUserId)
    {
        LastSourceRowHash = MigrationRun.Hash(sourceRowHash);
        LastRunId = runId;
        LastStageRowId = stageRowId;
        SeenCount++;
        UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
    }

    public void BindTarget(Guid targetEntityId, DateTimeOffset now, Guid actorUserId)
    {
        if (targetEntityId == Guid.Empty) throw new ArgumentException("Target entity is required.", nameof(targetEntityId));
        TargetEntityId = targetEntityId;
        UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
    }
}

public sealed class MigrationReconciliation : Entity
{
    private MigrationReconciliation() { }
    public Guid MigrationRunId { get; private set; }
    public string MetricCode { get; private set; } = string.Empty;
    public string MetricName { get; private set; } = string.Empty;
    public decimal SourceValue { get; private set; }
    public decimal TargetValue { get; private set; }
    public decimal Difference { get; private set; }
    public decimal Tolerance { get; private set; }
    public string Status { get; private set; } = MigrationReconciliationStatuses.Match;
    public string? Notes { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public Guid RecordedBy { get; private set; }

    public static MigrationReconciliation Create(Guid runId, string metricCode, string metricName, decimal sourceValue, decimal targetValue, decimal tolerance, string? notes, DateTimeOffset now, Guid actorUserId)
    {
        if (runId == Guid.Empty || actorUserId == Guid.Empty || tolerance < 0) throw new ArgumentException("Reconciliation context and non-negative tolerance are required.");
        var difference = decimal.Abs(targetValue - sourceValue);
        return new MigrationReconciliation
        {
            MigrationRunId = runId,
            MetricCode = MigrationRun.Required(metricCode, 100).ToUpperInvariant(),
            MetricName = MigrationRun.Required(metricName, 200),
            SourceValue = sourceValue,
            TargetValue = targetValue,
            Difference = difference,
            Tolerance = tolerance,
            Status = difference <= tolerance ? MigrationReconciliationStatuses.Match : MigrationReconciliationStatuses.Mismatch,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : MigrationRun.Required(notes, 2000),
            RecordedAt = now.ToUniversalTime(), RecordedBy = actorUserId
        };
    }
}