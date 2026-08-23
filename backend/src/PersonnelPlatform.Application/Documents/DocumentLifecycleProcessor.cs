namespace PersonnelPlatform.Application.Documents;

public sealed class DocumentLifecycleProcessor(IDocumentRepository repository, TimeProvider timeProvider)
{
    public Task<DocumentLifecycleResult> RunAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return repository.RefreshLifecycleStatusesAsync(today, cancellationToken);
    }
}
