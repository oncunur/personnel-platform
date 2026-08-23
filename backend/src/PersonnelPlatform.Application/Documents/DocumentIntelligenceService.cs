using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Domain.Documents;

namespace PersonnelPlatform.Application.Documents;

public sealed class DocumentIntelligenceService(
    IDocumentRepository repository,
    AccessControlService accessControlService,
    TimeProvider timeProvider)
{
    public Task<DocumentLifecycleResult> RefreshLifecycleAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return repository.RefreshLifecycleStatusesAsync(today, cancellationToken);
    }

    public async Task<DocumentResult<DocumentDashboardList<DocumentAttentionItem>>> ListExpiringAsync(Guid userId, int days, int limit, CancellationToken cancellationToken)
    {
        days = Math.Clamp(days, 1, 3650);
        limit = Math.Clamp(limit, 1, 500);
        var access = await ResolveAccessAsync(userId, cancellationToken);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var rows = await repository.ListAttentionDocumentsAsync(access.Global, access.CompanyIds, today, today.AddDays(days), false, limit, cancellationToken);
        return DocumentResult<DocumentDashboardList<DocumentAttentionItem>>.Success(new DocumentDashboardList<DocumentAttentionItem>(rows, rows.Count));
    }

    public async Task<DocumentResult<DocumentDashboardList<DocumentAttentionItem>>> ListExpiredAsync(Guid userId, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 500);
        var access = await ResolveAccessAsync(userId, cancellationToken);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var rows = await repository.ListAttentionDocumentsAsync(access.Global, access.CompanyIds, today, today, true, limit, cancellationToken);
        return DocumentResult<DocumentDashboardList<DocumentAttentionItem>>.Success(new DocumentDashboardList<DocumentAttentionItem>(rows, rows.Count));
    }

    public async Task<DocumentResult<DocumentDashboardList<MissingEmployeeDocumentItem>>> ListMissingAsync(Guid userId, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 500);
        var access = await ResolveAccessAsync(userId, cancellationToken);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var employees = await repository.ListEmployeeContextsAsync(access.Global, access.CompanyIds, limit, cancellationToken);
        if (employees.Count == 0)
            return DocumentResult<DocumentDashboardList<MissingEmployeeDocumentItem>>.Success(new DocumentDashboardList<MissingEmployeeDocumentItem>(Array.Empty<MissingEmployeeDocumentItem>(), 0));

        var types = (await repository.ListDocumentTypesAsync(cancellationToken)).Where(x => x.IsActive).ToArray();
        var requirements = await repository.ListRequirementsAsync(cancellationToken);
        var facts = await repository.ListDocumentFactsAsync(employees.Select(x => x.EmployeeId).ToArray(), cancellationToken);
        var factsByEmployee = facts.GroupBy(x => x.EmployeeId).ToDictionary(x => x.Key, x => x.ToArray());
        var missing = new List<MissingEmployeeDocumentItem>();

        foreach (var employee in employees)
        {
            var requiredTypeIds = types.Where(x => x.RequiredByDefault).Select(x => x.Id).ToHashSet();
            foreach (var requirement in requirements.Where(x => x.EmployeeTypeId == employee.EmployeeTypeId && x.IsRequired
                && (x.ValidFrom == null || x.ValidFrom <= today) && (x.ValidUntil == null || x.ValidUntil >= today)))
                requiredTypeIds.Add(requirement.DocumentTypeId);

            factsByEmployee.TryGetValue(employee.EmployeeId, out var employeeFacts);
            employeeFacts ??= Array.Empty<DocumentFact>();
            foreach (var type in types.Where(x => requiredTypeIds.Contains(x.Id)))
            {
                var satisfied = employeeFacts.Any(x => x.DocumentTypeId == type.Id
                    && x.Status is not (EmployeeDocumentStatuses.Archived or EmployeeDocumentStatuses.Cancelled)
                    && (x.ValidUntil == null || x.ValidUntil >= today));
                if (!satisfied)
                    missing.Add(new MissingEmployeeDocumentItem(employee.EmployeeId, employee.CompanyId, employee.EmployeeNo, employee.EmployeeName, type.Id, type.Code, type.Name));
                if (missing.Count >= limit) break;
            }
            if (missing.Count >= limit) break;
        }

        return DocumentResult<DocumentDashboardList<MissingEmployeeDocumentItem>>.Success(new DocumentDashboardList<MissingEmployeeDocumentItem>(missing, missing.Count));
    }

    private async Task<CompanyAccess> ResolveAccessAsync(Guid userId, CancellationToken cancellationToken)
    {
        var snapshot = await accessControlService.GetSnapshotAsync(userId, cancellationToken);
        return new CompanyAccess(
            snapshot.Scopes.Any(x => x.ScopeType == ScopeTypes.Global),
            snapshot.Scopes.Where(x => x.ScopeType == ScopeTypes.Company && x.ScopeId is not null).Select(x => x.ScopeId!.Value).Distinct().ToArray());
    }

    private sealed record CompanyAccess(bool Global, IReadOnlyCollection<Guid> CompanyIds);
}
