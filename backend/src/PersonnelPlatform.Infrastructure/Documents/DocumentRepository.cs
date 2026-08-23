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
