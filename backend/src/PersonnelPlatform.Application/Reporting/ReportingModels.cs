namespace PersonnelPlatform.Application.Reporting;

public static class ReportingPermissions
{
    public const string View = "reporting.view";
    public const string Export = "reporting.export";
}

public sealed record CurrencyCostSummary(string Currency, decimal PayrollCost, decimal MealCost, decimal AccommodationCost, decimal TotalCost);

public sealed record Project360Summary(
    Guid ProjectId,
    Guid CompanyId,
    string ProjectCode,
    string ProjectName,
    DateOnly From,
    DateOnly To,
    int Headcount,
    int ManDays,
    decimal WorkedHours,
    decimal ApprovedOvertimeHours,
    decimal MealQuantity,
    int AccommodationNights,
    IReadOnlyList<CurrencyCostSummary> Costs);

public sealed record ManagementProjectSummary(
    Guid ProjectId,
    string ProjectCode,
    string ProjectName,
    int Headcount,
    int ManDays,
    decimal WorkedHours,
    decimal ApprovedOvertimeHours,
    decimal MealQuantity,
    int AccommodationNights,
    IReadOnlyList<CurrencyCostSummary> Costs);

public sealed record CreateReportExportRequest(Guid CompanyId, string ReportType, string Format, Guid? ProjectId, DateOnly? From, DateOnly? To);

public sealed record ReportExportJobSummary(
    Guid Id,
    Guid CompanyId,
    Guid RequestedByUserId,
    string ReportType,
    string Format,
    string FiltersJson,
    string Status,
    string? FileName,
    string? ContentType,
    long? FileSizeBytes,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    int Version);

public sealed record ReportExportDownload(Stream Content, string ContentType, string FileName);

public sealed record ReportingResult<T>(bool Succeeded, T? Value, string? ErrorCode, string? ErrorMessage) where T : class
{
    public static ReportingResult<T> Success(T value) => new(true, value, null, null);
    public static ReportingResult<T> Failure(string code, string message) => new(false, null, code, message);
}
