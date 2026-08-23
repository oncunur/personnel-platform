using PersonnelPlatform.Domain.Reporting;

namespace PersonnelPlatform.Application.Reporting;

public interface IReportingRepository
{
    Task<Project360Summary?> GetProject360Async(Guid projectId, DateOnly from, DateOnly to, CancellationToken ct);
    Task<IReadOnlyList<ManagementProjectSummary>> ListManagementAsync(Guid companyId, DateOnly from, DateOnly to, CancellationToken ct);

    void AddExportJob(ReportExportJob job);
    Task<ReportExportJob?> FindExportJobAsync(Guid exportJobId, CancellationToken ct);
    Task<IReadOnlyList<ReportExportJobSummary>> ListExportJobsAsync(Guid userId, Guid? companyId, int take, CancellationToken ct);
    Task<IReadOnlyList<ReportExportJob>> ListQueuedExportJobsAsync(int take, CancellationToken ct);
    Task<int> SaveChangesAsync(CancellationToken ct);
}

public interface IReportFileStorage
{
    Task WriteAsync(string storageKey, ReadOnlyMemory<byte> content, CancellationToken ct);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct);
}
