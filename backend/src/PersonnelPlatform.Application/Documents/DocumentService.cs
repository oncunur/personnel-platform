using System.Security.Cryptography;
using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Application.Personnel;
using PersonnelPlatform.Domain.Documents;

namespace PersonnelPlatform.Application.Documents;

public sealed class DocumentService(
    IDocumentRepository documentRepository,
    IPersonnelRepository personnelRepository,
    AccessControlService accessControlService,
    IFileStorage fileStorage,
    DocumentFilePolicyOptions filePolicy,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<DocumentTypeSummary>> ListDocumentTypesAsync(CancellationToken cancellationToken)
    {
        var types = await documentRepository.ListDocumentTypesAsync(cancellationToken);
        var requirements = await documentRepository.ListRequirementsAsync(cancellationToken);
        return types.Select(type => ToTypeSummary(type, requirements)).ToArray();
    }

    public async Task<DocumentResult<DocumentTypeSummary>> CreateDocumentTypeAsync(Guid actorUserId, CreateDocumentTypeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
            return DocumentResult<DocumentTypeSummary>.Failure("DOCUMENT_TYPE_DATA_INVALID", "Belge türü kodu ve adı zorunludur.");

        var code = request.Code.Trim().ToUpperInvariant();
        if (await documentRepository.DocumentTypeCodeExistsAsync(code, cancellationToken))
            return DocumentResult<DocumentTypeSummary>.Failure("DOCUMENT_TYPE_ALREADY_EXISTS", "Bu belge türü kodu zaten kullanılıyor.");

        var employeeTypeIds = (request.RequiredEmployeeTypeIds ?? Array.Empty<Guid>()).Distinct().ToArray();
        foreach (var employeeTypeId in employeeTypeIds)
        {
            var employeeType = await personnelRepository.FindEmployeeTypeAsync(employeeTypeId, cancellationToken);
            if (employeeType is null || !employeeType.IsActive)
                return DocumentResult<DocumentTypeSummary>.Failure("EMPLOYEE_TYPE_INVALID", "Zorunluluk için seçilen personel tipi bulunamadı veya pasif.");
        }

        try
        {
            var now = timeProvider.GetUtcNow();
            var type = DocumentType.Create(code, request.Name, request.Description, request.RequiredByDefault, request.ExpirationRequired, request.DefaultValidityDays,
                request.FileRequired, request.DocumentNumberRequired, request.MultipleAllowed, request.ReminderDays, request.DisplayOrder, now, actorUserId);
            documentRepository.AddDocumentType(type);
            foreach (var employeeTypeId in employeeTypeIds)
                documentRepository.AddRequirement(DocumentTypeEmployeeTypeRequirement.Create(type.Id, employeeTypeId, true, null, null, now, actorUserId));
            await documentRepository.SaveChangesAsync(cancellationToken);
            var requirements = employeeTypeIds.Select(id => DocumentTypeEmployeeTypeRequirement.Create(type.Id, id, true, null, null, now, actorUserId)).ToArray();
            return DocumentResult<DocumentTypeSummary>.Success(ToTypeSummary(type, requirements));
        }
        catch (ArgumentException)
        {
            return DocumentResult<DocumentTypeSummary>.Failure("DOCUMENT_TYPE_DATA_INVALID", "Belge türü bilgileri geçersiz.");
        }
    }

    public async Task<DocumentResult<IReadOnlyList<EmployeeDocumentSummary>>> ListEmployeeDocumentsAsync(Guid userId, Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await personnelRepository.FindEmployeeAsync(employeeId, cancellationToken);
        if (employee is null) return DocumentResult<IReadOnlyList<EmployeeDocumentSummary>>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, employee.CompanyId, cancellationToken)) return DocumentResult<IReadOnlyList<EmployeeDocumentSummary>>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");

        var documents = await documentRepository.ListEmployeeDocumentsAsync(employeeId, cancellationToken);
        var types = (await documentRepository.ListDocumentTypesAsync(cancellationToken)).ToDictionary(x => x.Id);
        var fileIds = documents.Where(x => x.FileId is not null).Select(x => x.FileId!.Value).Distinct().ToArray();
        var files = (await documentRepository.ListStoredFilesAsync(fileIds, cancellationToken)).ToDictionary(x => x.Id);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        return DocumentResult<IReadOnlyList<EmployeeDocumentSummary>>.Success(documents
            .Where(x => types.ContainsKey(x.DocumentTypeId))
            .Select(x => ToDocumentSummary(x, types[x.DocumentTypeId], x.FileId is not null && files.TryGetValue(x.FileId.Value, out var file) ? file : null, today))
            .OrderBy(x => x.DocumentTypeName).ThenByDescending(x => x.ValidUntil)
            .ToArray());
    }

    public async Task<DocumentResult<IReadOnlyList<MissingDocumentSummary>>> ListMissingDocumentsAsync(Guid userId, Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await personnelRepository.FindEmployeeAsync(employeeId, cancellationToken);
        if (employee is null) return DocumentResult<IReadOnlyList<MissingDocumentSummary>>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, employee.CompanyId, cancellationToken)) return DocumentResult<IReadOnlyList<MissingDocumentSummary>>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var types = (await documentRepository.ListDocumentTypesAsync(cancellationToken)).Where(x => x.IsActive).ToArray();
        var requirements = await documentRepository.ListRequirementsForEmployeeTypeAsync(employee.EmployeeTypeId, today, cancellationToken);
        var requiredTypeIds = types.Where(x => x.RequiredByDefault).Select(x => x.Id).Concat(requirements.Where(x => x.IsRequired).Select(x => x.DocumentTypeId)).ToHashSet();
        var documents = await documentRepository.ListEmployeeDocumentsAsync(employeeId, cancellationToken);
        var byType = documents.Where(x => !EmployeeDocumentStatuses.IsTerminal(x.Status)).GroupBy(x => x.DocumentTypeId).ToDictionary(x => x.Key, x => x.ToArray());

        var missing = new List<MissingDocumentSummary>();
        foreach (var type in types.Where(x => requiredTypeIds.Contains(x.Id)))
        {
            var satisfied = byType.TryGetValue(type.Id, out var existing) && existing.Any(x => EffectiveStatus(x, type, today) != EmployeeDocumentStatuses.Expired);
            if (!satisfied) missing.Add(new MissingDocumentSummary(type.Id, type.Code, type.Name, type.FileRequired, type.DocumentNumberRequired, type.ExpirationRequired));
        }
        return DocumentResult<IReadOnlyList<MissingDocumentSummary>>.Success(missing.OrderBy(x => x.Name).ToArray());
    }

    public Task<DocumentResult<EmployeeDocumentSummary>> UploadAsync(Guid userId, Guid employeeId, UploadEmployeeDocumentRequest request, CancellationToken cancellationToken) =>
        CreateDocumentAsync(userId, employeeId, request, null, cancellationToken);

    public async Task<DocumentResult<EmployeeDocumentSummary>> RenewAsync(Guid userId, Guid documentId, UploadEmployeeDocumentRequest request, CancellationToken cancellationToken)
    {
        var existing = await documentRepository.FindEmployeeDocumentAsync(documentId, cancellationToken);
        if (existing is null) return DocumentResult<EmployeeDocumentSummary>.Failure("DOCUMENT_NOT_FOUND", "Belge bulunamadı.");
        if (EmployeeDocumentStatuses.IsTerminal(existing.Status)) return DocumentResult<EmployeeDocumentSummary>.Failure("DOCUMENT_RENEWAL_NOT_ALLOWED", "Arşivlenmiş veya iptal edilmiş belge yenilenemez.");
        var normalized = request with { DocumentTypeId = existing.DocumentTypeId };
        return await CreateDocumentAsync(userId, existing.EmployeeId, normalized, existing, cancellationToken);
    }

    public async Task<DocumentResult<EmployeeDocumentSummary>> CancelAsync(Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await documentRepository.FindEmployeeDocumentAsync(documentId, cancellationToken);
        if (document is null) return DocumentResult<EmployeeDocumentSummary>.Failure("DOCUMENT_NOT_FOUND", "Belge bulunamadı.");
        var employee = await personnelRepository.FindEmployeeAsync(document.EmployeeId, cancellationToken);
        if (employee is null) return DocumentResult<EmployeeDocumentSummary>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, employee.CompanyId, cancellationToken)) return DocumentResult<EmployeeDocumentSummary>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");
        if (EmployeeDocumentStatuses.IsTerminal(document.Status)) return DocumentResult<EmployeeDocumentSummary>.Failure("DOCUMENT_CANCELLATION_NOT_ALLOWED", "Belge zaten arşivlenmiş veya iptal edilmiş.");

        document.Cancel(timeProvider.GetUtcNow(), userId);
        await documentRepository.SaveChangesAsync(cancellationToken);
        var type = await documentRepository.FindDocumentTypeAsync(document.DocumentTypeId, cancellationToken);
        var file = document.FileId is null ? null : await documentRepository.FindStoredFileAsync(document.FileId.Value, cancellationToken);
        return DocumentResult<EmployeeDocumentSummary>.Success(ToDocumentSummary(document, type!, file, DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime)));
    }

    public async Task<DocumentResult<DocumentFileDownload>> OpenFileAsync(Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await documentRepository.FindEmployeeDocumentAsync(documentId, cancellationToken);
        if (document is null) return DocumentResult<DocumentFileDownload>.Failure("DOCUMENT_NOT_FOUND", "Belge bulunamadı.");
        var employee = await personnelRepository.FindEmployeeAsync(document.EmployeeId, cancellationToken);
        if (employee is null) return DocumentResult<DocumentFileDownload>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, employee.CompanyId, cancellationToken)) return DocumentResult<DocumentFileDownload>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");
        if (document.FileId is null) return DocumentResult<DocumentFileDownload>.Failure("FILE_NOT_FOUND", "Bu belgeye bağlı dosya yok.");
        var file = await documentRepository.FindStoredFileAsync(document.FileId.Value, cancellationToken);
        if (file is null || file.Status != StoredFileStatuses.Active) return DocumentResult<DocumentFileDownload>.Failure("FILE_NOT_FOUND", "Dosya bulunamadı veya kullanıma hazır değil.");
        var stream = await fileStorage.OpenReadAsync(file.StorageKey, cancellationToken);
        return stream is null
            ? DocumentResult<DocumentFileDownload>.Failure("FILE_NOT_FOUND", "Dosya depolama alanında bulunamadı.")
            : DocumentResult<DocumentFileDownload>.Success(new DocumentFileDownload(stream, file.ContentType, file.OriginalName));
    }

    private async Task<DocumentResult<EmployeeDocumentSummary>> CreateDocumentAsync(Guid userId, Guid employeeId, UploadEmployeeDocumentRequest request, EmployeeDocument? replacedDocument, CancellationToken cancellationToken)
    {
        var employee = await personnelRepository.FindEmployeeAsync(employeeId, cancellationToken);
        if (employee is null) return DocumentResult<EmployeeDocumentSummary>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, employee.CompanyId, cancellationToken)) return DocumentResult<EmployeeDocumentSummary>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");

        var type = await documentRepository.FindDocumentTypeAsync(request.DocumentTypeId, cancellationToken);
        if (type is null) return DocumentResult<EmployeeDocumentSummary>.Failure("DOCUMENT_TYPE_NOT_FOUND", "Belge türü bulunamadı.");
        if (!type.IsActive) return DocumentResult<EmployeeDocumentSummary>.Failure("DOCUMENT_TYPE_INACTIVE", "Belge türü pasif.");
        if (type.DocumentNumberRequired && string.IsNullOrWhiteSpace(request.DocumentNumber)) return DocumentResult<EmployeeDocumentSummary>.Failure("DOCUMENT_NUMBER_REQUIRED", "Belge numarası zorunludur.");
        if (type.FileRequired && request.File is null) return DocumentResult<EmployeeDocumentSummary>.Failure("DOCUMENT_FILE_REQUIRED", "Belge dosyası zorunludur.");

        var validFrom = request.ValidFrom ?? request.IssueDate;
        var validUntil = request.ValidUntil;
        if (validUntil is null && type.DefaultValidityDays is not null && validFrom is not null) validUntil = validFrom.Value.AddDays(type.DefaultValidityDays.Value);
        if (type.ExpirationRequired && validUntil is null) return DocumentResult<EmployeeDocumentSummary>.Failure("DOCUMENT_EXPIRATION_REQUIRED", "Belge için geçerlilik bitiş tarihi zorunludur.");
        if (validUntil is not null && validFrom is not null && validUntil < validFrom) return DocumentResult<EmployeeDocumentSummary>.Failure("DOCUMENT_DATE_INVALID", "Belge geçerlilik tarihleri hatalı.");
        if (request.IssueDate is not null && validUntil is not null && validUntil < request.IssueDate) return DocumentResult<EmployeeDocumentSummary>.Failure("DOCUMENT_DATE_INVALID", "Belge bitiş tarihi düzenlenme tarihinden önce olamaz.");

        if (!type.MultipleAllowed && await documentRepository.HasActiveDocumentOfTypeAsync(employeeId, type.Id, replacedDocument?.Id, cancellationToken))
            return DocumentResult<EmployeeDocumentSummary>.Failure("DOCUMENT_MULTIPLE_NOT_ALLOWED", "Bu belge türü için birden fazla aktif kayıt oluşturulamaz.");

        StoredFile? storedFile = null;
        if (request.File is not null)
        {
            var prepared = await PrepareFileAsync(request.File, cancellationToken);
            if (!prepared.Succeeded || prepared.Value is null) return DocumentResult<EmployeeDocumentSummary>.Failure(prepared.ErrorCode!, prepared.ErrorMessage!);
            var now = timeProvider.GetUtcNow();
            var storageKey = $"documents/{now:yyyy/MM}/{Guid.NewGuid():N}{prepared.Value.Extension}";
            storedFile = StoredFile.CreatePending(prepared.Value.OriginalName, storageKey, prepared.Value.ContentType, prepared.Value.Extension, prepared.Value.Bytes.LongLength, prepared.Value.Sha256, fileStorage.ProviderCode, userId, now);
            documentRepository.AddStoredFile(storedFile);
            await documentRepository.SaveChangesAsync(cancellationToken);
            try
            {
                await fileStorage.WriteAsync(storageKey, prepared.Value.Bytes, cancellationToken);
            }
            catch
            {
                storedFile.MarkFailed();
                await documentRepository.SaveChangesAsync(cancellationToken);
                return DocumentResult<EmployeeDocumentSummary>.Failure("FILE_STORAGE_FAILED", "Dosya güvenli depolama alanına yazılamadı.");
            }
            storedFile.Activate();
        }

        try
        {
            var now = timeProvider.GetUtcNow();
            var document = EmployeeDocument.Create(employeeId, type.Id, storedFile?.Id, request.DocumentNumber, request.IssueDate, validFrom, validUntil, request.IssuingAuthority, request.CountryCode, request.Notes, replacedDocument?.Id, now, userId);
            if (replacedDocument is not null) replacedDocument.Archive(now, userId);
            documentRepository.AddEmployeeDocument(document);
            await documentRepository.SaveChangesAsync(cancellationToken);
            return DocumentResult<EmployeeDocumentSummary>.Success(ToDocumentSummary(document, type, storedFile, DateOnly.FromDateTime(now.UtcDateTime)));
        }
        catch (ArgumentException)
        {
            return DocumentResult<EmployeeDocumentSummary>.Failure("DOCUMENT_DATA_INVALID", "Belge bilgileri geçersiz.");
        }
    }

    private async Task<DocumentResult<PreparedFile>> PrepareFileAsync(DocumentUploadFile file, CancellationToken cancellationToken)
    {
        if (file.Length <= 0) return DocumentResult<PreparedFile>.Failure("FILE_EMPTY", "Dosya boş.");
        if (file.Length > filePolicy.MaxBytes) return DocumentResult<PreparedFile>.Failure("FILE_SIZE_LIMIT_EXCEEDED", $"Dosya boyutu en fazla {filePolicy.MaxBytes / 1024 / 1024} MB olabilir.");
        var originalName = Path.GetFileName(file.OriginalName);
        if (string.IsNullOrWhiteSpace(originalName) || originalName.Length > 255) return DocumentResult<PreparedFile>.Failure("FILE_NAME_INVALID", "Dosya adı geçersiz.");
        var extension = Path.GetExtension(originalName).ToLowerInvariant();
        var expectedContentType = extension switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => null
        };
        if (expectedContentType is null) return DocumentResult<PreparedFile>.Failure("FILE_TYPE_NOT_ALLOWED", "Yalnız PDF, JPG/JPEG ve PNG dosyaları yüklenebilir.");
        if (!string.Equals(file.ContentType, expectedContentType, StringComparison.OrdinalIgnoreCase)) return DocumentResult<PreparedFile>.Failure("FILE_SECURITY_CHECK_FAILED", "Dosya uzantısı ile MIME türü eşleşmiyor.");

        await using var buffer = new MemoryStream((int)Math.Min(file.Length, filePolicy.MaxBytes));
        var chunk = new byte[81920];
        while (true)
        {
            var read = await file.Content.ReadAsync(chunk, cancellationToken);
            if (read == 0) break;
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            if (buffer.Length > filePolicy.MaxBytes) return DocumentResult<PreparedFile>.Failure("FILE_SIZE_LIMIT_EXCEEDED", "Dosya boyutu izin verilen sınırı aşıyor.");
        }
        var bytes = buffer.ToArray();
        if (!SignatureMatches(extension, bytes)) return DocumentResult<PreparedFile>.Failure("FILE_SECURITY_CHECK_FAILED", "Dosyanın gerçek içeriği izin verilen formatla eşleşmiyor.");
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return DocumentResult<PreparedFile>.Success(new PreparedFile(originalName, expectedContentType, extension, bytes, sha256));
    }

    private static bool SignatureMatches(string extension, byte[] bytes)
    {
        if (extension == ".pdf") return bytes.Length >= 5 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46 && bytes[4] == 0x2D;
        if (extension is ".jpg" or ".jpeg") return bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        return bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;
    }

    private static string EffectiveStatus(EmployeeDocument document, DocumentType type, DateOnly today)
    {
        if (EmployeeDocumentStatuses.IsTerminal(document.Status)) return document.Status;
        if (document.ValidUntil is null) return EmployeeDocumentStatuses.Valid;
        if (document.ValidUntil < today) return EmployeeDocumentStatuses.Expired;
        var threshold = type.ReminderDays().DefaultIfEmpty(30).Max();
        return document.ValidUntil <= today.AddDays(threshold) ? EmployeeDocumentStatuses.Expiring : EmployeeDocumentStatuses.Valid;
    }

    private static DocumentTypeSummary ToTypeSummary(DocumentType type, IEnumerable<DocumentTypeEmployeeTypeRequirement> requirements) =>
        new(type.Id, type.Code, type.Name, type.Description, type.RequiredByDefault, type.ExpirationRequired, type.DefaultValidityDays, type.FileRequired,
            type.DocumentNumberRequired, type.MultipleAllowed, type.ReminderDays(), type.IsActive, type.DisplayOrder,
            requirements.Where(x => x.DocumentTypeId == type.Id && x.IsRequired).Select(x => x.EmployeeTypeId).Distinct().ToArray());

    private static EmployeeDocumentSummary ToDocumentSummary(EmployeeDocument document, DocumentType type, StoredFile? file, DateOnly today) =>
        new(document.Id, document.EmployeeId, document.DocumentTypeId, type.Code, type.Name, document.DocumentNumber, document.IssueDate, document.ValidFrom, document.ValidUntil,
            EffectiveStatus(document, type, today), file?.OriginalName, file?.ContentType, file?.SizeBytes, document.ReplacesDocumentId, document.Version);

    private Task<bool> CanAccessCompanyAsync(Guid userId, Guid companyId, CancellationToken cancellationToken) => accessControlService.HasScopeAsync(userId, ScopeTypes.Company, companyId, cancellationToken);

    private sealed record PreparedFile(string OriginalName, string ContentType, string Extension, byte[] Bytes, string Sha256);
}
