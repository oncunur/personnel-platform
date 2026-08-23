using PersonnelPlatform.Domain.Documents;
using PersonnelPlatform.Domain.Leave;

namespace PersonnelPlatform.Application.Leave;

public interface ILeaveAttachmentRepository
{
    Task<IReadOnlyList<LeaveAttachmentSummary>> ListAsync(Guid leaveId, CancellationToken cancellationToken);
    Task<LeaveAttachment?> FindAsync(Guid attachmentId, CancellationToken cancellationToken);
    Task<StoredFile?> FindStoredFileAsync(Guid fileId, CancellationToken cancellationToken);
    void AddAttachment(LeaveAttachment attachment);
    void AddStoredFile(StoredFile file);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
