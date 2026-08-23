using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Documents;

public static class EmployeeDocumentHistoryActions
{
    public const string Uploaded = "UPLOADED";
    public const string Renewed = "RENEWED";
    public const string Archived = "ARCHIVED";
    public const string Cancelled = "CANCELLED";
}

public sealed class EmployeeDocumentHistory : Entity
{
    private EmployeeDocumentHistory() { }

    private EmployeeDocumentHistory(Guid employeeDocumentId, string action, string? fromStatus, string toStatus, Guid changedBy, DateTimeOffset changedAt, string? reason, string? metadataJson)
    {
        EmployeeDocumentId = employeeDocumentId;
        Action = action;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ChangedBy = changedBy;
        ChangedAt = changedAt;
        Reason = reason;
        MetadataJson = metadataJson;
    }

    public Guid EmployeeDocumentId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? FromStatus { get; private set; }
    public string ToStatus { get; private set; } = string.Empty;
    public Guid ChangedBy { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }
    public string? Reason { get; private set; }
    public string? MetadataJson { get; private set; }

    public static EmployeeDocumentHistory Create(Guid documentId, string action, string? fromStatus, string toStatus, Guid changedBy, DateTimeOffset changedAt, string? reason = null, string? metadataJson = null)
    {
        if (documentId == Guid.Empty || changedBy == Guid.Empty) throw new ArgumentException("Required ids are missing.");
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(toStatus);
        if (action.Length > 40 || toStatus.Length > 30 || fromStatus?.Length > 30 || reason?.Length > 1000) throw new ArgumentException("History value is too long.");
        return new EmployeeDocumentHistory(documentId, action.Trim().ToUpperInvariant(), fromStatus, toStatus, changedBy, changedAt, string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), metadataJson);
    }
}
