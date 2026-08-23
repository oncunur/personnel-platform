using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Documents;

public static class StoredFileStatuses
{
    public const string Pending = "PENDING";
    public const string Active = "ACTIVE";
    public const string Failed = "FAILED";
    public const string Quarantined = "QUARANTINED";
    public const string Deleted = "DELETED";
}

public static class EmployeeDocumentStatuses
{
    public const string Valid = "VALID";
    public const string Expiring = "EXPIRING";
    public const string Expired = "EXPIRED";
    public const string Archived = "ARCHIVED";
    public const string Cancelled = "CANCELLED";

    public static bool IsTerminal(string status) => status is Archived or Cancelled;
}

public sealed class StoredFile : Entity
{
    private StoredFile() { }

    private StoredFile(string originalName, string storageKey, string contentType, string extension, long sizeBytes, string sha256, string provider, Guid uploadedBy, DateTimeOffset uploadedAt)
    {
        OriginalName = originalName;
        StorageKey = storageKey;
        ContentType = contentType;
        Extension = extension;
        SizeBytes = sizeBytes;
        Sha256 = sha256;
        Provider = provider;
        UploadedBy = uploadedBy;
        UploadedAt = uploadedAt;
        Status = StoredFileStatuses.Pending;
    }

    public string OriginalName { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public string Extension { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public string Provider { get; private set; } = string.Empty;
    public string Status { get; private set; } = StoredFileStatuses.Pending;
    public Guid UploadedBy { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }

    public static StoredFile CreatePending(string originalName, string storageKey, string contentType, string extension, long sizeBytes, string sha256, string provider, Guid uploadedBy, DateTimeOffset uploadedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalName);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        if (sizeBytes <= 0) throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        if (uploadedBy == Guid.Empty) throw new ArgumentException("Uploaded by is required.", nameof(uploadedBy));
        return new StoredFile(originalName, storageKey, contentType, extension, sizeBytes, sha256, provider, uploadedBy, uploadedAt);
    }

    public void Activate() => Status = StoredFileStatuses.Active;
    public void MarkFailed() => Status = StoredFileStatuses.Failed;
    public void Quarantine() => Status = StoredFileStatuses.Quarantined;
}

public sealed class DocumentType : AuditableEntity
{
    private DocumentType() { }

    private DocumentType(string code, string name, string? description, bool requiredByDefault, bool expirationRequired, int? defaultValidityDays, bool fileRequired, bool documentNumberRequired, bool multipleAllowed, string reminderDaysCsv, int displayOrder, DateTimeOffset createdAt, Guid createdBy)
    {
        Code = code;
        Name = name;
        Description = description;
        RequiredByDefault = requiredByDefault;
        ExpirationRequired = expirationRequired;
        DefaultValidityDays = defaultValidityDays;
        FileRequired = fileRequired;
        DocumentNumberRequired = documentNumberRequired;
        MultipleAllowed = multipleAllowed;
        ReminderDaysCsv = reminderDaysCsv;
        DisplayOrder = displayOrder;
        IsActive = true;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
    }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool RequiredByDefault { get; private set; }
    public bool ExpirationRequired { get; private set; }
    public int? DefaultValidityDays { get; private set; }
    public bool FileRequired { get; private set; }
    public bool DocumentNumberRequired { get; private set; }
    public bool MultipleAllowed { get; private set; }
    public string ReminderDaysCsv { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public int DisplayOrder { get; private set; }

    public static DocumentType Create(string code, string name, string? description, bool requiredByDefault, bool expirationRequired, int? defaultValidityDays, bool fileRequired, bool documentNumberRequired, bool multipleAllowed, IEnumerable<int>? reminderDays, int displayOrder, DateTimeOffset createdAt, Guid createdBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (defaultValidityDays is <= 0) throw new ArgumentOutOfRangeException(nameof(defaultValidityDays));
        var normalizedCode = code.Trim().ToUpperInvariant();
        if (normalizedCode.Length > 80) throw new ArgumentException("Document type code is too long.", nameof(code));
        if (name.Trim().Length > 150) throw new ArgumentException("Document type name is too long.", nameof(name));
        var reminders = (reminderDays ?? Array.Empty<int>()).Where(x => x >= 0 && x <= 3650).Distinct().OrderByDescending(x => x);
        return new DocumentType(normalizedCode, name.Trim(), string.IsNullOrWhiteSpace(description) ? null : description.Trim(), requiredByDefault, expirationRequired, defaultValidityDays, fileRequired, documentNumberRequired, multipleAllowed, string.Join(',', reminders), displayOrder, createdAt, createdBy);
    }

    public IReadOnlyList<int> ReminderDays() => string.IsNullOrWhiteSpace(ReminderDaysCsv)
        ? Array.Empty<int>()
        : ReminderDaysCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
}

public sealed class DocumentTypeEmployeeTypeRequirement : Entity
{
    private DocumentTypeEmployeeTypeRequirement() { }

    private DocumentTypeEmployeeTypeRequirement(Guid documentTypeId, Guid employeeTypeId, bool isRequired, DateOnly? validFrom, DateOnly? validUntil, DateTimeOffset createdAt, Guid createdBy)
    {
        DocumentTypeId = documentTypeId;
        EmployeeTypeId = employeeTypeId;
        IsRequired = isRequired;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
    }

    public Guid DocumentTypeId { get; private set; }
    public Guid EmployeeTypeId { get; private set; }
    public bool IsRequired { get; private set; }
    public DateOnly? ValidFrom { get; private set; }
    public DateOnly? ValidUntil { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }

    public static DocumentTypeEmployeeTypeRequirement Create(Guid documentTypeId, Guid employeeTypeId, bool isRequired, DateOnly? validFrom, DateOnly? validUntil, DateTimeOffset createdAt, Guid createdBy)
    {
        if (documentTypeId == Guid.Empty || employeeTypeId == Guid.Empty || createdBy == Guid.Empty) throw new ArgumentException("Ids are required.");
        if (validUntil is not null && validFrom is not null && validUntil < validFrom) throw new ArgumentException("Requirement validity range is invalid.");
        return new DocumentTypeEmployeeTypeRequirement(documentTypeId, employeeTypeId, isRequired, validFrom, validUntil, createdAt, createdBy);
    }
}

public sealed class EmployeeDocument : AuditableEntity
{
    private EmployeeDocument() { }

    private EmployeeDocument(Guid employeeId, Guid documentTypeId, Guid? fileId, string? documentNumber, DateOnly? issueDate, DateOnly? validFrom, DateOnly? validUntil, string? issuingAuthority, string? countryCode, string? notes, Guid? replacesDocumentId, DateTimeOffset createdAt, Guid createdBy)
    {
        EmployeeId = employeeId;
        DocumentTypeId = documentTypeId;
        FileId = fileId;
        DocumentNumber = documentNumber;
        IssueDate = issueDate;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        IssuingAuthority = issuingAuthority;
        CountryCode = countryCode;
        Notes = notes;
        ReplacesDocumentId = replacesDocumentId;
        Status = EmployeeDocumentStatuses.Valid;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
    }

    public Guid EmployeeId { get; private set; }
    public Guid DocumentTypeId { get; private set; }
    public Guid? FileId { get; private set; }
    public string? DocumentNumber { get; private set; }
    public DateOnly? IssueDate { get; private set; }
    public DateOnly? ValidFrom { get; private set; }
    public DateOnly? ValidUntil { get; private set; }
    public string? IssuingAuthority { get; private set; }
    public string? CountryCode { get; private set; }
    public string Status { get; private set; } = EmployeeDocumentStatuses.Valid;
    public string? Notes { get; private set; }
    public Guid? ReplacesDocumentId { get; private set; }

    public static EmployeeDocument Create(Guid employeeId, Guid documentTypeId, Guid? fileId, string? documentNumber, DateOnly? issueDate, DateOnly? validFrom, DateOnly? validUntil, string? issuingAuthority, string? countryCode, string? notes, Guid? replacesDocumentId, DateTimeOffset createdAt, Guid createdBy)
    {
        if (employeeId == Guid.Empty || documentTypeId == Guid.Empty || createdBy == Guid.Empty) throw new ArgumentException("Required ids are missing.");
        if (validUntil is not null && validFrom is not null && validUntil < validFrom) throw new ArgumentException("Document validity range is invalid.");
        if (issueDate is not null && validUntil is not null && validUntil < issueDate) throw new ArgumentException("Document expiration cannot precede issue date.");
        return new EmployeeDocument(employeeId, documentTypeId, fileId, Clean(documentNumber, 150), issueDate, validFrom, validUntil, Clean(issuingAuthority, 200), Clean(countryCode, 3)?.ToUpperInvariant(), Clean(notes, 2000), replacesDocumentId, createdAt, createdBy);
    }

    public void Archive(DateTimeOffset now, Guid actor)
    {
        if (Status == EmployeeDocumentStatuses.Archived) return;
        if (Status == EmployeeDocumentStatuses.Cancelled) throw new InvalidOperationException("Cancelled document cannot be archived.");
        Status = EmployeeDocumentStatuses.Archived;
        UpdatedAt = now;
        UpdatedBy = actor;
        Version++;
    }

    public void Cancel(DateTimeOffset now, Guid actor)
    {
        if (Status == EmployeeDocumentStatuses.Cancelled) return;
        Status = EmployeeDocumentStatuses.Cancelled;
        UpdatedAt = now;
        UpdatedBy = actor;
        Version++;
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim();
        if (cleaned.Length > maxLength) throw new ArgumentException("Value is too long.");
        return cleaned;
    }
}
