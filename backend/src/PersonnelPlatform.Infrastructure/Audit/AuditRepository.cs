using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Audit;
using PersonnelPlatform.Domain.Audit;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Audit;

public sealed class AuditRepository(ApplicationDbContext dbContext) : IAuditRepository
{
    public void Add(AuditLog auditLog) => dbContext.AuditLogs.Add(auditLog);

    public async Task<IReadOnlyList<AuditLog>> ListAsync(
        string? category,
        string? eventType,
        Guid? actorUserId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int take,
        CancellationToken cancellationToken)
    {
        var query = dbContext.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            var normalizedCategory = category.Trim().ToUpperInvariant();
            query = query.Where(x => x.Category == normalizedCategory);
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            var normalizedEventType = eventType.Trim().ToUpperInvariant();
            query = query.Where(x => x.EventType == normalizedEventType);
        }

        if (actorUserId is not null)
        {
            query = query.Where(x => x.ActorUserId == actorUserId);
        }

        if (from is not null)
        {
            query = query.Where(x => x.OccurredAt >= from);
        }

        if (to is not null)
        {
            query = query.Where(x => x.OccurredAt <= to);
        }

        return await query
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
