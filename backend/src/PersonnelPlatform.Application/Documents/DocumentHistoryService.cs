using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Application.Personnel;

namespace PersonnelPlatform.Application.Documents;

public sealed class DocumentHistoryService(
    IDocumentHistoryRepository historyRepository,
    IDocumentRepository documentRepository,
    IPersonnelRepository personnelRepository,
    AccessControlService accessControlService)
{
    public async Task<DocumentResult<EmployeeDocumentSummary>> GetAsync(Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        var access = await ResolveAsync(userId, documentId, cancellationToken);
        if (!access.Succeeded || access.Document is null)
            return DocumentResult<EmployeeDocumentSummary>.Failure(access.ErrorCode!, access.ErrorMessage!);

        var type = await documentRepository.FindDocumentTypeAsync(access.Document.DocumentTypeId, cancellationToken);
        if (type is null) return DocumentResult<EmployeeDocumentSummary>.Failure("DOCUMENT_TYPE_NOT_FOUND", "Belge türü bulunamadı.");
        var file = access.Document.FileId is null ? null : await documentRepository.FindStoredFileAsync(access.Document.FileId.Value, cancellationToken);
        return DocumentResult<EmployeeDocumentSummary>.Success(new EmployeeDocumentSummary(
            access.Document.Id,
            access.Document.EmployeeId,
            access.Document.DocumentTypeId,
            type.Code,
            type.Name,
            access.Document.DocumentNumber,
            access.Document.IssueDate,
            access.Document.ValidFrom,
            access.Document.ValidUntil,
            access.Document.Status,
            file?.OriginalName,
            file?.ContentType,
            file?.SizeBytes,
            access.Document.ReplacesDocumentId,
            access.Document.Version));
    }

    public async Task<DocumentResult<IReadOnlyList<EmployeeDocumentHistorySummary>>> ListAsync(Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        var access = await ResolveAsync(userId, documentId, cancellationToken);
        if (!access.Succeeded)
            return DocumentResult<IReadOnlyList<EmployeeDocumentHistorySummary>>.Failure(access.ErrorCode!, access.ErrorMessage!);

        var rows = await historyRepository.ListAsync(documentId, cancellationToken);
        return DocumentResult<IReadOnlyList<EmployeeDocumentHistorySummary>>.Success(rows
            .Select(x => new EmployeeDocumentHistorySummary(x.Id, x.EmployeeDocumentId, x.Action, x.FromStatus, x.ToStatus, x.ChangedBy, x.ChangedAt, x.Reason))
            .ToArray());
    }

    private async Task<AccessResult> ResolveAsync(Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await documentRepository.FindEmployeeDocumentAsync(documentId, cancellationToken);
        if (document is null) return AccessResult.Failure("DOCUMENT_NOT_FOUND", "Belge bulunamadı.");
        var employee = await personnelRepository.FindEmployeeAsync(document.EmployeeId, cancellationToken);
        if (employee is null) return AccessResult.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, employee.CompanyId, cancellationToken))
            return AccessResult.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");
        return AccessResult.Success(document);
    }

    private sealed record AccessResult(bool Succeeded, PersonnelPlatform.Domain.Documents.EmployeeDocument? Document, string? ErrorCode, string? ErrorMessage)
    {
        public static AccessResult Success(PersonnelPlatform.Domain.Documents.EmployeeDocument document) => new(true, document, null, null);
        public static AccessResult Failure(string code, string message) => new(false, null, code, message);
    }
}
