using PersonnelPlatform.Application.Documents;
using PersonnelPlatform.Application.Reporting;

namespace PersonnelPlatform.Infrastructure.Reporting;

public sealed class ReportFileStorage(IFileStorage fileStorage) : IReportFileStorage
{
    public Task WriteAsync(string storageKey, ReadOnlyMemory<byte> content, CancellationToken ct) => fileStorage.WriteAsync(storageKey, content, ct);
    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct) => fileStorage.OpenReadAsync(storageKey, ct);
}
