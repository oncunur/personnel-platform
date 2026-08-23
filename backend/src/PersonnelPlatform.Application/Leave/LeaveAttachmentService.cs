using System.Security.Cryptography;
using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Application.Documents;
using PersonnelPlatform.Domain.Documents;
using PersonnelPlatform.Domain.Leave;

namespace PersonnelPlatform.Application.Leave;

public sealed class LeaveAttachmentService(
    ILeaveAttachmentRepository attachmentRepository,
    ILeaveRepository leaveRepository,
    AccessControlService accessControlService,
    IFileStorage fileStorage,
    DocumentFilePolicyOptions filePolicy,
    TimeProvider timeProvider)
{
    public async Task<LeaveResult<IReadOnlyList<LeaveAttachmentSummary>>> ListAsync(Guid userId, Guid leaveId, CancellationToken cancellationToken)
    {
        var access = await ValidateLeaveAccessAsync(userId, leaveId, cancellationToken);
        if (access.Error is not null) return LeaveResult<IReadOnlyList<LeaveAttachmentSummary>>.Failure(access.Error.Value.Code, access.Error.Value.Message);
        return LeaveResult<IReadOnlyList<LeaveAttachmentSummary>>.Success(await attachmentRepository.ListAsync(leaveId, cancellationToken));
    }

    public async Task<LeaveResult<LeaveAttachmentSummary>> UploadAsync(Guid userId, Guid leaveId, UploadLeaveAttachmentRequest request, CancellationToken cancellationToken)
    {
        var access = await ValidateLeaveAccessAsync(userId, leaveId, cancellationToken);
        if (access.Error is not null) return LeaveResult<LeaveAttachmentSummary>.Failure(access.Error.Value.Code, access.Error.Value.Message);
        if (access.Leave!.Status != LeaveRequestStatuses.Draft)
            return LeaveResult<LeaveAttachmentSummary>.Failure("LEAVE_ATTACHMENT_LOCKED", "İzin eki yalnız talep taslak durumundayken eklenebilir.");
        if (request.File is null) return LeaveResult<LeaveAttachmentSummary>.Failure("LEAVE_ATTACHMENT_FILE_REQUIRED", "Dosya zorunludur.");

        var prepared = await PrepareFileAsync(request.File, cancellationToken);
        if (!prepared.Succeeded || prepared.Value is null)
            return LeaveResult<LeaveAttachmentSummary>.Failure(prepared.ErrorCode!, prepared.ErrorMessage!);

        var now = timeProvider.GetUtcNow();
        var storageKey = $"leave/{now:yyyy/MM}/{Guid.NewGuid():N}{prepared.Value.Extension}";
        var storedFile = StoredFile.CreatePending(
            prepared.Value.OriginalName,
            storageKey,
            prepared.Value.ContentType,
            prepared.Value.Extension,
            prepared.Value.Bytes.LongLength,
            prepared.Value.Sha256,
            fileStorage.ProviderCode,
            userId,
            now);
        attachmentRepository.AddStoredFile(storedFile);
        await attachmentRepository.SaveChangesAsync(cancellationToken);

        try
        {
            await fileStorage.WriteAsync(storageKey, prepared.Value.Bytes, cancellationToken);
        }
        catch
        {
            storedFile.MarkFailed();
            await attachmentRepository.SaveChangesAsync(cancellationToken);
            return LeaveResult<LeaveAttachmentSummary>.Failure("FILE_STORAGE_FAILED", "Dosya güvenli depolama alanına yazılamadı.");
        }

        storedFile.Activate();
        var attachment = LeaveAttachment.Create(leaveId, storedFile.Id, request.Description, now, userId);
        attachmentRepository.AddAttachment(attachment);
        await attachmentRepository.SaveChangesAsync(cancellationToken);

        var summary = (await attachmentRepository.ListAsync(leaveId, cancellationToken)).First(x => x.Id == attachment.Id);
        return LeaveResult<LeaveAttachmentSummary>.Success(summary);
    }

    public async Task<LeaveResult<LeaveAttachmentDownload>> OpenAsync(Guid userId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await attachmentRepository.FindAsync(attachmentId, cancellationToken);
        if (attachment is null) return LeaveResult<LeaveAttachmentDownload>.Failure("LEAVE_ATTACHMENT_NOT_FOUND", "İzin eki bulunamadı.");
        var access = await ValidateLeaveAccessAsync(userId, attachment.LeaveId, cancellationToken);
        if (access.Error is not null) return LeaveResult<LeaveAttachmentDownload>.Failure(access.Error.Value.Code, access.Error.Value.Message);

        var file = await attachmentRepository.FindStoredFileAsync(attachment.FileId, cancellationToken);
        if (file is null || file.Status != StoredFileStatuses.Active)
            return LeaveResult<LeaveAttachmentDownload>.Failure("FILE_NOT_FOUND", "Dosya bulunamadı veya kullanıma hazır değil.");
        var stream = await fileStorage.OpenReadAsync(file.StorageKey, cancellationToken);
        return stream is null
            ? LeaveResult<LeaveAttachmentDownload>.Failure("FILE_NOT_FOUND", "Dosya depolama alanında bulunamadı.")
            : LeaveResult<LeaveAttachmentDownload>.Success(new LeaveAttachmentDownload(stream, file.ContentType, file.OriginalName));
    }

    private async Task<(LeaveRequestSummary? Leave, (string Code, string Message)? Error)> ValidateLeaveAccessAsync(Guid userId, Guid leaveId, CancellationToken cancellationToken)
    {
        var leave = await leaveRepository.GetLeaveRequestSummaryAsync(leaveId, cancellationToken);
        if (leave is null) return (null, ("LEAVE_NOT_FOUND", "İzin talebi bulunamadı."));
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, leave.CompanyId, cancellationToken))
            return (null, ("SCOPE_DENIED", "İzin kaydının şirket kapsamına erişiminiz yok."));
        return (leave, null);
    }

    private async Task<LeaveResult<PreparedFile>> PrepareFileAsync(LeaveAttachmentUploadFile file, CancellationToken cancellationToken)
    {
        if (file.Length <= 0) return LeaveResult<PreparedFile>.Failure("FILE_EMPTY", "Dosya boş.");
        if (file.Length > filePolicy.MaxBytes) return LeaveResult<PreparedFile>.Failure("FILE_SIZE_LIMIT_EXCEEDED", $"Dosya boyutu en fazla {filePolicy.MaxBytes / 1024 / 1024} MB olabilir.");
        var originalName = Path.GetFileName(file.OriginalName);
        if (string.IsNullOrWhiteSpace(originalName) || originalName.Length > 255) return LeaveResult<PreparedFile>.Failure("FILE_NAME_INVALID", "Dosya adı geçersiz.");
        var extension = Path.GetExtension(originalName).ToLowerInvariant();
        var expectedContentType = extension switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => null
        };
        if (expectedContentType is null) return LeaveResult<PreparedFile>.Failure("FILE_TYPE_NOT_ALLOWED", "Yalnız PDF, JPG/JPEG ve PNG dosyaları yüklenebilir.");
        if (!string.Equals(file.ContentType, expectedContentType, StringComparison.OrdinalIgnoreCase))
            return LeaveResult<PreparedFile>.Failure("FILE_SECURITY_CHECK_FAILED", "Dosya uzantısı ile MIME türü eşleşmiyor.");

        await using var buffer = new MemoryStream((int)Math.Min(file.Length, filePolicy.MaxBytes));
        var chunk = new byte[81920];
        while (true)
        {
            var read = await file.Content.ReadAsync(chunk, cancellationToken);
            if (read == 0) break;
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            if (buffer.Length > filePolicy.MaxBytes) return LeaveResult<PreparedFile>.Failure("FILE_SIZE_LIMIT_EXCEEDED", "Dosya boyutu izin verilen sınırı aşıyor.");
        }

        var bytes = buffer.ToArray();
        if (!SignatureMatches(extension, bytes)) return LeaveResult<PreparedFile>.Failure("FILE_SECURITY_CHECK_FAILED", "Dosyanın gerçek içeriği izin verilen formatla eşleşmiyor.");
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return LeaveResult<PreparedFile>.Success(new PreparedFile(originalName, expectedContentType, extension, bytes, sha256));
    }

    private static bool SignatureMatches(string extension, byte[] bytes)
    {
        if (extension == ".pdf") return bytes.Length >= 5 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46 && bytes[4] == 0x2D;
        if (extension is ".jpg" or ".jpeg") return bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        if (extension == ".png") return bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;
        return false;
    }

    private sealed record PreparedFile(string OriginalName, string ContentType, string Extension, byte[] Bytes, string Sha256);
}
