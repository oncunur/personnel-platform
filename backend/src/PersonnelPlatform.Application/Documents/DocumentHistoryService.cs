using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Application.Personnel;
using PersonnelPlatform.Domain.Documents;

namespace PersonnelPlatform.Application.Documents;

public sealed class DocumentHistoryService(
    IDocumentHistoryRepository historyRepository,
    IDocumentRepository documentRepository,
    IPersonnelRepository personnelRepository,
    AccessControlService accessControlService,
    TimeProvider timeProvider)
{
    public async Task RecordCreatedAsync(Guid actorUserId, EmployeeDocumentSummary document, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var action = document.ReplacesDocumentId is null ? EmployeeDocumentHistoryActions.Uploaded : EmployeeDocumentHistoryActions.Renewed;
        historyRepository.Add(EmployeeDocumentHistory.Create(document.Id, action, null, document.Status, actorUserId, now));
        if (document.ReplacesDocumentId is Guid replacedId)
            historyRepository.Add(EmployeeDocumentHistory.Create(replacedId, EmployeeDocumentHistoryActions.Archived, null, EmployeeDocumentStatuses.Archived, actorUserId, now, "Belge yenileme nedeniyle arşivlendi."));
        await historyRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordCancelledAsync(Guid actorUserId, EmployeeDocumentSummary document, CancellationToken cancellationToken)
    {
        historyRepository.Add(EmployeeDocumentHistory.Create(document.Id, EmployeeDocumentHistoryActions.Cancelled, null, EmployeeDocumentStatuses.Cancelled, actorUserId, timeProvider.GetUtcNow()));
        await historyRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<DocumentResult<IReadOnlyList<EmployeeDocumentHistorySummary>>> ListAsync(Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await documentRepository.FindEmployeeDocumentAsync(documentId, cancellationToken);
        if (document is null) return DocumentResult<IReadOnlyList<EmployeeDocumentHistorySummary>>.Failure("DOCUMENT_NOT_FOUND", "Belge bulunamadı.");
        var employee = await personnelRepository.FindEmployeeAsync(document.EmployeeId, cancellationToken);
        if (employee is null) return DocumentResult<IReadOnlyList<EmployeeDocumentHistorySummary>>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, employee.CompanyId, cancellationToken))
            return DocumentResult<IReadOnlyList<EmployeeDocumentHistorySummary>>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");

        var rows = await historyRepository.ListAsync(documentId, cancellationToken);
        return DocumentResult<IReadOnlyList<EmployeeDocumentHistorySummary>>.Success(rows
            .Select(x => new EmployeeDocumentHistorySummary(x.Id, x.EmployeeDocumentId, x.Action, x.FromStatus, x.ToStatus, x.ChangedBy, x.ChangedAt, x.Reason))
            .ToArray());
    }
}
