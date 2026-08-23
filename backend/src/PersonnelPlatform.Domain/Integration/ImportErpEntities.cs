using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Integration;

public static class ImportTargetTypes
{
    public const string IntegrationMapping = "INTEGRATION_MAPPING";
    public const string ErpAccountMapping = "ERP_ACCOUNT_MAPPING";
    public static bool IsKnown(string value) => value is IntegrationMapping or ErpAccountMapping;
}

public static class ImportJobStatuses
{
    public const string Uploaded = "UPLOADED";
    public const string Ready = "READY";
    public const string Processing = "PROCESSING";
    public const string Completed = "COMPLETED";
    public const string Partial = "PARTIAL";
    public const string Failed = "FAILED";
    public static bool IsTerminal(string value) => value is Completed or Partial or Failed;
}

public static class ImportRowStatuses
{
    public const string Preview = "PREVIEW";
    public const string Imported = "IMPORTED";
    public const string Error = "ERROR";
}

public sealed class ImportJob : AuditableEntity
{
    private ImportJob() { }

    public Guid CompanyId { get; private set; }
    public Guid IntegrationSystemId { get; private set; }
    public string TargetType { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string FileHash { get; private set; } = string.Empty;
    public string Status { get; private set; } = ImportJobStatuses.Uploaded;
    public string HeadersJson { get; private set; } = "[]";
    public string MappingJson { get; private set; } = "{}";
    public int TotalRows { get; private set; }
    public int SuccessRows { get; private set; }
    public int ErrorRows { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public static ImportJob Create(Guid companyId, Guid integrationSystemId, string targetType, string originalFileName, string fileHash, string headersJson, int totalRows, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || integrationSystemId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Company, integration system and actor are required.");
        var target = Required(targetType, 50).ToUpperInvariant();
        if (!ImportTargetTypes.IsKnown(target)) throw new ArgumentException("Import target type is invalid.", nameof(targetType));
        if (totalRows < 0) throw new ArgumentOutOfRangeException(nameof(totalRows));
        return new ImportJob
        {
            CompanyId = companyId,
            IntegrationSystemId = integrationSystemId,
            TargetType = target,
            OriginalFileName = Required(originalFileName, 240),
            FileHash = Required(fileHash, 128).ToUpperInvariant(),
            HeadersJson = Required(headersJson, 20_000),
            TotalRows = totalRows,
            Status = ImportJobStatuses.Uploaded,
            CreatedAt = now.ToUniversalTime(),
            CreatedBy = actorUserId
        };
    }

    public void ApplyMapping(string mappingJson, DateTimeOffset now, Guid actorUserId)
    {
        if (Status == ImportJobStatuses.Processing || ImportJobStatuses.IsTerminal(Status)) throw new InvalidOperationException("Import mapping can no longer be changed.");
        MappingJson = Required(mappingJson, 20_000);
        Status = ImportJobStatuses.Ready;
        Touch(now, actorUserId);
    }

    public void Begin(DateTimeOffset now, Guid actorUserId)
    {
        if (Status != ImportJobStatuses.Ready) throw new InvalidOperationException("Import job is not ready for processing.");
        Status = ImportJobStatuses.Processing;
        SuccessRows = 0;
        ErrorRows = 0;
        CompletedAt = null;
        Touch(now, actorUserId);
    }

    public void Finish(int successRows, int errorRows, DateTimeOffset now, Guid actorUserId)
    {
        if (Status != ImportJobStatuses.Processing) throw new InvalidOperationException("Only processing import can finish.");
        if (successRows < 0 || errorRows < 0 || successRows + errorRows != TotalRows) throw new ArgumentOutOfRangeException(nameof(successRows));
        SuccessRows = successRows;
        ErrorRows = errorRows;
        Status = errorRows == 0 ? ImportJobStatuses.Completed : successRows == 0 ? ImportJobStatuses.Failed : ImportJobStatuses.Partial;
        CompletedAt = now.ToUniversalTime();
        Touch(now, actorUserId);
    }

    private void Touch(DateTimeOffset now, Guid actorUserId) { UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++; }
    private static string Required(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Text is too long."); return v; }
}

public sealed class ImportRow : Entity
{
    private ImportRow() { }

    public Guid ImportJobId { get; private set; }
    public int RowNumber { get; private set; }
    public string RawJson { get; private set; } = "{}";
    public string Status { get; private set; } = ImportRowStatuses.Preview;
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? ProcessedEntityType { get; private set; }
    public Guid? ProcessedEntityId { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    public static ImportRow Create(Guid importJobId, int rowNumber, string rawJson)
    {
        if (importJobId == Guid.Empty || rowNumber < 2) throw new ArgumentException("Import job and spreadsheet row number are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(rawJson);
        if (rawJson.Length > 100_000) throw new ArgumentException("Import row payload is too large.", nameof(rawJson));
        return new ImportRow { ImportJobId = importJobId, RowNumber = rowNumber, RawJson = rawJson, Status = ImportRowStatuses.Preview };
    }

    public void MarkImported(string entityType, Guid entityId, DateTimeOffset now)
    {
        if (Status != ImportRowStatuses.Preview) throw new InvalidOperationException("Import row has already been processed.");
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        if (entityId == Guid.Empty) throw new ArgumentException("Processed entity id is required.");
        Status = ImportRowStatuses.Imported;
        ErrorCode = null;
        ErrorMessage = null;
        ProcessedEntityType = entityType.Trim().ToUpperInvariant();
        ProcessedEntityId = entityId;
        ProcessedAt = now.ToUniversalTime();
    }

    public void MarkError(string code, string message, DateTimeOffset now)
    {
        if (Status != ImportRowStatuses.Preview) throw new InvalidOperationException("Import row has already been processed.");
        Status = ImportRowStatuses.Error;
        ErrorCode = Required(code, 120).ToUpperInvariant();
        ErrorMessage = Required(message, 2000);
        ProcessedAt = now.ToUniversalTime();
    }

    private static string Required(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Text is too long."); return v; }
}

public sealed class ErpAccountMapping : AuditableEntity
{
    private ErpAccountMapping() { }

    public Guid CompanyId { get; private set; }
    public Guid IntegrationSystemId { get; private set; }
    public string CostCategory { get; private set; } = string.Empty;
    public string AccountCode { get; private set; } = string.Empty;
    public string? CounterAccountCode { get; private set; }
    public bool IsActive { get; private set; }

    public static ErpAccountMapping Create(Guid companyId, Guid systemId, string costCategory, string accountCode, string? counterAccountCode, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || systemId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Company, system and actor are required.");
        return new ErpAccountMapping
        {
            CompanyId = companyId,
            IntegrationSystemId = systemId,
            CostCategory = Required(costCategory, 50).ToUpperInvariant(),
            AccountCode = Required(accountCode, 100).ToUpperInvariant(),
            CounterAccountCode = Optional(counterAccountCode, 100)?.ToUpperInvariant(),
            IsActive = true,
            CreatedAt = now.ToUniversalTime(),
            CreatedBy = actorUserId
        };
    }

    public void Change(string accountCode, string? counterAccountCode, bool isActive, DateTimeOffset now, Guid actorUserId)
    {
        AccountCode = Required(accountCode, 100).ToUpperInvariant();
        CounterAccountCode = Optional(counterAccountCode, 100)?.ToUpperInvariant();
        IsActive = isActive;
        UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
    }

    private static string Required(string value, int max) => Optional(value, max) ?? throw new ArgumentException("Value is required.");
    private static string? Optional(string? value, int max) { if (string.IsNullOrWhiteSpace(value)) return null; var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Text is too long."); return v; }
}

public static class ErpBatchStatuses
{
    public const string Draft = "DRAFT";
    public const string Sent = "SENT";
    public const string PartiallyAccepted = "PARTIALLY_ACCEPTED";
    public const string Accepted = "ACCEPTED";
    public const string Rejected = "REJECTED";
    public const string Closed = "CLOSED";
    public static bool IsKnown(string value) => value is Draft or Sent or PartiallyAccepted or Accepted or Rejected or Closed;
}

public static class ErpLineStatuses
{
    public const string Pending = "PENDING";
    public const string Sent = "SENT";
    public const string Accepted = "ACCEPTED";
    public const string Rejected = "REJECTED";
    public static bool IsKnown(string value) => value is Pending or Sent or Accepted or Rejected;
}

public sealed class ErpExportBatch : AuditableEntity
{
    private ErpExportBatch() { }

    public Guid CompanyId { get; private set; }
    public Guid IntegrationSystemId { get; private set; }
    public DateOnly FromDate { get; private set; }
    public DateOnly ToDate { get; private set; }
    public string Status { get; private set; } = ErpBatchStatuses.Draft;
    public DateTimeOffset? SentAt { get; private set; }
    public DateTimeOffset? ReconciledAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public static ErpExportBatch Create(Guid companyId, Guid systemId, DateOnly fromDate, DateOnly toDate, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || systemId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Company, system and actor are required.");
        if (toDate < fromDate) throw new ArgumentException("ERP batch date range is invalid.");
        return new ErpExportBatch { CompanyId = companyId, IntegrationSystemId = systemId, FromDate = fromDate, ToDate = toDate, Status = ErpBatchStatuses.Draft, CreatedAt = now.ToUniversalTime(), CreatedBy = actorUserId };
    }

    public void MarkSent(DateTimeOffset now, Guid actorUserId)
    {
        if (Status != ErpBatchStatuses.Draft) throw new InvalidOperationException("Only draft ERP batch can be sent.");
        Status = ErpBatchStatuses.Sent; SentAt = now.ToUniversalTime(); Touch(now, actorUserId);
    }

    public void MarkReconciled(string status, DateTimeOffset now, Guid actorUserId)
    {
        var normalized = status.Trim().ToUpperInvariant();
        if (normalized is not (ErpBatchStatuses.Accepted or ErpBatchStatuses.PartiallyAccepted or ErpBatchStatuses.Rejected)) throw new ArgumentException("ERP reconciliation status is invalid.", nameof(status));
        if (Status is not (ErpBatchStatuses.Sent or ErpBatchStatuses.PartiallyAccepted or ErpBatchStatuses.Rejected)) throw new InvalidOperationException("ERP batch is not awaiting reconciliation.");
        Status = normalized; ReconciledAt = now.ToUniversalTime(); Touch(now, actorUserId);
    }

    public void Close(DateTimeOffset now, Guid actorUserId)
    {
        if (Status != ErpBatchStatuses.Accepted) throw new InvalidOperationException("ERP batch cannot close before it is fully accepted.");
        Status = ErpBatchStatuses.Closed; ClosedAt = now.ToUniversalTime(); Touch(now, actorUserId);
    }

    private void Touch(DateTimeOffset now, Guid actorUserId) { UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++; }
}

public sealed class ErpExportLine : Entity
{
    private ErpExportLine() { }

    public Guid BatchId { get; private set; }
    public Guid CostEntryId { get; private set; }
    public string ExternalLineKey { get; private set; } = string.Empty;
    public string SourceType { get; private set; } = string.Empty;
    public Guid SourceId { get; private set; }
    public Guid? EmployeeId { get; private set; }
    public Guid? ProjectId { get; private set; }
    public Guid? CostCenterId { get; private set; }
    public DateOnly CostDate { get; private set; }
    public string CostCategory { get; private set; } = string.Empty;
    public string AccountCode { get; private set; } = string.Empty;
    public string? CounterAccountCode { get; private set; }
    public decimal SentAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string Status { get; private set; } = ErpLineStatuses.Pending;
    public decimal? AcceptedAmount { get; private set; }
    public decimal? VarianceAmount { get; private set; }
    public string? ExternalReference { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset? ReconciledAt { get; private set; }

    public static ErpExportLine Create(Guid batchId, Guid costEntryId, string externalLineKey, string sourceType, Guid sourceId, Guid? employeeId, Guid? projectId, Guid? costCenterId, DateOnly costDate, string costCategory, string accountCode, string? counterAccountCode, decimal sentAmount, string currency)
    {
        if (batchId == Guid.Empty || costEntryId == Guid.Empty || sourceId == Guid.Empty) throw new ArgumentException("Batch, cost entry and source are required.");
        if (sentAmount < 0) throw new ArgumentOutOfRangeException(nameof(sentAmount));
        var cur = Required(currency, 3).ToUpperInvariant(); if (cur.Length != 3) throw new ArgumentException("Currency is invalid.");
        return new ErpExportLine
        {
            BatchId = batchId, CostEntryId = costEntryId, ExternalLineKey = Required(externalLineKey, 120), SourceType = Required(sourceType, 40).ToUpperInvariant(), SourceId = sourceId,
            EmployeeId = employeeId, ProjectId = projectId, CostCenterId = costCenterId, CostDate = costDate, CostCategory = Required(costCategory, 50).ToUpperInvariant(),
            AccountCode = Required(accountCode, 100).ToUpperInvariant(), CounterAccountCode = Optional(counterAccountCode, 100)?.ToUpperInvariant(), SentAmount = decimal.Round(sentAmount, 2, MidpointRounding.AwayFromZero), Currency = cur, Status = ErpLineStatuses.Pending
        };
    }

    public void MarkSent()
    {
        if (Status != ErpLineStatuses.Pending) throw new InvalidOperationException("Only pending ERP line can be sent.");
        Status = ErpLineStatuses.Sent;
    }

    public void Reconcile(string status, decimal? acceptedAmount, string? externalReference, string? errorCode, string? errorMessage, DateTimeOffset now)
    {
        if (Status is not (ErpLineStatuses.Sent or ErpLineStatuses.Accepted or ErpLineStatuses.Rejected)) throw new InvalidOperationException("ERP line has not been sent.");
        var normalized = Required(status, 20).ToUpperInvariant();
        if (normalized is not (ErpLineStatuses.Accepted or ErpLineStatuses.Rejected)) throw new ArgumentException("ERP line reconciliation status is invalid.");
        if (acceptedAmount is < 0) throw new ArgumentOutOfRangeException(nameof(acceptedAmount));
        Status = normalized;
        AcceptedAmount = acceptedAmount is null ? null : decimal.Round(acceptedAmount.Value, 2, MidpointRounding.AwayFromZero);
        VarianceAmount = decimal.Round((AcceptedAmount ?? 0m) - SentAmount, 2, MidpointRounding.AwayFromZero);
        ExternalReference = Optional(externalReference, 200);
        ErrorCode = Optional(errorCode, 120)?.ToUpperInvariant();
        ErrorMessage = Optional(errorMessage, 2000);
        ReconciledAt = now.ToUniversalTime();
    }

    private static string Required(string value, int max) => Optional(value, max) ?? throw new ArgumentException("Value is required.");
    private static string? Optional(string? value, int max) { if (string.IsNullOrWhiteSpace(value)) return null; var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Text is too long."); return v; }
}

public sealed class ErpReconciliationEvent : Entity
{
    private ErpReconciliationEvent() { }
    public Guid BatchId { get; private set; }
    public Guid LineId { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public decimal SentAmount { get; private set; }
    public decimal? AcceptedAmount { get; private set; }
    public decimal VarianceAmount { get; private set; }
    public string? ExternalReference { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Guid ActorUserId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    public static ErpReconciliationEvent Create(Guid batchId, Guid lineId, string status, decimal sentAmount, decimal? acceptedAmount, decimal varianceAmount, string? externalReference, string? errorCode, string? errorMessage, Guid actorUserId, DateTimeOffset now)
    {
        if (batchId == Guid.Empty || lineId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Batch, line and actor are required.");
        return new ErpReconciliationEvent
        {
            BatchId = batchId, LineId = lineId, Status = status, SentAmount = sentAmount, AcceptedAmount = acceptedAmount, VarianceAmount = varianceAmount,
            ExternalReference = externalReference, ErrorCode = errorCode, ErrorMessage = errorMessage, ActorUserId = actorUserId, OccurredAt = now.ToUniversalTime()
        };
    }
}
