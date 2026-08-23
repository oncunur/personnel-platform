using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Documents;
using PersonnelPlatform.Domain.Documents;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Documents;

public sealed class DocumentHistoryRepository(ApplicationDbContext dbContext) : IDocumentHistoryRepository
{
    public async Task<IReadOnlyList<EmployeeDocumentHistory>> ListAsync(Guid documentId, CancellationToken cancellationToken) =>
        await dbContext.EmployeeDocumentHistories.AsNoTracking()
            .Where(x => x.EmployeeDocumentId == documentId)
            .OrderByDescending(x => x.ChangedAt)
            .ToListAsync(cancellationToken);

    public void Add(EmployeeDocumentHistory history) => dbContext.EmployeeDocumentHistories.Add(history);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
