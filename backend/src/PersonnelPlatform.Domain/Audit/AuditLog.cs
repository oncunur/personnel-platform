using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Audit;

public sealed class AuditLog : Entity
{
    private AuditLog()
    {
    }

    private AuditLog(
        string category,
        string eventType,
        bool succeeded,
        string severity,
        DateTimeOffset occurredAt,
        Guid? actorUserId,
        string? actorUsername,
        string? ipAddress,
        string? userAgent,
        string? traceId,
        string? targetType,
        string? targetId,
        string? errorCode,
        string? message,
        string? metadataJson)
    {
        Category = category;
        EventType = eventType;
        Succeeded = succeeded;
        Severity = severity;
        OccurredAt = occurredAt;
        ActorUserId = actorUserId;
        ActorUsername = Normalize(actorUsername, 100);
        IpAddress = Normalize(ipAddress, 64);
        UserAgent = Normalize(userAgent, 500);
        TraceId = Normalize(traceId, 100);
        TargetType = Normalize(targetType, 100);
        TargetId = Normalize(targetId, 100);
        ErrorCode = Normalize(errorCode, 100);
        Message = Normalize(message, 500);
        MetadataJson = Normalize(metadataJson, 4000);
    }

    public string Category { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public bool Succeeded { get; private set; }
    public string Severity { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string? ActorUsername { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? TraceId { get; private set; }
    public string? TargetType { get; private set; }
    public string? TargetId { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? Message { get; private set; }
    public string? MetadataJson { get; private set; }

    public static AuditLog Create(
        string category,
        string eventType,
        bool succeeded,
        string severity,
        DateTimeOffset occurredAt,
        Guid? actorUserId = null,
        string? actorUsername = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? traceId = null,
        string? targetType = null,
        string? targetId = null,
        string? errorCode = null,
        string? message = null,
        string? metadataJson = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(severity);

        if (category.Length > 50) throw new ArgumentOutOfRangeException(nameof(category));
        if (eventType.Length > 100) throw new ArgumentOutOfRangeException(nameof(eventType));
        if (severity.Length > 20) throw new ArgumentOutOfRangeException(nameof(severity));

        return new AuditLog(
            category.Trim(), eventType.Trim(), succeeded, severity.Trim(), occurredAt,
            actorUserId, actorUsername, ipAddress, userAgent, traceId,
            targetType, targetId, errorCode, message, metadataJson);
    }

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
