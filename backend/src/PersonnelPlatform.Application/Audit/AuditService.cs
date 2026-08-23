using PersonnelPlatform.Domain.Audit;

namespace PersonnelPlatform.Application.Audit;

public static class AuditCategories
{
    public const string Security = "SECURITY";
    public const string Administration = "ADMINISTRATION";
}

public static class AuditSeverities
{
    public const string Info = "INFO";
    public const string Warning = "WARNING";
    public const string Critical = "CRITICAL";
}

public sealed record AuditEvent(
    string Category,
    string EventType,
    bool Succeeded,
    string Severity,
    Guid? ActorUserId = null,
    string? ActorUsername = null,
    string? IpAddress = null,
    string? UserAgent = null,
    string? TraceId = null,
    string? TargetType = null,
    string? TargetId = null,
    string? ErrorCode = null,
    string? Message = null,
    string? MetadataJson = null);

public sealed record AuditLogItem(
    Guid Id,
    string Category,
    string EventType,
    bool Succeeded,
    string Severity,
    DateTimeOffset OccurredAt,
    Guid? ActorUserId,
    string? ActorUsername,
    string? IpAddress,
    string? TraceId,
    string? TargetType,
    string? TargetId,
    string? ErrorCode,
    string? Message);

public sealed class AuditService(
    IAuditRepository repository,
    TimeProvider timeProvider)
{
    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        repository.Add(AuditLog.Create(
            auditEvent.Category,
            auditEvent.EventType,
            auditEvent.Succeeded,
            auditEvent.Severity,
            timeProvider.GetUtcNow(),
            auditEvent.ActorUserId,
            auditEvent.ActorUsername,
            auditEvent.IpAddress,
            auditEvent.UserAgent,
            auditEvent.TraceId,
            auditEvent.TargetType,
            auditEvent.TargetId,
            auditEvent.ErrorCode,
            auditEvent.Message,
            auditEvent.MetadataJson));

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogItem>> ListAsync(
        string? category,
        string? eventType,
        Guid? actorUserId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int take,
        CancellationToken cancellationToken)
    {
        var boundedTake = Math.Clamp(take, 1, 500);
        var rows = await repository.ListAsync(category, eventType, actorUserId, from, to, boundedTake, cancellationToken);

        return rows.Select(x => new AuditLogItem(
            x.Id,
            x.Category,
            x.EventType,
            x.Succeeded,
            x.Severity,
            x.OccurredAt,
            x.ActorUserId,
            x.ActorUsername,
            x.IpAddress,
            x.TraceId,
            x.TargetType,
            x.TargetId,
            x.ErrorCode,
            x.Message)).ToArray();
    }
}
