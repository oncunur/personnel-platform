namespace PersonnelPlatform.Application.Documents;

public static class DocumentPermissions
{
    public const string TypeView = "documents.type.view";
    public const string TypeManage = "documents.type.manage";
    public const string EmployeeView = "documents.employee.view";
    public const string EmployeeUpload = "documents.employee.upload";
    public const string EmployeeRenew = "documents.employee.renew";
    public const string EmployeeCancel = "documents.employee.cancel";
    public const string FileView = "documents.file.view";
    public const string MissingView = "documents.missing.view";
    public const string ExpiringView = "documents.expiring.view";
}

public sealed record DocumentTypeSummary(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool RequiredByDefault,
    bool ExpirationRequired,
    int? DefaultValidityDays,
    bool FileRequired,
    bool DocumentNumberRequired,
    bool MultipleAllowed,
    IReadOnlyList<int> ReminderDays,
    bool IsActive,
    int DisplayOrder,
    IReadOnlyList<Guid> RequiredEmployeeTypeIds);

public sealed record CreateDocumentTypeRequest(
    string Code,
    string Name,
    string? Description,
    bool RequiredByDefault,
    bool ExpirationRequired,
    int? DefaultValidityDays,
    bool FileRequired,
    bool DocumentNumberRequired,
    bool MultipleAllowed,
    IReadOnlyList<int>? ReminderDays,
    int DisplayOrder,
    IReadOnlyList<Guid>? RequiredEmployeeTypeIds);

public sealed record EmployeeDocumentSummary(
    Guid Id,
    Guid EmployeeId,
    Guid DocumentTypeId,
    string DocumentTypeCode,
    string DocumentTypeName,
    string? DocumentNumber,
    DateOnly? IssueDate,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil,
    string Status,
    string? FileName,
    string? ContentType,
    long? FileSizeBytes,
    Guid? ReplacesDocumentId,
    int Version);

public sealed record EmployeeDocumentHistorySummary(
    Guid Id,
    Guid EmployeeDocumentId,
    string Action,
    string? FromStatus,
    string ToStatus,
    Guid ChangedBy,
    DateTimeOffset ChangedAt,
    string? Reason);

public sealed record MissingDocumentSummary(Guid DocumentTypeId, string Code, string Name, bool FileRequired, bool DocumentNumberRequired, bool ExpirationRequired);

public sealed record DocumentAttentionItem(
    Guid DocumentId,
    Guid EmployeeId,
    Guid CompanyId,
    string EmployeeNo,
    string EmployeeName,
    Guid DocumentTypeId,
    string DocumentTypeCode,
    string DocumentTypeName,
    DateOnly ValidUntil,
    string Status,
    int DaysRemaining);

public sealed record MissingEmployeeDocumentItem(
    Guid EmployeeId,
    Guid CompanyId,
    string EmployeeNo,
    string EmployeeName,
    Guid DocumentTypeId,
    string Code,
    string Name);

public sealed record DocumentEmployeeContext(Guid EmployeeId, Guid CompanyId, Guid EmployeeTypeId, string EmployeeNo, string EmployeeName);
public sealed record DocumentFact(Guid EmployeeId, Guid DocumentTypeId, string Status, DateOnly? ValidUntil);
public sealed record DocumentLifecycleResult(int Scanned, int Changed, int Expiring, int Expired);
public sealed record DocumentDashboardList<T>(IReadOnlyList<T> Items, int TotalCount);

public sealed record DocumentUploadFile(string OriginalName, string ContentType, long Length, Stream Content);

public sealed record UploadEmployeeDocumentRequest(
    Guid DocumentTypeId,
    string? DocumentNumber,
    DateOnly? IssueDate,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil,
    string? IssuingAuthority,
    string? CountryCode,
    string? Notes,
    DocumentUploadFile? File);

public sealed record DocumentFileDownload(Stream Content, string ContentType, string FileName);

public sealed record DocumentFilePolicyOptions(long MaxBytes)
{
    public static readonly long DefaultMaxBytes = 10 * 1024 * 1024;
}

public sealed record DocumentResult<T>(bool Succeeded, T? Value, string? ErrorCode, string? ErrorMessage) where T : class
{
    public static DocumentResult<T> Success(T value) => new(true, value, null, null);
    public static DocumentResult<T> Failure(string code, string message) => new(false, null, code, message);
}
