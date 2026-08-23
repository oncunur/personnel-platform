using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Leave;
using PersonnelPlatform.Domain.Documents;
using PersonnelPlatform.Domain.Leave;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Leave;

public sealed class LeaveAttachmentRepository(ApplicationDbContext dbContext) : ILeaveAttachmentRepository
{
    public async Task<IReadOnlyList<LeaveAttachmentSummary>> ListAsync(Guid leaveId, CancellationToken cancellationToken)
    {
        var query =
            from attachment in dbContext.LeaveAttachments.AsNoTracking()
            join file in dbContext.StoredFiles.AsNoTracking() on attachment.FileId equals file.Id
            where attachment.LeaveId == leaveId && attachment.DeletedAt == null && file.Status == StoredFileStatuses.Active
            orderby attachment.CreatedAt descending
            select new LeaveAttachmentSummary(
                attachment.Id,
                attachment.LeaveId,
                attachment.FileId,
                file.OriginalName,
                file.ContentType,
                file.SizeBytes,
                attachment.Description,
                attachment.CreatedAt,
                attachment.CreatedBy!.Value);
        return await query.ToListAsync(cancellationToken);
    }

    public Task<LeaveAttachment?> FindAsync(Guid attachmentId, CancellationToken cancellationToken) =>
        dbContext.LeaveAttachments.FirstOrDefaultAsync(x => x.Id == attachmentId && x.DeletedAt == null, cancellationToken);

    public Task<StoredFile?> FindStoredFileAsync(Guid fileId, CancellationToken cancellationToken) =>
        dbContext.StoredFiles.FirstOrDefaultAsync(x => x.Id == fileId, cancellationToken);

    public void AddAttachment(LeaveAttachment attachment) => dbContext.LeaveAttachments.Add(attachment);
    public void AddStoredFile(StoredFile file) => dbContext.StoredFiles.Add(file);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
