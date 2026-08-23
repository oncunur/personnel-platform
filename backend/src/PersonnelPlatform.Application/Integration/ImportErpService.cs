using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Application.Organization;
using PersonnelPlatform.Domain.Integration;

namespace PersonnelPlatform.Application.Integration;

public sealed class ImportErpService(
    IImportErpRepository repository,
    IIntegrationRepository integrationRepository,
    IOrganizationRepository organizationRepository,
    AccessControlService accessControlService,
    TimeProvider timeProvider)
{
    private static readonly IReadOnlyDictionary<string, string[]> TargetFields = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        [ImportTargetTypes.IntegrationMapping] = ["ENTITY_TYPE", "EXTERNAL_CODE", "INTERNAL_ENTITY_ID"],
        [ImportTargetTypes.ErpAccountMapping] = ["COST_CATEGORY", "ACCOUNT_CODE"]
    };

    public async Task<IntegrationResult<ImportUploadSummary>> UploadImportAsync(Guid userId, Guid companyId, Guid integrationSystemId, string targetType, string fileName, ReadOnlyMemory<byte> content, CancellationToken ct)
    {
        var systemResult = await ResolveSystemAsync(userId, companyId, integrationSystemId, ct);
        if (!systemResult.Succeeded || systemResult.Value is null) return IntegrationResult<ImportUploadSummary>.Failure(systemResult.ErrorCode!, systemResult.ErrorMessage!);
        var target = Normalize(targetType);
        if (!ImportTargetTypes.IsKnown(target)) return IntegrationResult<ImportUploadSummary>.Failure("IMPORT_TARGET_INVALID", "Import hedef türü geçersiz.");
        if (target == ImportTargetTypes.ErpAccountMapping && systemResult.Value.SystemType != IntegrationSystemTypes.Erp)
            return IntegrationResult<ImportUploadSummary>.Failure("ERP_SYSTEM_REQUIRED", "ERP account mapping importu için ERP tipi entegrasyon sistemi seçilmelidir.");

        SpreadsheetData data;
        try { data = SpreadsheetImportReader.ReadXlsx(fileName, content); }
        catch (InvalidDataException ex) { return IntegrationResult<ImportUploadSummary>.Failure("IMPORT_FILE_INVALID", ex.Message); }
        catch { return IntegrationResult<ImportUploadSummary>.Failure("IMPORT_FILE_INVALID", "Excel dosyası okunamadı."); }

        var now = timeProvider.GetUtcNow();
        var hash = Convert.ToHexString(SHA256.HashData(content.Span));
        ImportJob job;
        try { job = ImportJob.Create(companyId, integrationSystemId, target, Path.GetFileName(fileName), hash, JsonSerializer.Serialize(data.Headers), data.Rows.Count, now, userId); }
        catch (ArgumentException) { return IntegrationResult<ImportUploadSummary>.Failure("IMPORT_JOB_INVALID", "Import iş bilgileri geçersiz."); }
        repository.AddImportJob(job);
        repository.AddImportRows(data.Rows.Select(x => ImportRow.Create(job.Id, x.RowNumber, JsonSerializer.Serialize(x.Values))));
        await repository.SaveChangesAsync(ct);

        var suggested = SuggestMapping(target, data.Headers);
        var preview = data.Rows.Take(20).Select(x => new SpreadsheetPreviewRow(x.RowNumber, x.Values)).ToArray();
        return IntegrationResult<ImportUploadSummary>.Success(new ImportUploadSummary(MapJob(job), suggested, preview));
    }

    public async Task<IntegrationResult<IReadOnlyList<ImportJobSummary>>> ListImportsAsync(Guid userId, Guid? companyId, int take, CancellationToken ct)
    {
        var access = await ResolveAccessAsync(userId, ct);
        if (companyId is not null && !access.Global && !access.CompanyIds.Contains(companyId.Value))
            return IntegrationResult<IReadOnlyList<ImportJobSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var rows = await repository.ListImportJobsAsync(access.Global, access.CompanyIds, companyId, Math.Clamp(take, 1, 200), ct);
        return IntegrationResult<IReadOnlyList<ImportJobSummary>>.Success(rows.Select(MapJob).ToArray());
    }

    public async Task<IntegrationResult<ImportJobSummary>> GetImportAsync(Guid userId, Guid jobId, CancellationToken ct)
    {
        var job = await repository.FindImportJobAsync(jobId, ct);
        if (job is null) return IntegrationResult<ImportJobSummary>.Failure("IMPORT_JOB_NOT_FOUND", "Import işi bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, job.CompanyId, ct)) return IntegrationResult<ImportJobSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return IntegrationResult<ImportJobSummary>.Success(MapJob(job));
    }

    public async Task<IntegrationResult<IReadOnlyList<ImportRowSummary>>> ListImportRowsAsync(Guid userId, Guid jobId, bool errorsOnly, int take, CancellationToken ct)
    {
        var job = await repository.FindImportJobAsync(jobId, ct);
        if (job is null) return IntegrationResult<IReadOnlyList<ImportRowSummary>>.Failure("IMPORT_JOB_NOT_FOUND", "Import işi bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, job.CompanyId, ct)) return IntegrationResult<IReadOnlyList<ImportRowSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var rows = await repository.ListImportRowsAsync(job.Id, errorsOnly, Math.Clamp(take, 1, 1000), ct);
        return IntegrationResult<IReadOnlyList<ImportRowSummary>>.Success(rows.Select(MapRow).ToArray());
    }

    public async Task<IntegrationResult<ImportValidationSummary>> ApplyImportMappingAsync(Guid userId, Guid jobId, ApplyImportMappingRequest request, CancellationToken ct)
    {
        var job = await repository.FindImportJobAsync(jobId, ct);
        if (job is null) return IntegrationResult<ImportValidationSummary>.Failure("IMPORT_JOB_NOT_FOUND", "Import işi bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, job.CompanyId, ct)) return IntegrationResult<ImportValidationSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (job.Version != request.Version) return IntegrationResult<ImportValidationSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Import işi başka bir işlem tarafından değiştirilmiş.");
        var mappingError = ValidateMapping(job.TargetType, Headers(job), request.Mapping);
        if (mappingError is not null) return IntegrationResult<ImportValidationSummary>.Failure(mappingError.Value.Code, mappingError.Value.Message);

        try { job.ApplyMapping(JsonSerializer.Serialize(NormalizeMapping(request.Mapping)), timeProvider.GetUtcNow(), userId); }
        catch (InvalidOperationException) { return IntegrationResult<ImportValidationSummary>.Failure("IMPORT_MAPPING_LOCKED", "Bu import işi için mapping artık değiştirilemez."); }
        await repository.SaveChangesAsync(ct);

        var rows = await repository.ListImportRowsAsync(job.Id, false, SpreadsheetImportReader.MaxRows, ct);
        var errors = new List<ImportRowSummary>();
        var valid = 0;
        var mapping = Mapping(job);
        foreach (var row in rows)
        {
            var error = await ValidateRowAsync(job, row, mapping, ct);
            if (error is null) valid++;
            else errors.Add(new ImportRowSummary(row.RowNumber, "INVALID", error.Value.Code, error.Value.Message, null, null, Values(row)));
        }
        return IntegrationResult<ImportValidationSummary>.Success(new ImportValidationSummary(MapJob(job), valid, errors.Count, errors.Take(200).ToArray()));
    }

    public async Task<IntegrationResult<ImportProcessSummary>> ProcessImportAsync(Guid userId, Guid jobId, ProcessImportRequest request, CancellationToken ct)
    {
        var job = await repository.FindImportJobAsync(jobId, ct);
        if (job is null) return IntegrationResult<ImportProcessSummary>.Failure("IMPORT_JOB_NOT_FOUND", "Import işi bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, job.CompanyId, ct)) return IntegrationResult<ImportProcessSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (job.Version != request.Version) return IntegrationResult<ImportProcessSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Import işi başka bir işlem tarafından değiştirilmiş.");
        if (job.Status != ImportJobStatuses.Ready) return IntegrationResult<ImportProcessSummary>.Failure("IMPORT_NOT_READY", "Import mapping doğrulanmadan işlenemez.");
        var mapping = Mapping(job);
        var mappingError = ValidateMapping(job.TargetType, Headers(job), mapping);
        if (mappingError is not null) return IntegrationResult<ImportProcessSummary>.Failure(mappingError.Value.Code, mappingError.Value.Message);

        var now = timeProvider.GetUtcNow();
        job.Begin(now, userId);
        await repository.SaveChangesAsync(ct);
        var rows = await repository.ListImportRowsAsync(job.Id, false, SpreadsheetImportReader.MaxRows, ct);
        var success = 0;
        var errors = 0;

        foreach (var row in rows)
        {
            if (row.Status != ImportRowStatuses.Preview) continue;
            var validation = await ValidateRowAsync(job, row, mapping, ct);
            if (validation is not null)
            {
                row.MarkError(validation.Value.Code, validation.Value.Message, timeProvider.GetUtcNow()); errors++; await repository.SaveChangesAsync(ct); continue;
            }
            try
            {
                var values = ProjectValues(row, mapping);
                Guid entityId;
                string entityType;
                if (job.TargetType == ImportTargetTypes.IntegrationMapping)
                {
                    var type = Normalize(values["ENTITY_TYPE"]);
                    var targetId = Guid.Parse(values["INTERNAL_ENTITY_ID"]);
                    var entity = ExternalEntityMapping.Create(job.CompanyId, job.IntegrationSystemId, type, values["EXTERNAL_CODE"], targetId, timeProvider.GetUtcNow(), userId);
                    if (!await repository.TryInsertExternalMappingAsync(entity, ct))
                    {
                        row.MarkError("INTEGRATION_MAPPING_EXISTS", "Aynı external mapping zaten tanımlı.", timeProvider.GetUtcNow()); errors++; await repository.SaveChangesAsync(ct); continue;
                    }
                    entityId = entity.Id; entityType = "INTEGRATION_MAPPING";
                }
                else
                {
                    var entity = ErpAccountMapping.Create(job.CompanyId, job.IntegrationSystemId, values["COST_CATEGORY"], values["ACCOUNT_CODE"], values.GetValueOrDefault("COUNTER_ACCOUNT_CODE"), timeProvider.GetUtcNow(), userId);
                    if (!await repository.TryInsertErpAccountMappingAsync(entity, ct))
                    {
                        row.MarkError("ERP_ACCOUNT_MAPPING_EXISTS", "Bu cost category için account mapping zaten tanımlı.", timeProvider.GetUtcNow()); errors++; await repository.SaveChangesAsync(ct); continue;
                    }
                    entityId = entity.Id; entityType = "ERP_ACCOUNT_MAPPING";
                }
                row.MarkImported(entityType, entityId, timeProvider.GetUtcNow()); success++; await repository.SaveChangesAsync(ct);
            }
            catch (Exception)
            {
                row.MarkError("IMPORT_ROW_PROCESSING_FAILED", "Satır işlenirken beklenmeyen bir hata oluştu.", timeProvider.GetUtcNow()); errors++; await repository.SaveChangesAsync(ct);
            }
        }

        job.Finish(success, errors, timeProvider.GetUtcNow(), userId);
        await repository.SaveChangesAsync(ct);
        return IntegrationResult<ImportProcessSummary>.Success(new ImportProcessSummary(MapJob(job), success, errors));
    }

    public async Task<IntegrationResult<IReadOnlyList<ErpAccountMappingSummary>>> ListErpAccountMappingsAsync(Guid userId, Guid systemId, CancellationToken ct)
    {
        var system = await integrationRepository.FindSystemAsync(systemId, ct);
        if (system is null || system.SystemType != IntegrationSystemTypes.Erp) return IntegrationResult<IReadOnlyList<ErpAccountMappingSummary>>.Failure("ERP_SYSTEM_NOT_FOUND", "ERP entegrasyon sistemi bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, system.CompanyId, ct)) return IntegrationResult<IReadOnlyList<ErpAccountMappingSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return IntegrationResult<IReadOnlyList<ErpAccountMappingSummary>>.Success((await repository.ListErpAccountMappingsAsync(systemId, ct)).Select(MapAccount).ToArray());
    }

    public async Task<IntegrationResult<ErpAccountMappingSummary>> CreateErpAccountMappingAsync(Guid userId, CreateErpAccountMappingRequest request, CancellationToken ct)
    {
        var systemResult = await ResolveSystemAsync(userId, request.CompanyId, request.IntegrationSystemId, ct);
        if (!systemResult.Succeeded || systemResult.Value is null) return IntegrationResult<ErpAccountMappingSummary>.Failure(systemResult.ErrorCode!, systemResult.ErrorMessage!);
        if (systemResult.Value.SystemType != IntegrationSystemTypes.Erp) return IntegrationResult<ErpAccountMappingSummary>.Failure("ERP_SYSTEM_REQUIRED", "Account mapping yalnız ERP entegrasyon sistemi için tanımlanabilir.");
        var category = Normalize(request.CostCategory);
        if (await repository.ErpAccountMappingExistsAsync(request.IntegrationSystemId, category, ct)) return IntegrationResult<ErpAccountMappingSummary>.Failure("ERP_ACCOUNT_MAPPING_EXISTS", "Bu cost category için account mapping zaten tanımlı.");
        try
        {
            var row = ErpAccountMapping.Create(request.CompanyId, request.IntegrationSystemId, category, request.AccountCode, request.CounterAccountCode, timeProvider.GetUtcNow(), userId);
            if (!await repository.TryInsertErpAccountMappingAsync(row, ct)) return IntegrationResult<ErpAccountMappingSummary>.Failure("ERP_ACCOUNT_MAPPING_EXISTS", "Bu cost category için account mapping zaten tanımlı.");
            return IntegrationResult<ErpAccountMappingSummary>.Success(MapAccount(row));
        }
        catch (ArgumentException) { return IntegrationResult<ErpAccountMappingSummary>.Failure("ERP_ACCOUNT_MAPPING_INVALID", "ERP account mapping bilgileri geçersiz."); }
    }

    public async Task<IntegrationResult<ErpAccountMappingSummary>> UpdateErpAccountMappingAsync(Guid userId, Guid mappingId, UpdateErpAccountMappingRequest request, CancellationToken ct)
    {
        var row = await repository.FindErpAccountMappingAsync(mappingId, ct);
        if (row is null) return IntegrationResult<ErpAccountMappingSummary>.Failure("ERP_ACCOUNT_MAPPING_NOT_FOUND", "ERP account mapping bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, row.CompanyId, ct)) return IntegrationResult<ErpAccountMappingSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (row.Version != request.Version) return IntegrationResult<ErpAccountMappingSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "ERP account mapping başka bir kullanıcı tarafından değiştirilmiş.");
        try { row.Change(request.AccountCode, request.CounterAccountCode, request.IsActive, timeProvider.GetUtcNow(), userId); await repository.SaveChangesAsync(ct); return IntegrationResult<ErpAccountMappingSummary>.Success(MapAccount(row)); }
        catch (ArgumentException) { return IntegrationResult<ErpAccountMappingSummary>.Failure("ERP_ACCOUNT_MAPPING_INVALID", "ERP account mapping bilgileri geçersiz."); }
    }

    public async Task<IntegrationResult<ErpBatchSummary>> CreateErpBatchAsync(Guid userId, CreateErpBatchRequest request, CancellationToken ct)
    {
        var systemResult = await ResolveSystemAsync(userId, request.CompanyId, request.IntegrationSystemId, ct);
        if (!systemResult.Succeeded || systemResult.Value is null) return IntegrationResult<ErpBatchSummary>.Failure(systemResult.ErrorCode!, systemResult.ErrorMessage!);
        if (systemResult.Value.SystemType != IntegrationSystemTypes.Erp) return IntegrationResult<ErpBatchSummary>.Failure("ERP_SYSTEM_REQUIRED", "ERP export batch için ERP entegrasyon sistemi seçilmelidir.");
        if (request.ToDate < request.FromDate || request.ToDate.DayNumber - request.FromDate.DayNumber > 730) return IntegrationResult<ErpBatchSummary>.Failure("ERP_DATE_RANGE_INVALID", "ERP batch tarih aralığı geçersiz.");

        var entries = await repository.ListEligibleCostEntriesAsync(request.CompanyId, request.IntegrationSystemId, request.FromDate, request.ToDate, ct);
        if (entries.Count == 0) return IntegrationResult<ErpBatchSummary>.Failure("ERP_NO_ELIGIBLE_COST", "Bu tarih aralığında ERP'ye gönderilecek yeni maliyet kaydı bulunmuyor.");
        var mappings = (await repository.ListErpAccountMappingsAsync(request.IntegrationSystemId, ct)).Where(x => x.IsActive).ToDictionary(x => x.CostCategory, StringComparer.OrdinalIgnoreCase);
        var missing = entries.Select(x => x.Category).Distinct(StringComparer.OrdinalIgnoreCase).Where(x => !mappings.ContainsKey(x)).OrderBy(x => x).ToArray();
        if (missing.Length > 0) return IntegrationResult<ErpBatchSummary>.Failure("ERP_ACCOUNT_MAPPING_MISSING", $"Account mapping eksik cost category: {string.Join(", ", missing)}");

        var batch = ErpExportBatch.Create(request.CompanyId, request.IntegrationSystemId, request.FromDate, request.ToDate, timeProvider.GetUtcNow(), userId);
        var lines = entries.Select(x =>
        {
            var account = mappings[x.Category];
            return ErpExportLine.Create(batch.Id, x.Id, x.Id.ToString("N").ToUpperInvariant(), x.SourceType, x.SourceId, x.EmployeeId, x.ProjectId, x.CostCenterId, x.CostDate, x.Category, account.AccountCode, account.CounterAccountCode, x.Amount, x.Currency);
        }).ToArray();
        repository.AddErpBatch(batch); repository.AddErpLines(lines); await repository.SaveChangesAsync(ct);
        return IntegrationResult<ErpBatchSummary>.Success(MapBatch(batch, lines));
    }

    public async Task<IntegrationResult<IReadOnlyList<ErpBatchSummary>>> ListErpBatchesAsync(Guid userId, Guid? companyId, Guid? systemId, int take, CancellationToken ct)
    {
        var access = await ResolveAccessAsync(userId, ct);
        if (companyId is not null && !access.Global && !access.CompanyIds.Contains(companyId.Value)) return IntegrationResult<IReadOnlyList<ErpBatchSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var batches = await repository.ListErpBatchesAsync(access.Global, access.CompanyIds, companyId, systemId, Math.Clamp(take, 1, 200), ct);
        var summaries = new List<ErpBatchSummary>(batches.Count);
        foreach (var batch in batches) summaries.Add(MapBatch(batch, await repository.ListErpLinesAsync(batch.Id, ct)));
        return IntegrationResult<IReadOnlyList<ErpBatchSummary>>.Success(summaries);
    }

    public async Task<IntegrationResult<IReadOnlyList<ErpBatchLineSummary>>> ListErpBatchLinesAsync(Guid userId, Guid batchId, CancellationToken ct)
    {
        var batch = await repository.FindErpBatchAsync(batchId, ct);
        if (batch is null) return IntegrationResult<IReadOnlyList<ErpBatchLineSummary>>.Failure("ERP_BATCH_NOT_FOUND", "ERP batch bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, batch.CompanyId, ct)) return IntegrationResult<IReadOnlyList<ErpBatchLineSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return IntegrationResult<IReadOnlyList<ErpBatchLineSummary>>.Success((await repository.ListErpLinesAsync(batch.Id, ct)).Select(MapLine).ToArray());
    }

    public async Task<IntegrationResult<ErpBatchSummary>> SendErpBatchAsync(Guid userId, Guid batchId, ErpBatchActionRequest request, CancellationToken ct)
    {
        var batch = await repository.FindErpBatchAsync(batchId, ct);
        if (batch is null) return IntegrationResult<ErpBatchSummary>.Failure("ERP_BATCH_NOT_FOUND", "ERP batch bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, batch.CompanyId, ct)) return IntegrationResult<ErpBatchSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (batch.Version != request.Version) return IntegrationResult<ErpBatchSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "ERP batch başka bir işlem tarafından değiştirilmiş.");
        var lines = await repository.ListErpLinesAsync(batch.Id, ct);
        if (lines.Count == 0) return IntegrationResult<ErpBatchSummary>.Failure("ERP_BATCH_EMPTY", "ERP batch satır içermiyor.");
        try { foreach (var line in lines) line.MarkSent(); batch.MarkSent(timeProvider.GetUtcNow(), userId); await repository.SaveChangesAsync(ct); return IntegrationResult<ErpBatchSummary>.Success(MapBatch(batch, lines)); }
        catch (InvalidOperationException) { return IntegrationResult<ErpBatchSummary>.Failure("ERP_BATCH_INVALID_STATE", "Yalnız DRAFT ERP batch gönderilebilir."); }
    }

    public async Task<IntegrationResult<ErpExportFile>> DownloadErpBatchAsync(Guid userId, Guid batchId, CancellationToken ct)
    {
        var batch = await repository.FindErpBatchAsync(batchId, ct);
        if (batch is null) return IntegrationResult<ErpExportFile>.Failure("ERP_BATCH_NOT_FOUND", "ERP batch bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, batch.CompanyId, ct)) return IntegrationResult<ErpExportFile>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var lines = await repository.ListErpLinesAsync(batch.Id, ct);
        var sb = new StringBuilder("external_line_key,cost_date,source_type,source_id,cost_category,account_code,counter_account_code,amount,currency,employee_id,project_id,cost_center_id\r\n");
        foreach (var x in lines)
        {
            sb.Append(Csv(x.ExternalLineKey)).Append(',').Append(x.CostDate.ToString("yyyy-MM-dd")).Append(',').Append(Csv(x.SourceType)).Append(',').Append(x.SourceId).Append(',')
                .Append(Csv(x.CostCategory)).Append(',').Append(Csv(x.AccountCode)).Append(',').Append(Csv(x.CounterAccountCode)).Append(',').Append(x.SentAmount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(Csv(x.Currency)).Append(',').Append(x.EmployeeId?.ToString() ?? "").Append(',').Append(x.ProjectId?.ToString() ?? "").Append(',').Append(x.CostCenterId?.ToString() ?? "").Append("\r\n");
        }
        return IntegrationResult<ErpExportFile>.Success(new ErpExportFile(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv; charset=utf-8", $"erp-batch-{batch.Id:N}.csv"));
    }

    public async Task<IntegrationResult<ErpBatchSummary>> ReconcileErpBatchAsync(Guid userId, Guid batchId, ReconcileErpBatchRequest request, CancellationToken ct)
    {
        var batch = await repository.FindErpBatchAsync(batchId, ct);
        if (batch is null) return IntegrationResult<ErpBatchSummary>.Failure("ERP_BATCH_NOT_FOUND", "ERP batch bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, batch.CompanyId, ct)) return IntegrationResult<ErpBatchSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (batch.Version != request.Version) return IntegrationResult<ErpBatchSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "ERP batch başka bir işlem tarafından değiştirilmiş.");
        if (batch.Status is not (ErpBatchStatuses.Sent or ErpBatchStatuses.PartiallyAccepted or ErpBatchStatuses.Rejected)) return IntegrationResult<ErpBatchSummary>.Failure("ERP_BATCH_INVALID_STATE", "ERP batch reconciliation için SENT durumda olmalıdır.");
        if (request.Lines.Count == 0) return IntegrationResult<ErpBatchSummary>.Failure("ERP_RECONCILIATION_REQUIRED", "En az bir reconciliation satırı zorunludur.");
        if (request.Lines.GroupBy(x => x.ExternalLineKey, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1)) return IntegrationResult<ErpBatchSummary>.Failure("ERP_RECONCILIATION_DUPLICATE", "Aynı external line key birden fazla kez gönderilemez.");

        var lines = await repository.ListErpLinesAsync(batch.Id, ct);
        var byKey = lines.ToDictionary(x => x.ExternalLineKey, StringComparer.OrdinalIgnoreCase);
        var now = timeProvider.GetUtcNow();
        foreach (var input in request.Lines)
        {
            if (!byKey.TryGetValue(input.ExternalLineKey, out var line)) return IntegrationResult<ErpBatchSummary>.Failure("ERP_LINE_NOT_FOUND", $"Batch içinde line bulunamadı: {input.ExternalLineKey}");
            var status = Normalize(input.Status);
            if (status is not (ErpLineStatuses.Accepted or ErpLineStatuses.Rejected)) return IntegrationResult<ErpBatchSummary>.Failure("ERP_RECONCILIATION_STATUS_INVALID", "Line status ACCEPTED veya REJECTED olmalıdır.");
            var accepted = input.AcceptedAmount ?? (status == ErpLineStatuses.Accepted ? line.SentAmount : 0m);
            try { line.Reconcile(status, accepted, input.ExternalReference, input.ErrorCode, input.ErrorMessage, now); }
            catch (ArgumentException) { return IntegrationResult<ErpBatchSummary>.Failure("ERP_RECONCILIATION_INVALID", "Reconciliation satırı geçersiz."); }
            repository.AddReconciliationEvent(ErpReconciliationEvent.Create(batch.Id, line.Id, line.Status, line.SentAmount, line.AcceptedAmount, line.VarianceAmount ?? 0m, line.ExternalReference, line.ErrorCode, line.ErrorMessage, userId, now));
        }

        var allAcceptedZeroVariance = lines.All(x => x.Status == ErpLineStatuses.Accepted && (x.VarianceAmount ?? 0m) == 0m);
        var allRejected = lines.All(x => x.Status == ErpLineStatuses.Rejected);
        var batchStatus = allAcceptedZeroVariance ? ErpBatchStatuses.Accepted : allRejected ? ErpBatchStatuses.Rejected : ErpBatchStatuses.PartiallyAccepted;
        try { batch.MarkReconciled(batchStatus, now, userId); }
        catch (InvalidOperationException) { return IntegrationResult<ErpBatchSummary>.Failure("ERP_BATCH_INVALID_STATE", "ERP batch reconciliation durumuna uygun değil."); }
        await repository.SaveChangesAsync(ct);
        return IntegrationResult<ErpBatchSummary>.Success(MapBatch(batch, lines));
    }

    public async Task<IntegrationResult<ErpBatchSummary>> CloseErpBatchAsync(Guid userId, Guid batchId, ErpBatchActionRequest request, CancellationToken ct)
    {
        var batch = await repository.FindErpBatchAsync(batchId, ct);
        if (batch is null) return IntegrationResult<ErpBatchSummary>.Failure("ERP_BATCH_NOT_FOUND", "ERP batch bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, batch.CompanyId, ct)) return IntegrationResult<ErpBatchSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (batch.Version != request.Version) return IntegrationResult<ErpBatchSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "ERP batch başka bir işlem tarafından değiştirilmiş.");
        var lines = await repository.ListErpLinesAsync(batch.Id, ct);
        if (lines.Any(x => x.Status != ErpLineStatuses.Accepted || (x.VarianceAmount ?? 0m) != 0m)) return IntegrationResult<ErpBatchSummary>.Failure("ERP_RECONCILIATION_OPEN", "Batch kapanmadan önce tüm satırlar ACCEPTED ve amount variance sıfır olmalıdır.");
        try { batch.Close(timeProvider.GetUtcNow(), userId); await repository.SaveChangesAsync(ct); return IntegrationResult<ErpBatchSummary>.Success(MapBatch(batch, lines)); }
        catch (InvalidOperationException) { return IntegrationResult<ErpBatchSummary>.Failure("ERP_BATCH_NOT_ACCEPTED", "ERP batch ACCEPTED olmadan kapatılamaz."); }
    }

    private async Task<(string Code, string Message)?> ValidateRowAsync(ImportJob job, ImportRow row, IReadOnlyDictionary<string, string> mapping, CancellationToken ct)
    {
        var values = ProjectValues(row, mapping);
        if (job.TargetType == ImportTargetTypes.IntegrationMapping)
        {
            var type = Normalize(values.GetValueOrDefault("ENTITY_TYPE"));
            var external = values.GetValueOrDefault("EXTERNAL_CODE")?.Trim() ?? string.Empty;
            if (!IntegrationEntityTypes.IsKnown(type)) return ("INTEGRATION_MAPPING_TYPE_INVALID", "ENTITY_TYPE geçersiz.");
            if (string.IsNullOrWhiteSpace(external) || external.Length > 200) return ("INTEGRATION_EXTERNAL_CODE_INVALID", "EXTERNAL_CODE zorunlu ve en fazla 200 karakter olmalıdır.");
            if (!Guid.TryParse(values.GetValueOrDefault("INTERNAL_ENTITY_ID"), out var targetId) || targetId == Guid.Empty) return ("INTEGRATION_MAPPING_TARGET_INVALID", "INTERNAL_ENTITY_ID geçerli UUID olmalıdır.");
            if (await integrationRepository.MappingExistsAsync(job.IntegrationSystemId, type, external.Trim().ToUpperInvariant(), ct)) return ("INTEGRATION_MAPPING_EXISTS", "Aynı external mapping zaten tanımlı.");
            return await ValidateTargetAsync(job.CompanyId, type, targetId, ct);
        }
        var category = Normalize(values.GetValueOrDefault("COST_CATEGORY"));
        var account = values.GetValueOrDefault("ACCOUNT_CODE")?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(category) || category.Length > 50) return ("ERP_COST_CATEGORY_INVALID", "COST_CATEGORY zorunlu ve en fazla 50 karakter olmalıdır.");
        if (string.IsNullOrWhiteSpace(account) || account.Length > 100) return ("ERP_ACCOUNT_CODE_INVALID", "ACCOUNT_CODE zorunlu ve en fazla 100 karakter olmalıdır.");
        if (await repository.ErpAccountMappingExistsAsync(job.IntegrationSystemId, category, ct)) return ("ERP_ACCOUNT_MAPPING_EXISTS", "Bu cost category için account mapping zaten tanımlı.");
        var counter = values.GetValueOrDefault("COUNTER_ACCOUNT_CODE");
        if (!string.IsNullOrWhiteSpace(counter) && counter.Trim().Length > 100) return ("ERP_COUNTER_ACCOUNT_CODE_INVALID", "COUNTER_ACCOUNT_CODE en fazla 100 karakter olabilir.");
        return null;
    }

    private async Task<(string Code, string Message)?> ValidateTargetAsync(Guid companyId, string entityType, Guid id, CancellationToken ct)
    {
        switch (entityType)
        {
            case IntegrationEntityTypes.Employee: { var x = await integrationRepository.FindEmployeeAsync(id, ct); return x is null || x.CompanyId != companyId ? ("EMPLOYEE_NOT_FOUND", "Şirket kapsamındaki personel bulunamadı.") : null; }
            case IntegrationEntityTypes.Camp: { var x = await integrationRepository.FindCampAsync(id, ct); return x is null || x.CompanyId != companyId ? ("CAMP_NOT_FOUND", "Şirket kapsamındaki kamp bulunamadı.") : null; }
            case IntegrationEntityTypes.MealType: return await integrationRepository.FindMealTypeAsync(id, ct) is null ? ("MEAL_TYPE_NOT_FOUND", "Öğün türü bulunamadı.") : null;
            case IntegrationEntityTypes.Project: { var x = await organizationRepository.FindProjectAsync(id, ct); return x is null || x.CompanyId != companyId ? ("PROJECT_NOT_FOUND", "Şirket kapsamındaki proje bulunamadı.") : null; }
            case IntegrationEntityTypes.CostCenter: { var x = await organizationRepository.FindCostCenterAsync(id, ct); return x is null || x.CompanyId != companyId ? ("COST_CENTER_NOT_FOUND", "Şirket kapsamındaki cost center bulunamadı.") : null; }
            default: return null;
        }
    }

    private async Task<IntegrationResult<IntegrationSystemSummary>> ResolveSystemAsync(Guid userId, Guid companyId, Guid systemId, CancellationToken ct)
    {
        var system = await integrationRepository.FindSystemAsync(systemId, ct);
        if (system is null || system.CompanyId != companyId) return IntegrationResult<IntegrationSystemSummary>.Failure("INTEGRATION_SYSTEM_NOT_FOUND", "Şirket kapsamındaki entegrasyon sistemi bulunamadı.");
        if (!system.IsActive) return IntegrationResult<IntegrationSystemSummary>.Failure("INTEGRATION_SYSTEM_INACTIVE", "Pasif entegrasyon sistemi kullanılamaz.");
        if (!await CanAccessCompanyAsync(userId, companyId, ct)) return IntegrationResult<IntegrationSystemSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return IntegrationResult<IntegrationSystemSummary>.Success(new IntegrationSystemSummary(system.Id, system.CompanyId, system.Code, system.Name, system.SystemType, system.IsActive, system.Version));
    }

    private static (string Code, string Message)? ValidateMapping(string targetType, IReadOnlyList<string> headers, IReadOnlyDictionary<string, string> mapping)
    {
        if (!TargetFields.TryGetValue(targetType, out var required)) return ("IMPORT_TARGET_INVALID", "Import hedef türü geçersiz.");
        foreach (var field in required)
        {
            if (!mapping.TryGetValue(field, out var header) || string.IsNullOrWhiteSpace(header)) return ("IMPORT_MAPPING_REQUIRED", $"{field} için kolon eşlemesi zorunludur.");
            if (!headers.Contains(header, StringComparer.OrdinalIgnoreCase)) return ("IMPORT_MAPPING_COLUMN_NOT_FOUND", $"Excel kolonu bulunamadı: {header}");
        }
        foreach (var pair in mapping.Where(x => !string.IsNullOrWhiteSpace(x.Value)))
            if (!headers.Contains(pair.Value, StringComparer.OrdinalIgnoreCase)) return ("IMPORT_MAPPING_COLUMN_NOT_FOUND", $"Excel kolonu bulunamadı: {pair.Value}");
        if (mapping.Where(x => !string.IsNullOrWhiteSpace(x.Value)).GroupBy(x => x.Value, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1)) return ("IMPORT_MAPPING_DUPLICATE_COLUMN", "Aynı Excel kolonu birden fazla hedef alana eşlenemez.");
        return null;
    }

    private static IReadOnlyDictionary<string, string> SuggestMapping(string targetType, IReadOnlyList<string> headers)
    {
        var fields = targetType == ImportTargetTypes.IntegrationMapping ? new[] { "ENTITY_TYPE", "EXTERNAL_CODE", "INTERNAL_ENTITY_ID" } : new[] { "COST_CATEGORY", "ACCOUNT_CODE", "COUNTER_ACCOUNT_CODE" };
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            var normalized = Key(field);
            var match = headers.FirstOrDefault(x => Key(x) == normalized);
            if (match is not null) result[field] = match;
        }
        return result;
    }

    private static IReadOnlyDictionary<string, string> NormalizeMapping(IReadOnlyDictionary<string, string> source) =>
        source.Where(x => !string.IsNullOrWhiteSpace(x.Key) && !string.IsNullOrWhiteSpace(x.Value)).ToDictionary(x => Normalize(x.Key), x => x.Value.Trim(), StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, string> ProjectValues(ImportRow row, IReadOnlyDictionary<string, string> mapping)
    {
        var raw = Values(row);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in mapping) result[Normalize(pair.Key)] = raw.TryGetValue(pair.Value, out var value) ? value : string.Empty;
        return result;
    }

    private static ImportJobSummary MapJob(ImportJob x) => new(x.Id, x.CompanyId, x.IntegrationSystemId, x.TargetType, x.OriginalFileName, x.Status, Headers(x), Mapping(x), x.TotalRows, x.SuccessRows, x.ErrorRows, x.CreatedAt, x.CompletedAt, x.Version);
    private static ImportRowSummary MapRow(ImportRow x) => new(x.RowNumber, x.Status, x.ErrorCode, x.ErrorMessage, x.ProcessedEntityType, x.ProcessedEntityId, Values(x));
    private static ErpAccountMappingSummary MapAccount(ErpAccountMapping x) => new(x.Id, x.CompanyId, x.IntegrationSystemId, x.CostCategory, x.AccountCode, x.CounterAccountCode, x.IsActive, x.Version);
    private static ErpBatchLineSummary MapLine(ErpExportLine x) => new(x.Id, x.BatchId, x.CostEntryId, x.ExternalLineKey, x.SourceType, x.SourceId, x.EmployeeId, x.ProjectId, x.CostCenterId, x.CostDate, x.CostCategory, x.AccountCode, x.CounterAccountCode, x.SentAmount, x.Currency, x.Status, x.AcceptedAmount, x.VarianceAmount, x.ExternalReference, x.ErrorCode, x.ErrorMessage, x.ReconciledAt);
    private static ErpBatchSummary MapBatch(ErpExportBatch batch, IReadOnlyList<ErpExportLine> lines)
    {
        var totals = lines.GroupBy(x => x.Currency).OrderBy(x => x.Key).Select(g => new ErpCurrencyTotal(g.Key, g.Sum(x => x.SentAmount), g.Sum(x => x.AcceptedAmount ?? 0m), g.Sum(x => x.VarianceAmount ?? 0m))).ToArray();
        return new ErpBatchSummary(batch.Id, batch.CompanyId, batch.IntegrationSystemId, batch.FromDate, batch.ToDate, batch.Status, lines.Count, lines.Count(x => x.Status == ErpLineStatuses.Accepted), lines.Count(x => x.Status == ErpLineStatuses.Rejected), totals, batch.CreatedAt, batch.SentAt, batch.ReconciledAt, batch.ClosedAt, batch.Version);
    }
    private static IReadOnlyList<string> Headers(ImportJob x) => JsonSerializer.Deserialize<string[]>(x.HeadersJson) ?? [];
    private static IReadOnlyDictionary<string, string> Mapping(ImportJob x) => JsonSerializer.Deserialize<Dictionary<string, string>>(x.MappingJson) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static IReadOnlyDictionary<string, string> Values(ImportRow x) => JsonSerializer.Deserialize<Dictionary<string, string>>(x.RawJson) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static string Csv(string? value) { var text = value ?? string.Empty; return text.ContainsAny([',', '"', '\r', '\n']) ? $"\"{text.Replace("\"", "\"\"")}\"" : text; }
    private static string Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
    private static string Key(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    private async Task<bool> CanAccessCompanyAsync(Guid userId, Guid companyId, CancellationToken ct) => await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, companyId, ct);
    private async Task<(bool Global, HashSet<Guid> CompanyIds)> ResolveAccessAsync(Guid userId, CancellationToken ct)
    {
        var snapshot = await accessControlService.GetSnapshotAsync(userId, ct);
        return (snapshot.Scopes.Any(x => x.ScopeType == ScopeTypes.Global), snapshot.Scopes.Where(x => x.ScopeType == ScopeTypes.Company && x.ScopeId is not null).Select(x => x.ScopeId!.Value).ToHashSet());
    }
}

internal static class StringExtensions
{
    public static bool ContainsAny(this string value, IEnumerable<char> chars) => chars.Any(value.Contains);
}
