using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Reporting;

public static class ReportExportStatuses
{
    public const string Queued = "QUEUED";
    public const string Processing = "PROCESSING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
    public static bool IsTerminal(string value) => value is Completed or Failed;
}

public static class ReportExportFormats
{
    public const string Xlsx = "XLSX";
    public const string Pdf = "PDF";
    public static bool IsKnown(string value) => value is Xlsx or Pdf;
}

public static class ReportTypes
{
    public const string CostLedger = "COST_LEDGER";
    public const string Project360 = "PROJECT_360";
    public const string Management = "MANAGEMENT";
    public static bool IsKnown(string value) => value is CostLedger or Project360 or Management;
}

public sealed class ReportExportJob : AuditableEntity
{
    private ReportExportJob() { }

    public Guid CompanyId { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public string ReportType { get; private set; } = string.Empty;
    public string Format { get; private set; } = ReportExportFormats.Xlsx;
    public string FiltersJson { get; private set; } = "{}";
    public string Status { get; private set; } = ReportExportStatuses.Queued;
    public string? StorageKey { get; private set; }
    public string? FileName { get; private set; }
    public string? ContentType { get; private set; }
    public long? FileSizeBytes { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? ErrorMessage { get; private set; }

    public static ReportExportJob Create(Guid companyId, Guid requestedByUserId, string reportType, string format, string filtersJson, DateTimeOffset now)
    {
        if (companyId == Guid.Empty || requestedByUserId == Guid.Empty) throw new ArgumentException("Company and requester are required.");
        var type = Required(reportType, 50).ToUpperInvariant();
        var normalizedFormat = Required(format, 10).ToUpperInvariant();
        if (!ReportTypes.IsKnown(type)) throw new ArgumentException("Report type is invalid.", nameof(reportType));
        if (!ReportExportFormats.IsKnown(normalizedFormat)) throw new ArgumentException("Report export format is invalid.", nameof(format));
        return new ReportExportJob
        {
            CompanyId = companyId,
            RequestedByUserId = requestedByUserId,
            ReportType = type,
            Format = normalizedFormat,
            FiltersJson = Required(filtersJson, 20_000),
            Status = ReportExportStatuses.Queued,
            CreatedAt = now.ToUniversalTime(),
            CreatedBy = requestedByUserId
        };
    }

    public void Start(DateTimeOffset now)
    {
        if (Status != ReportExportStatuses.Queued) throw new InvalidOperationException("Only queued export jobs can start.");
        Status = ReportExportStatuses.Processing;
        StartedAt = now.ToUniversalTime();
        UpdatedAt = now.ToUniversalTime();
        Version++;
    }

    public void Complete(string storageKey, string fileName, string contentType, long fileSizeBytes, DateTimeOffset now)
    {
        if (Status != ReportExportStatuses.Processing) throw new InvalidOperationException("Only processing export jobs can complete.");
        if (fileSizeBytes <= 0) throw new ArgumentOutOfRangeException(nameof(fileSizeBytes));
        StorageKey = Required(storageKey, 500);
        FileName = Required(fileName, 240);
        ContentType = Required(contentType, 150);
        FileSizeBytes = fileSizeBytes;
        Status = ReportExportStatuses.Completed;
        CompletedAt = now.ToUniversalTime();
        ErrorMessage = null;
        UpdatedAt = now.ToUniversalTime();
        Version++;
    }

    public void Fail(string errorMessage, DateTimeOffset now)
    {
        if (ReportExportStatuses.IsTerminal(Status)) throw new InvalidOperationException("Terminal export job cannot fail again.");
        Status = ReportExportStatuses.Failed;
        ErrorMessage = Required(errorMessage, 2000);
        CompletedAt = now.ToUniversalTime();
        UpdatedAt = now.ToUniversalTime();
        Version++;
    }

    private static string Required(string value, int max)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > max) throw new ArgumentException("Text is too long.");
        return normalized;
    }
}
