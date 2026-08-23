using PersonnelPlatform.Domain.Audit;

namespace PersonnelPlatform.Application.Audit;

public interface IAuditRepository
{
    void Add(AuditLog auditLog);

    Task<IReadOnlyList<AuditLog>> ListAsync(
        string? category,
        string? eventType,
        Guid? actorUserId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int take,
        CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
