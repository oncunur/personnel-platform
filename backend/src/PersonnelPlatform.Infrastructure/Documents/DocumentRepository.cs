using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Documents;
using PersonnelPlatform.Domain.Documents;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Documents;

public sealed class DocumentRepository(ApplicationDbContext dbContext) : IDocumentRepository
{
    public async Task<IReadOnlyList<DocumentType>> ListDocumentTypesAsync(CancellationToken cancellationToken) =>
        await dbContext.DocumentTypes.Where(x => x.DeletedAt == null).OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<DocumentType?> FindDocumentTypeAsync(Guid documentTypeId, CancellationToken cancellationToken) =>
        dbContext.DocumentTypes.FirstOrDefaultAsync(x => x.Id == documentTypeId && x.DeletedAt == null, cancellationToken);

    public Task<bool> DocumentTypeCodeExistsAsync(string code, CancellationToken cancellationToken) =>
        dbContext.DocumentTypes.AnyAsync(x => x.Code == code && x.DeletedAt == null, cancellationToken);

    public async Task<IReadOnlyList<DocumentTypeEmployeeTypeRequirement>> ListRequirementsAsync(CancellationToken cancellationToken) =>
        await dbContext.DocumentTypeEmployeeTypeRequirements.ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DocumentTypeEmployeeTypeRequirement>> ListRequirementsForEmployeeTypeAsync(Guid employeeTypeId, DateOnly onDate, CancellationToken cancellationToken) =>
        await dbContext.DocumentTypeEmployeeTypeRequirements
            .Where(x => x.EmployeeTypeId == employeeTypeId && (x.ValidFrom == null || x.ValidFrom <= onDate) && (x.ValidUntil == null || x.ValidUntil >= onDate))
            .ToListAsync(cancellationToken);

    public void AddDocumentType(DocumentType documentType) => dbContext.DocumentTypes.Add(documentType);
    public void AddRequirement(DocumentTypeEmployeeTypeRequirement requirement) => dbContext.DocumentTypeEmployeeTypeRequirements.Add(requirement);

    public async Task<IReadOnlyList<EmployeeDocument>> ListEmployeeDocumentsAsync(Guid employeeId, CancellationToken cancellationToken) =>
        await dbContext.EmployeeDocuments.Where(x => x.EmployeeId == employeeId && x.DeletedAt == null).OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);

    public Task<EmployeeDocument?> FindEmployeeDocumentAsync(Guid documentId, CancellationToken cancellationToken) =>
        dbContext.EmployeeDocuments.FirstOrDefaultAsync(x => x.Id == documentId && x.DeletedAt == null, cancellationToken);

    public Task<bool> HasActiveDocumentOfTypeAsync(Guid employeeId, Guid documentTypeId, Guid? excludeDocumentId, CancellationToken cancellationToken) =>
        dbContext.EmployeeDocuments.AnyAsync(x => x.EmployeeId == employeeId && x.DocumentTypeId == documentTypeId && x.DeletedAt == null
            && x.Status != EmployeeDocumentStatuses.Archived && x.Status != EmployeeDocumentStatuses.Cancelled
            && (excludeDocumentId == null || x.Id != excludeDocumentId.Value), cancellationToken);

    public void AddEmployeeDocument(EmployeeDocument document) => dbContext.EmployeeDocuments.Add(document);

    public async Task<IReadOnlyList<DocumentAttentionItem>> ListAttentionDocumentsAsync(bool global, IReadOnlyCollection<Guid> companyIds, DateOnly fromDate, DateOnly toDate, bool expiredOnly, int limit, CancellationToken cancellationToken)
    {
        var companyArray = companyIds.ToArray();
        var query =
            from document in dbContext.EmployeeDocuments.AsNoTracking()
            join employee in dbContext.Employees.AsNoTracking() on document.EmployeeId equals employee.Id
            join type in dbContext.DocumentTypes.AsNoTracking() on document.DocumentTypeId equals type.Id
            where document.DeletedAt == null && employee.DeletedAt == null && type.DeletedAt == null
                && document.Status != EmployeeDocumentStatuses.Archived && document.Status != EmployeeDocumentStatuses.Cancelled
                && document.ValidUntil != null
                && (global || companyArray.Contains(employee.CompanyId))
            select new { document, employee, type };

        query = expiredOnly
            ? query.Where(x => x.document.ValidUntil < fromDate)
            : query.Where(x => x.document.ValidUntil >= fromDate && x.document.ValidUntil <= toDate);

        var rows = await query
            .OrderBy(x => x.document.ValidUntil)
            .ThenBy(x => x.employee.LastName)
            .ThenBy(x => x.employee.FirstName)
            .Take(limit)
            .Select(x => new
            {
                x.document.Id,
                x.document.EmployeeId,
                x.employee.CompanyId,
                x.employee.EmployeeNo,
                x.employee.FirstName,
                x.employee.LastName,
                x.document.DocumentTypeId,
                TypeCode = x.type.Code,
                TypeName = x.type.Name,
                ValidUntil = x.document.ValidUntil!.Value,
                x.document.Status
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new DocumentAttentionItem(
            x.Id,
            x.EmployeeId,
            x.CompanyId,
            x.EmployeeNo,
            $"{x.FirstName} {x.LastName}".Trim(),
            x.DocumentTypeId,
            x.TypeCode,
            x.TypeName,
            x.ValidUntil,
            expiredOnly ? EmployeeDocumentStatuses.Expired : EmployeeDocumentStatuses.Expiring,
            x.ValidUntil.DayNumber - fromDate.DayNumber)).ToArray();
    }

    public async Task<IReadOnlyList<DocumentEmployeeContext>> ListEmployeeContextsAsync(bool global, IReadOnlyCollection<Guid> companyIds, int limit, CancellationToken cancellationToken)
    {
        var companyArray = companyIds.ToArray();
        return await dbContext.Employees.AsNoTracking()
            .Where(x => x.DeletedAt == null && x.Status != "TERMINATED" && (global || companyArray.Contains(x.CompanyId)))
            .OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
            .Take(limit)
            .Select(x => new DocumentEmployeeContext(x.Id, x.CompanyId, x.EmployeeTypeId, x.EmployeeNo, (x.FirstName + " " + x.LastName).Trim()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentFact>> ListDocumentFactsAsync(IReadOnlyCollection<Guid> employeeIds, CancellationToken cancellationToken)
    {
        if (employeeIds.Count == 0) return Array.Empty<DocumentFact>();
        var ids = employeeIds.ToArray();
        return await dbContext.EmployeeDocuments.AsNoTracking()
            .Where(x => ids.Contains(x.EmployeeId) && x.DeletedAt == null && x.Status != EmployeeDocumentStatuses.Archived && x.Status != EmployeeDocumentStatuses.Cancelled)
            .Select(x => new DocumentFact(x.EmployeeId, x.DocumentTypeId, x.Status, x.ValidUntil))
            .ToListAsync(cancellationToken);
    }

    public async Task<DocumentLifecycleResult> RefreshLifecycleStatusesAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var active = dbContext.EmployeeDocuments.Where(x => x.DeletedAt == null && x.Status != EmployeeDocumentStatuses.Archived && x.Status != EmployeeDocumentStatuses.Cancelled);
        var scanned = await active.CountAsync(cancellationToken);
        var changed = 0;

        changed += await active
            .Where(x => x.ValidUntil != null && x.ValidUntil < today && x.Status != EmployeeDocumentStatuses.Expired)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, EmployeeDocumentStatuses.Expired)
                .SetProperty(x => x.Version, x => x.Version + 1), cancellationToken);

        var types = await dbContext.DocumentTypes.AsNoTracking().Where(x => x.DeletedAt == null && x.IsActive).ToListAsync(cancellationToken);
        foreach (var type in types)
        {
            var threshold = type.ReminderDays().DefaultIfEmpty(30).Max();
            var expiringThrough = today.AddDays(threshold);
            changed += await active
                .Where(x => x.DocumentTypeId == type.Id && x.ValidUntil != null && x.ValidUntil >= today && x.ValidUntil <= expiringThrough && x.Status != EmployeeDocumentStatuses.Expiring)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, EmployeeDocumentStatuses.Expiring)
                    .SetProperty(x => x.Version, x => x.Version + 1), cancellationToken);

            changed += await active
                .Where(x => x.DocumentTypeId == type.Id && (x.ValidUntil == null || x.ValidUntil > expiringThrough) && x.Status != EmployeeDocumentStatuses.Valid)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, EmployeeDocumentStatuses.Valid)
                    .SetProperty(x => x.Version, x => x.Version + 1), cancellationToken);
        }

        var expiring = await active.CountAsync(x => x.Status == EmployeeDocumentStatuses.Expiring, cancellationToken);
        var expired = await active.CountAsync(x => x.Status == EmployeeDocumentStatuses.Expired, cancellationToken);
        return new DocumentLifecycleResult(scanned, changed, expiring, expired);
    }

    public Task<StoredFile?> FindStoredFileAsync(Guid fileId, CancellationToken cancellationToken) =>
        dbContext.StoredFiles.FirstOrDefaultAsync(x => x.Id == fileId, cancellationToken);

    public async Task<IReadOnlyList<StoredFile>> ListStoredFilesAsync(IReadOnlyCollection<Guid> fileIds, CancellationToken cancellationToken)
    {
        if (fileIds.Count == 0) return Array.Empty<StoredFile>();
        return await dbContext.StoredFiles.Where(x => fileIds.Contains(x.Id)).ToListAsync(cancellationToken);
    }

    public void AddStoredFile(StoredFile file) => dbContext.StoredFiles.Add(file);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
