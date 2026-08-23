using System.Text.Json;
using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Application.Security;
using PersonnelPlatform.Domain.Migration;

namespace PersonnelPlatform.Application.Migration;

public sealed class MigrationService(
    IMigrationRepository repository,
    AccessControlService accessControl,
    ISensitiveDataProtector sensitiveDataProtector,
    TimeProvider timeProvider)
{
    public async Task<MigrationResult<MigrationRunSummary>> CreateRunAsync(Guid userId, CreateMigrationRunRequest request, CancellationToken ct)
    {
        var denied = await AuthorizeAsync(userId, MigrationPermissions.Manage, request.CompanyId, ct);
        if (denied is not null) return MigrationResult<MigrationRunSummary>.Failure(denied.Value.Code, denied.Value.Message);
        try
        {
            var run = MigrationRun.Create(request.CompanyId, request.SourceSystem, request.SourceObject, request.TargetEntity, request.SourceFileName, request.SourceContentHash, request.MappingHash, timeProvider.GetUtcNow(), userId);
            repository.AddRun(run);
            await repository.SaveChangesAsync(ct);
            return MigrationResult<MigrationRunSummary>.Success(Map(run));
        }
        catch (ArgumentException ex) { return MigrationResult<MigrationRunSummary>.Failure("MIGRATION_RUN_INVALID", ex.Message); }
    }

    public async Task<MigrationResult<IReadOnlyList<MigrationRunSummary>>> ListRunsAsync(Guid userId, Guid? companyId, int take, CancellationToken ct)
    {
        if (!await accessControl.HasPermissionAsync(userId, MigrationPermissions.View, ct)) return MigrationResult<IReadOnlyList<MigrationRunSummary>>.Failure("PERMISSION_DENIED", "Migration kayıtlarını görüntüleme yetkiniz yok.");
        var snapshot = await accessControl.GetSnapshotAsync(userId, ct);
        var global = snapshot.Scopes.Any(x => x.ScopeType == ScopeTypes.Global);
        var companies = snapshot.Scopes.Where(x => x.ScopeType == ScopeTypes.Company && x.ScopeId is not null).Select(x => x.ScopeId!.Value).Distinct().ToArray();
        if (companyId is not null && !global && !companies.Contains(companyId.Value)) return MigrationResult<IReadOnlyList<MigrationRunSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var rows = await repository.ListRunsAsync(companyId, companies, global, Math.Clamp(take, 1, 200), ct);
        return MigrationResult<IReadOnlyList<MigrationRunSummary>>.Success(rows.Select(Map).ToArray());
    }

    public async Task<MigrationResult<MigrationRunSummary>> GetRunAsync(Guid userId, Guid runId, CancellationToken ct)
    {
        var run = await repository.FindRunAsync(runId, ct);
        if (run is null) return MigrationResult<MigrationRunSummary>.Failure("MIGRATION_RUN_NOT_FOUND", "Migration run bulunamadı.");
        var denied = await AuthorizeAsync(userId, MigrationPermissions.View, run.CompanyId, ct);
        return denied is null ? MigrationResult<MigrationRunSummary>.Success(Map(run)) : MigrationResult<MigrationRunSummary>.Failure(denied.Value.Code, denied.Value.Message);
    }

    public async Task<MigrationResult<IReadOnlyList<MigrationStageRowSummary>>> ListRowsAsync(Guid userId, Guid runId, string? status, int take, CancellationToken ct)
    {
        var run = await repository.FindRunAsync(runId, ct);
        if (run is null) return MigrationResult<IReadOnlyList<MigrationStageRowSummary>>.Failure("MIGRATION_RUN_NOT_FOUND", "Migration run bulunamadı.");
        var denied = await AuthorizeAsync(userId, MigrationPermissions.View, run.CompanyId, ct);
        if (denied is not null) return MigrationResult<IReadOnlyList<MigrationStageRowSummary>>.Failure(denied.Value.Code, denied.Value.Message);
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();
        if (normalizedStatus is not null && !MigrationStageRowStatuses.IsKnown(normalizedStatus)) return MigrationResult<IReadOnlyList<MigrationStageRowSummary>>.Failure("MIGRATION_ROW_STATUS_INVALID", "Migration satır durumu geçersiz.");
        var rows = await repository.ListRowsAsync(runId, normalizedStatus, Math.Clamp(take, 1, 2000), ct);
        return MigrationResult<IReadOnlyList<MigrationStageRowSummary>>.Success(rows.Select(Map).ToArray());
    }

    public async Task<MigrationResult<MigrationStageSummary>> StageRowsAsync(Guid userId, Guid runId, StageMigrationRowsRequest request, CancellationToken ct)
    {
        var run = await repository.FindRunAsync(runId, ct);
        if (run is null) return MigrationResult<MigrationStageSummary>.Failure("MIGRATION_RUN_NOT_FOUND", "Migration run bulunamadı.");
        var denied = await AuthorizeAsync(userId, MigrationPermissions.Manage, run.CompanyId, ct);
        if (denied is not null) return MigrationResult<MigrationStageSummary>.Failure(denied.Value.Code, denied.Value.Message);
        if (run.Version != request.Version) return MigrationResult<MigrationStageSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Migration run başka bir işlem tarafından değiştirilmiş.");
        if (run.Status != MigrationRunStatuses.Created) return MigrationResult<MigrationStageSummary>.Failure("MIGRATION_RUN_NOT_STAGEABLE", "Yalnız CREATED durumundaki migration run stage edilebilir.");
        if (request.Rows.Count is < 1 or > 10000) return MigrationResult<MigrationStageSummary>.Failure("MIGRATION_ROW_COUNT_INVALID", "Bir staging çağrısında 1-10000 satır gönderilmelidir.");
        if (request.Rows.GroupBy(x => x.RowNumber).Any(x => x.Count() > 1)) return MigrationResult<MigrationStageSummary>.Failure("MIGRATION_ROW_NUMBER_DUPLICATE", "Aynı rowNumber bir staging çağrısında birden fazla kez kullanılamaz.");

        var now = timeProvider.GetUtcNow();
        var existing = (await repository.ListLineagesAsync(run.CompanyId, run.SourceSystem, run.SourceObject, run.TargetEntity, ct)).ToDictionary(x => x.SourceKey, StringComparer.Ordinal);
        var staged = new List<MigrationStageRow>(request.Rows.Count);
        var newRows = 0; var changedRows = 0; var unchangedRows = 0;

        try
        {
            foreach (var input in request.Rows.OrderBy(x => x.RowNumber))
            {
                var sourceKey = MigrationRun.Required(input.SourceKey, 240);
                var sourceHash = MigrationRun.Hash(input.SourceRowHash);
                EnsureJsonObject(input.SourcePayloadJson, "sourcePayloadJson");
                EnsureJsonObject(input.TransformedPayloadJson, "transformedPayloadJson");
                var lineageKey = $"{run.SourceSystem}|{run.SourceObject}|{sourceKey}|{run.TargetEntity}";

                string idempotence;
                Guid? previousRunId = null;
                Guid? previousStageRowId = null;
                if (existing.TryGetValue(sourceKey, out var lineage))
                {
                    idempotence = lineage.Classify(sourceHash);
                    previousRunId = lineage.LastRunId;
                    previousStageRowId = lineage.LastStageRowId;
                    if (idempotence == MigrationIdempotenceStatuses.Unchanged) unchangedRows++; else changedRows++;
                }
                else { idempotence = MigrationIdempotenceStatuses.New; newRows++; }

                var row = MigrationStageRow.Create(
                    run.Id, input.RowNumber, sourceKey, lineageKey, sourceHash,
                    sensitiveDataProtector.Protect(input.SourcePayloadJson), sensitiveDataProtector.Protect(input.TransformedPayloadJson),
                    idempotence, previousRunId, previousStageRowId,
                    input.WarningCode, input.WarningMessage, input.ErrorCode, input.ErrorMessage, now);
                staged.Add(row);

                if (lineage is null)
                {
                    lineage = MigrationLineageRecord.Create(run.CompanyId, run.SourceSystem, run.SourceObject, sourceKey, run.TargetEntity, sourceHash, run.Id, row.Id, now, userId);
                    repository.AddLineage(lineage);
                    existing[sourceKey] = lineage;
                }
                else lineage.Observe(sourceHash, run.Id, row.Id, now, userId);
            }

            repository.AddRows(staged);
            run.CompleteStaging(staged.Count,
                staged.Count(x => x.Status == MigrationStageRowStatuses.Valid),
                staged.Count(x => x.Status == MigrationStageRowStatuses.Warning),
                staged.Count(x => x.Status == MigrationStageRowStatuses.Error),
                staged.Count(x => x.Status == MigrationStageRowStatuses.Duplicate), now, userId);
            await repository.SaveChangesAsync(ct);
            return MigrationResult<MigrationStageSummary>.Success(new MigrationStageSummary(Map(run), newRows, changedRows, unchangedRows));
        }
        catch (ArgumentException ex) { return MigrationResult<MigrationStageSummary>.Failure("MIGRATION_ROW_INVALID", ex.Message); }
        catch (JsonException) { return MigrationResult<MigrationStageSummary>.Failure("MIGRATION_PAYLOAD_INVALID", "Migration payload geçerli bir JSON object olmalıdır."); }
    }

    public async Task<MigrationResult<MigrationValidationSummary>> ValidateRunAsync(Guid userId, Guid runId, ValidateMigrationRunRequest request, CancellationToken ct)
    {
        var run = await repository.FindRunAsync(runId, ct);
        if (run is null) return MigrationResult<MigrationValidationSummary>.Failure("MIGRATION_RUN_NOT_FOUND", "Migration run bulunamadı.");
        var denied = await AuthorizeAsync(userId, MigrationPermissions.Manage, run.CompanyId, ct);
        if (denied is not null) return MigrationResult<MigrationValidationSummary>.Failure(denied.Value.Code, denied.Value.Message);
        if (run.Version != request.Version) return MigrationResult<MigrationValidationSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Migration run başka bir işlem tarafından değiştirilmiş.");
        try
        {
            run.CompleteValidation(timeProvider.GetUtcNow(), userId);
            await repository.SaveChangesAsync(ct);
            return MigrationResult<MigrationValidationSummary>.Success(new MigrationValidationSummary(Map(run), run.ErrorRows == 0, run.ErrorRows, run.WarningRows, run.DuplicateRows));
        }
        catch (InvalidOperationException ex) { return MigrationResult<MigrationValidationSummary>.Failure("MIGRATION_RUN_NOT_VALIDATABLE", ex.Message); }
    }

    public async Task<MigrationResult<MigrationReconcileSummary>> ReconcileRunAsync(Guid userId, Guid runId, ReconcileMigrationRunRequest request, CancellationToken ct)
    {
        var run = await repository.FindRunAsync(runId, ct);
        if (run is null) return MigrationResult<MigrationReconcileSummary>.Failure("MIGRATION_RUN_NOT_FOUND", "Migration run bulunamadı.");
        var denied = await AuthorizeAsync(userId, MigrationPermissions.Reconcile, run.CompanyId, ct);
        if (denied is not null) return MigrationResult<MigrationReconcileSummary>.Failure(denied.Value.Code, denied.Value.Message);
        if (run.Version != request.Version) return MigrationResult<MigrationReconcileSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Migration run başka bir işlem tarafından değiştirilmiş.");
        if (request.Metrics.Count is < 1 or > 100) return MigrationResult<MigrationReconcileSummary>.Failure("MIGRATION_RECONCILIATION_REQUIRED", "1-100 reconciliation metriği gereklidir.");
        if (request.Metrics.GroupBy(x => x.MetricCode.Trim(), StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1)) return MigrationResult<MigrationReconcileSummary>.Failure("MIGRATION_RECONCILIATION_DUPLICATE", "Aynı metricCode birden fazla kez kullanılamaz.");
        if ((await repository.ListReconciliationsAsync(runId, ct)).Count > 0) return MigrationResult<MigrationReconcileSummary>.Failure("MIGRATION_RECONCILIATION_EXISTS", "Bu run için reconciliation zaten kaydedilmiş.");
        try
        {
            var now = timeProvider.GetUtcNow();
            var metrics = request.Metrics.Select(x => MigrationReconciliation.Create(runId, x.MetricCode, x.MetricName, x.SourceValue, x.TargetValue, x.Tolerance, x.Notes, now, userId)).ToArray();
            repository.AddReconciliations(metrics);
            var mismatches = metrics.Count(x => x.Status == MigrationReconciliationStatuses.Mismatch);
            run.CompleteReconciliation(mismatches, now, userId);
            await repository.SaveChangesAsync(ct);
            return MigrationResult<MigrationReconcileSummary>.Success(new MigrationReconcileSummary(Map(run), metrics.Select(Map).ToArray(), mismatches));
        }
        catch (ArgumentException ex) { return MigrationResult<MigrationReconcileSummary>.Failure("MIGRATION_RECONCILIATION_INVALID", ex.Message); }
        catch (InvalidOperationException ex) { return MigrationResult<MigrationReconcileSummary>.Failure("MIGRATION_RUN_NOT_RECONCILABLE", ex.Message); }
    }

    private async Task<(string Code, string Message)?> AuthorizeAsync(Guid userId, string permission, Guid companyId, CancellationToken ct)
    {
        if (!await accessControl.HasPermissionAsync(userId, permission, ct)) return ("PERMISSION_DENIED", "Migration işlemi için yetkiniz yok.");
        if (await accessControl.HasScopeAsync(userId, ScopeTypes.Global, null, ct) || await accessControl.HasScopeAsync(userId, ScopeTypes.Company, companyId, ct)) return null;
        return ("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
    }

    private static void EnsureJsonObject(string value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        using var document = JsonDocument.Parse(value);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException($"{name} must be a JSON object.");
    }

    private static MigrationRunSummary Map(MigrationRun x) => new(x.Id, x.CompanyId, x.SourceSystem, x.SourceObject, x.TargetEntity, x.SourceFileName, x.SourceContentHash, x.MappingHash, x.Status, x.TotalRows, x.ValidRows, x.WarningRows, x.ErrorRows, x.DuplicateRows, x.ReconciliationMismatchCount, x.CreatedAt, x.UpdatedAt, x.Version);
    private static MigrationStageRowSummary Map(MigrationStageRow x) => new(x.Id, x.RowNumber, x.SourceKey, x.LineageKey, x.SourceRowHash, x.Status, x.IdempotenceStatus, x.WarningCode, x.WarningMessage, x.ErrorCode, x.ErrorMessage, x.PreviousRunId, x.PreviousStageRowId, x.StagedAt);
    private static MigrationReconciliationSummary Map(MigrationReconciliation x) => new(x.Id, x.MetricCode, x.MetricName, x.SourceValue, x.TargetValue, x.Difference, x.Tolerance, x.Status, x.Notes, x.RecordedAt);
}