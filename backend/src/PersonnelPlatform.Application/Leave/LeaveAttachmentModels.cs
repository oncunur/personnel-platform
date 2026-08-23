namespace PersonnelPlatform.Application.Leave;

public sealed record LeaveAttachmentSummary(
    Guid Id,
    Guid LeaveId,
    Guid FileId,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string? Description,
    DateTimeOffset UploadedAt,
    Guid UploadedBy);

public sealed record LeaveAttachmentUploadFile(string OriginalName, string ContentType, long Length, Stream Content);
public sealed record UploadLeaveAttachmentRequest(string? Description, LeaveAttachmentUploadFile? File);
public sealed record LeaveAttachmentDownload(Stream Content, string ContentType, string FileName);
