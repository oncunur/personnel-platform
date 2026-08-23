using PersonnelPlatform.Domain.Documents;

namespace PersonnelPlatform.Application.Documents;

public interface IDocumentRepository
{
    Task<IReadOnlyList<DocumentType>> ListDocumentTypesAsync(CancellationToken cancellationToken);
    Task<DocumentType?> FindDocumentTypeAsync(Guid documentTypeId, CancellationToken cancellationToken);
    Task<bool> DocumentTypeCodeExistsAsync(string code, CancellationToken cancellationToken);
    Task<IReadOnlyList<DocumentTypeEmployeeTypeRequirement>> ListRequirementsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<DocumentTypeEmployeeTypeRequirement>> ListRequirementsForEmployeeTypeAsync(Guid employeeTypeId, DateOnly onDate, CancellationToken cancellationToken);
    void AddDocumentType(DocumentType documentType);
    void AddRequirement(DocumentTypeEmployeeTypeRequirement requirement);

    Task<IReadOnlyList<EmployeeDocument>> ListEmployeeDocumentsAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<EmployeeDocument?> FindEmployeeDocumentAsync(Guid documentId, CancellationToken cancellationToken);
    Task<bool> HasActiveDocumentOfTypeAsync(Guid employeeId, Guid documentTypeId, Guid? excludeDocumentId, CancellationToken cancellationToken);
    void AddEmployeeDocument(EmployeeDocument document);

    Task<IReadOnlyList<DocumentAttentionItem>> ListAttentionDocumentsAsync(bool global, IReadOnlyCollection<Guid> companyIds, DateOnly fromDate, DateOnly toDate, bool expiredOnly, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<DocumentEmployeeContext>> ListEmployeeContextsAsync(bool global, IReadOnlyCollection<Guid> companyIds, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<DocumentFact>> ListDocumentFactsAsync(IReadOnlyCollection<Guid> employeeIds, CancellationToken cancellationToken);
    Task<DocumentLifecycleResult> RefreshLifecycleStatusesAsync(DateOnly today, CancellationToken cancellationToken);

    Task<StoredFile?> FindStoredFileAsync(Guid fileId, CancellationToken cancellationToken);
    Task<IReadOnlyList<StoredFile>> ListStoredFilesAsync(IReadOnlyCollection<Guid> fileIds, CancellationToken cancellationToken);
    void AddStoredFile(StoredFile file);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IFileStorage
{
    string ProviderCode { get; }
    Task WriteAsync(string storageKey, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}
