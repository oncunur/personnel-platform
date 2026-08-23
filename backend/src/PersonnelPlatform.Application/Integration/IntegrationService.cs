using System.Security.Cryptography;
using System.Text.Json;
using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Application.Organization;
using PersonnelPlatform.Domain.Integration;

namespace PersonnelPlatform.Application.Integration;

public sealed class IntegrationService(
    IIntegrationRepository repository,
    IOrganizationRepository organizationRepository,
    AccessControlService accessControlService,
    TimeProvider timeProvider)
{
    public async Task<IntegrationResult<IReadOnlyList<IntegrationSystemSummary>>> ListSystemsAsync(Guid userId, Guid? companyId, CancellationToken ct)
    {
        var access = await ResolveAccessAsync(userId, ct);
        if (companyId is not null && !access.Global && !access.CompanyIds.Contains(companyId.Value))
            return IntegrationResult<IReadOnlyList<IntegrationSystemSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var rows = await repository.ListSystemsAsync(access.Global, access.CompanyIds, companyId, ct);
        return IntegrationResult<IReadOnlyList<IntegrationSystemSummary>>.Success(rows.Select(MapSystem).ToArray());
    }

    public async Task<IntegrationResult<IntegrationSystemSummary>> CreateSystemAsync(Guid userId, CreateIntegrationSystemRequest request, CancellationToken ct)
    {
        var company = await organizationRepository.FindCompanyAsync(request.CompanyId, ct);
        if (company is null) return IntegrationResult<IntegrationSystemSummary>.Failure("COMPANY_NOT_FOUND", "Şirket bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, company.Id, ct))
            return IntegrationResult<IntegrationSystemSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var code = Normalize(request.Code);
        if (await repository.SystemCodeExistsAsync(company.Id, code, ct))
            return IntegrationResult<IntegrationSystemSummary>.Failure("INTEGRATION_SYSTEM_CODE_EXISTS", "Bu entegrasyon sistem kodu şirkette zaten kullanılıyor.");
        try
        {
            var row = IntegrationSystem.Create(company.Id, code, request.Name, request.SystemType, timeProvider.GetUtcNow(), userId);
            repository.AddSystem(row); await repository.SaveChangesAsync(ct);
            return IntegrationResult<IntegrationSystemSummary>.Success(MapSystem(row));
        }
        catch (ArgumentException) { return IntegrationResult<IntegrationSystemSummary>.Failure("INTEGRATION_SYSTEM_INVALID", "Entegrasyon sistem bilgileri geçersiz."); }
    }

    public async Task<IntegrationResult<IntegrationSystemSummary>> UpdateSystemAsync(Guid userId, Guid systemId, UpdateIntegrationSystemRequest request, CancellationToken ct)
    {
        var row = await repository.FindSystemAsync(systemId, ct);
        if (row is null) return IntegrationResult<IntegrationSystemSummary>.Failure("INTEGRATION_SYSTEM_NOT_FOUND", "Entegrasyon sistemi bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, row.CompanyId, ct)) return IntegrationResult<IntegrationSystemSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (row.Version != request.Version) return IntegrationResult<IntegrationSystemSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Entegrasyon sistemi başka bir kullanıcı tarafından değiştirilmiş.");
        row.SetActive(request.IsActive, timeProvider.GetUtcNow(), userId); await repository.SaveChangesAsync(ct);
        return IntegrationResult<IntegrationSystemSummary>.Success(MapSystem(row));
    }

    public async Task<IntegrationResult<IReadOnlyList<IntegrationDeviceSummary>>> ListDevicesAsync(Guid userId, Guid systemId, CancellationToken ct)
    {
        var system = await repository.FindSystemAsync(systemId, ct);
        if (system is null) return IntegrationResult<IReadOnlyList<IntegrationDeviceSummary>>.Failure("INTEGRATION_SYSTEM_NOT_FOUND", "Entegrasyon sistemi bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, system.CompanyId, ct)) return IntegrationResult<IReadOnlyList<IntegrationDeviceSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return IntegrationResult<IReadOnlyList<IntegrationDeviceSummary>>.Success((await repository.ListDevicesAsync(systemId, ct)).Select(MapDevice).ToArray());
    }

    public async Task<IntegrationResult<IntegrationDeviceCredentialSummary>> CreateDeviceAsync(Guid userId, CreateIntegrationDeviceRequest request, CancellationToken ct)
    {
        var system = await repository.FindSystemAsync(request.IntegrationSystemId, ct);
        if (system is null) return IntegrationResult<IntegrationDeviceCredentialSummary>.Failure("INTEGRATION_SYSTEM_NOT_FOUND", "Entegrasyon sistemi bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, system.CompanyId, ct)) return IntegrationResult<IntegrationDeviceCredentialSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (await repository.DeviceCodeExistsAsync(system.Id, Normalize(request.Code), ct)) return IntegrationResult<IntegrationDeviceCredentialSummary>.Failure("INTEGRATION_DEVICE_CODE_EXISTS", "Bu cihaz kodu sistemde zaten kullanılıyor.");
        var validation = await ValidateDeviceAsync(system, request.DeviceType, request.ScopedCampId, ct);
        if (validation is not null) return IntegrationResult<IntegrationDeviceCredentialSummary>.Failure(validation.Value.Code, validation.Value.Message);
        try
        {
            var key = GenerateKey();
            var row = IntegrationDevice.Create(system.CompanyId, system.Id, request.Code, request.Name, request.DeviceType, request.ScopedCampId, HashKey(key), timeProvider.GetUtcNow(), userId);
            repository.AddDevice(row); await repository.SaveChangesAsync(ct);
            return IntegrationResult<IntegrationDeviceCredentialSummary>.Success(new(MapDevice(row), key));
        }
        catch (ArgumentException) { return IntegrationResult<IntegrationDeviceCredentialSummary>.Failure("INTEGRATION_DEVICE_INVALID", "Entegrasyon cihaz bilgileri geçersiz."); }
    }

    public async Task<IntegrationResult<IntegrationDeviceSummary>> UpdateDeviceAsync(Guid userId, Guid deviceId, UpdateIntegrationDeviceRequest request, CancellationToken ct)
    {
        var row = await repository.FindDeviceAsync(deviceId, ct);
        if (row is null) return IntegrationResult<IntegrationDeviceSummary>.Failure("INTEGRATION_DEVICE_NOT_FOUND", "Entegrasyon cihazı bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, row.CompanyId, ct)) return IntegrationResult<IntegrationDeviceSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (row.Version != request.Version) return IntegrationResult<IntegrationDeviceSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Cihaz başka bir kullanıcı tarafından değiştirilmiş.");
        row.SetActive(request.IsActive, timeProvider.GetUtcNow(), userId); await repository.SaveChangesAsync(ct);
        return IntegrationResult<IntegrationDeviceSummary>.Success(MapDevice(row));
    }

    public async Task<IntegrationResult<IntegrationDeviceCredentialSummary>> RotateDeviceCredentialAsync(Guid userId, Guid deviceId, RotateIntegrationDeviceCredentialRequest request, CancellationToken ct)
    {
        var row = await repository.FindDeviceAsync(deviceId, ct);
        if (row is null) return IntegrationResult<IntegrationDeviceCredentialSummary>.Failure("INTEGRATION_DEVICE_NOT_FOUND", "Entegrasyon cihazı bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, row.CompanyId, ct)) return IntegrationResult<IntegrationDeviceCredentialSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (row.Version != request.Version) return IntegrationResult<IntegrationDeviceCredentialSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Cihaz başka bir kullanıcı tarafından değiştirilmiş.");
        var key = GenerateKey(); row.RotateCredential(HashKey(key), timeProvider.GetUtcNow(), userId); await repository.SaveChangesAsync(ct);
        return IntegrationResult<IntegrationDeviceCredentialSummary>.Success(new(MapDevice(row), key));
    }

    public async Task<IntegrationResult<IReadOnlyList<IntegrationMappingSummary>>> ListMappingsAsync(Guid userId, Guid systemId, string? entityType, CancellationToken ct)
    {
        var system = await repository.FindSystemAsync(systemId, ct);
        if (system is null) return IntegrationResult<IReadOnlyList<IntegrationMappingSummary>>.Failure("INTEGRATION_SYSTEM_NOT_FOUND", "Entegrasyon sistemi bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, system.CompanyId, ct)) return IntegrationResult<IReadOnlyList<IntegrationMappingSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return IntegrationResult<IReadOnlyList<IntegrationMappingSummary>>.Success((await repository.ListMappingsAsync(system.Id, NormalizeNullable(entityType), ct)).Select(MapMapping).ToArray());
    }

    public async Task<IntegrationResult<IntegrationMappingSummary>> CreateMappingAsync(Guid userId, CreateIntegrationMappingRequest request, CancellationToken ct)
    {
        var system = await repository.FindSystemAsync(request.IntegrationSystemId, ct);
        if (system is null) return IntegrationResult<IntegrationMappingSummary>.Failure("INTEGRATION_SYSTEM_NOT_FOUND", "Entegrasyon sistemi bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, system.CompanyId, ct)) return IntegrationResult<IntegrationMappingSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var type = Normalize(request.EntityType); var externalCode = Normalize(request.ExternalCode);
        if (!IntegrationEntityTypes.IsKnown(type)) return IntegrationResult<IntegrationMappingSummary>.Failure("INTEGRATION_MAPPING_TYPE_INVALID", "Mapping entity türü geçersiz.");
        if (await repository.MappingExistsAsync(system.Id, type, externalCode, ct)) return IntegrationResult<IntegrationMappingSummary>.Failure("INTEGRATION_MAPPING_EXISTS", "Bu external mapping zaten tanımlı.");
        var targetError = await ValidateMappingTargetAsync(system.CompanyId, type, request.InternalEntityId, ct);
        if (targetError is not null) return IntegrationResult<IntegrationMappingSummary>.Failure(targetError.Value.Code, targetError.Value.Message);
        try
        {
            var row = ExternalEntityMapping.Create(system.CompanyId, system.Id, type, externalCode, request.InternalEntityId, timeProvider.GetUtcNow(), userId);
            repository.AddMapping(row); await repository.SaveChangesAsync(ct); return IntegrationResult<IntegrationMappingSummary>.Success(MapMapping(row));
        }
        catch (ArgumentException) { return IntegrationResult<IntegrationMappingSummary>.Failure("INTEGRATION_MAPPING_INVALID", "Mapping bilgileri geçersiz."); }
    }

    public async Task<IntegrationResult<IntegrationMappingSummary>> UpdateMappingAsync(Guid userId, Guid mappingId, UpdateIntegrationMappingRequest request, CancellationToken ct)
    {
        var row = await repository.FindMappingAsync(mappingId, ct);
        if (row is null) return IntegrationResult<IntegrationMappingSummary>.Failure("INTEGRATION_MAPPING_NOT_FOUND", "Mapping bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, row.CompanyId, ct)) return IntegrationResult<IntegrationMappingSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (row.Version != request.Version) return IntegrationResult<IntegrationMappingSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Mapping başka bir kullanıcı tarafından değiştirilmiş.");
        var targetError = await ValidateMappingTargetAsync(row.CompanyId, row.EntityType, request.InternalEntityId, ct);
        if (targetError is not null) return IntegrationResult<IntegrationMappingSummary>.Failure(targetError.Value.Code, targetError.Value.Message);
        row.ChangeTarget(request.InternalEntityId, request.IsActive, timeProvider.GetUtcNow(), userId); await repository.SaveChangesAsync(ct);
        return IntegrationResult<IntegrationMappingSummary>.Success(MapMapping(row));
    }

    public async Task<IntegrationResult<IReadOnlyList<StagingRecordSummary>>> ListQueueAsync(Guid userId, IntegrationQueueQuery query, CancellationToken ct)
    {
        var access = await ResolveAccessAsync(userId, ct);
        if (query.CompanyId is not null && !access.Global && !access.CompanyIds.Contains(query.CompanyId.Value)) return IntegrationResult<IReadOnlyList<StagingRecordSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var normalized = query with { EventType = NormalizeNullable(query.EventType), Status = NormalizeNullable(query.Status), Take = Math.Clamp(query.Take, 1, 1000) };
        return IntegrationResult<IReadOnlyList<StagingRecordSummary>>.Success((await repository.ListStagingAsync(access.Global, access.CompanyIds, normalized, ct)).Select(MapStaging).ToArray());
    }

    public async Task<IntegrationResult<IReadOnlyList<StagingHistorySummary>>> ListHistoryAsync(Guid userId, Guid stagingId, CancellationToken ct)
    {
        var row = await repository.FindStagingAsync(stagingId, ct);
        if (row is null) return IntegrationResult<IReadOnlyList<StagingHistorySummary>>.Failure("INTEGRATION_STAGING_NOT_FOUND", "Staging kaydı bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, row.CompanyId, ct)) return IntegrationResult<IReadOnlyList<StagingHistorySummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var rows = await repository.ListStagingHistoryAsync(stagingId, ct);
        return IntegrationResult<IReadOnlyList<StagingHistorySummary>>.Success(rows.Select(x => new StagingHistorySummary(x.Id, x.StagingRecordId, x.EventType, x.FromStatus, x.ToStatus, x.ErrorCode, x.ErrorMessage, x.ActorUserId, x.OccurredAt)).ToArray());
    }

    public async Task<IntegrationResult<StagingRecordSummary>> ReprocessAsync(Guid userId, Guid stagingId, ReprocessStagingRequest request, CancellationToken ct)
    {
        var row = await repository.FindStagingAsync(stagingId, ct);
        if (row is null) return IntegrationResult<StagingRecordSummary>.Failure("INTEGRATION_STAGING_NOT_FOUND", "Staging kaydı bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, row.CompanyId, ct)) return IntegrationResult<StagingRecordSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (row.Version != request.Version) return IntegrationResult<StagingRecordSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Staging kaydı başka bir işlem tarafından değiştirilmiş.");
        try
        {
            var from = row.Requeue(timeProvider.GetUtcNow(), userId); repository.AddStagingHistory(IntegrationStagingHistory.Create(row.Id, row.EventType, from, row.Status, null, null, userId, timeProvider.GetUtcNow()));
            await repository.SaveChangesAsync(ct); return IntegrationResult<StagingRecordSummary>.Success(MapStaging(row));
        }
        catch (InvalidOperationException) { return IntegrationResult<StagingRecordSummary>.Failure("INTEGRATION_REPROCESS_INVALID_STATE", "Yalnız hata veya dead-letter kayıtları tekrar kuyruğa alınabilir."); }
    }

    public async Task<IntegrationResult<IntegrationMonitoringSummary>> GetMonitoringAsync(Guid userId, Guid companyId, CancellationToken ct)
    {
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, companyId, ct)) return IntegrationResult<IntegrationMonitoringSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var systems = await repository.ListSystemsAsync(true, [], companyId, ct); var staging = await repository.ListCompanyStagingAsync(companyId, ct); var now = timeProvider.GetUtcNow();
        var health = new List<IntegrationSystemHealth>();
        foreach (var system in systems)
        {
            var s = staging.Where(x => x.IntegrationSystemId == system.Id).ToArray(); var devices = await repository.ListDevicesAsync(system.Id, ct);
            var queue = new IntegrationQueueMetrics(s.Count(x => x.Status == IntegrationStagingStatuses.Received), s.Count(x => x.Status == IntegrationStagingStatuses.Processing), s.Count(x => x.Status == IntegrationStagingStatuses.BusinessError), s.Count(x => x.Status == IntegrationStagingStatuses.TechnicalError), s.Count(x => x.Status == IntegrationStagingStatuses.DeadLetter), s.Count(x => x.Status == IntegrationStagingStatuses.Processed));
            health.Add(new IntegrationSystemHealth(system.Id, system.Code, system.Name, system.SystemType, s.OrderByDescending(x => x.ReceivedAt).Select(x => (DateTimeOffset?)x.ReceivedAt).FirstOrDefault(), s.Where(x => x.ProcessedAt != null).Max(x => x.ProcessedAt), s.Where(x => x.ErrorCode != null).OrderByDescending(x => x.UpdatedAt).Select(x => x.UpdatedAt).FirstOrDefault(), queue,
                devices.Select(d => new IntegrationDeviceHealth(d.Id, system.Code, d.Code, d.Name, d.DeviceType, d.ScopedCampId, DeviceHealth(d, now), d.LastSeenAt, d.LastErrorAt, d.LastErrorMessage)).ToArray()));
        }
        var backlog = staging.Count(x => x.Status is IntegrationStagingStatuses.Received or IntegrationStagingStatuses.Processing or IntegrationStagingStatuses.BusinessError or IntegrationStagingStatuses.TechnicalError);
        var errors = staging.Count(x => x.Status is IntegrationStagingStatuses.BusinessError or IntegrationStagingStatuses.TechnicalError);
        return IntegrationResult<IntegrationMonitoringSummary>.Success(new(companyId, health, backlog, errors, staging.Count(x => x.Status == IntegrationStagingStatuses.DeadLetter)));
    }

    public async Task<IntegrationResult<IntegrationDeviceContext>> AuthenticateDeviceAsync(ExternalDeviceHeaders headers, string expectedSystemType, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(headers.CompanyCode) || string.IsNullOrWhiteSpace(headers.SystemCode) || string.IsNullOrWhiteSpace(headers.DeviceCode) || string.IsNullOrWhiteSpace(headers.DeviceKey))
            return IntegrationResult<IntegrationDeviceContext>.Failure("INTEGRATION_DEVICE_AUTH_FAILED", "Entegrasyon cihaz kimliği doğrulanamadı.");
        var source = await repository.FindDeviceForAuthenticationAsync(Normalize(headers.CompanyCode), Normalize(headers.SystemCode), Normalize(headers.DeviceCode), ct);
        if (source is null || !source.Company.IsActive || !source.System.IsActive || !source.Device.IsActive || source.System.SystemType != Normalize(expectedSystemType))
            return IntegrationResult<IntegrationDeviceContext>.Failure("INTEGRATION_DEVICE_AUTH_FAILED", "Entegrasyon cihaz kimliği doğrulanamadı.");
        if (!KeyMatches(headers.DeviceKey, source.Device.CredentialHash))
        {
            source.Device.MarkError("DEVICE_AUTH_FAILED", timeProvider.GetUtcNow()); await repository.SaveChangesAsync(ct);
            return IntegrationResult<IntegrationDeviceContext>.Failure("INTEGRATION_DEVICE_AUTH_FAILED", "Entegrasyon cihaz kimliği doğrulanamadı.");
        }
        source.Device.MarkSeen(timeProvider.GetUtcNow()); await repository.SaveChangesAsync(ct);
        return IntegrationResult<IntegrationDeviceContext>.Success(new(source.Company.Id, source.System.Id, source.Device.Id, source.System.SystemType, source.System.Code, source.Device.Code, source.Device.DeviceType, source.Device.ScopedCampId));
    }

    public async Task<IntegrationResult<ExternalIngestResult>> StageAttendanceAsync(IntegrationDeviceContext device, AttendanceIntegrationEventRequest request, CancellationToken ct)
    {
        if (device.SystemType != IntegrationSystemTypes.Pdks) return IntegrationResult<ExternalIngestResult>.Failure("INTEGRATION_SYSTEM_TYPE_MISMATCH", "Bu cihaz PDKS olayı kabul etmiyor.");
        return await StageAsync(device, IntegrationEventTypes.AttendanceEvent, request.ExternalEventId, JsonSerializer.Serialize(request), ct);
    }

    public async Task<IntegrationResult<ExternalBatchIngestResult>> StageMealBatchAsync(IntegrationDeviceContext device, MealIntegrationBatchRequest request, CancellationToken ct)
    {
        if (device.SystemType != IntegrationSystemTypes.Meal) return IntegrationResult<ExternalBatchIngestResult>.Failure("INTEGRATION_SYSTEM_TYPE_MISMATCH", "Bu cihaz yemek olayı kabul etmiyor.");
        if (request.Events is null || request.Events.Count is < 1 or > 1000) return IntegrationResult<ExternalBatchIngestResult>.Failure("INTEGRATION_BATCH_SIZE_INVALID", "Batch 1 ile 1000 olay içermelidir.");
        var items = new List<ExternalIngestResult>(request.Events.Count); var duplicates = 0;
        foreach (var item in request.Events)
        {
            var result = await StageAsync(device, IntegrationEventTypes.MealConsumption, item.ExternalEventId, JsonSerializer.Serialize(item), ct);
            if (!result.Succeeded || result.Value is null) return IntegrationResult<ExternalBatchIngestResult>.Failure(result.ErrorCode ?? "INTEGRATION_EVENT_INVALID", result.ErrorMessage ?? "Yemek olayı staging'e alınamadı.");
            items.Add(result.Value); if (result.Value.Duplicate) duplicates++;
        }
        return IntegrationResult<ExternalBatchIngestResult>.Success(new(items.Count - duplicates, duplicates, items));
    }

    private async Task<IntegrationResult<ExternalIngestResult>> StageAsync(IntegrationDeviceContext device, string eventType, string externalEventId, string payloadJson, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(externalEventId) || externalEventId.Trim().Length > 240) return IntegrationResult<ExternalIngestResult>.Failure("INTEGRATION_EXTERNAL_EVENT_ID_INVALID", "External event id zorunludur ve 240 karakteri aşamaz.");
        var externalId = externalEventId.Trim(); var existing = await repository.FindStagingByExternalIdAsync(device.SystemId, eventType, externalId, ct);
        if (existing is not null) return IntegrationResult<ExternalIngestResult>.Success(new(externalId, existing.Id, existing.Status, true));
        try
        {
            var now = timeProvider.GetUtcNow(); var row = IntegrationStagingRecord.Create(device.CompanyId, device.SystemId, device.DeviceId, eventType, externalId, payloadJson, now);
            if (!await repository.TryInsertStagingAsync(row, ct))
            {
                existing = await repository.FindStagingByExternalIdAsync(device.SystemId, eventType, externalId, ct);
                return existing is null ? IntegrationResult<ExternalIngestResult>.Failure("INTEGRATION_STAGING_CONFLICT", "Olay staging'e alınırken eşzamanlı çakışma oluştu.") : IntegrationResult<ExternalIngestResult>.Success(new(externalId, existing.Id, existing.Status, true));
            }
            repository.AddStagingHistory(IntegrationStagingHistory.Create(row.Id, row.EventType, "NONE", row.Status, null, null, null, now)); await repository.SaveChangesAsync(ct);
            return IntegrationResult<ExternalIngestResult>.Success(new(externalId, row.Id, row.Status, false));
        }
        catch (ArgumentException) { return IntegrationResult<ExternalIngestResult>.Failure("INTEGRATION_EVENT_INVALID", "Entegrasyon olay bilgileri geçersiz."); }
    }

    private async Task<(string Code, string Message)?> ValidateDeviceAsync(IntegrationSystem system, string deviceType, Guid? campId, CancellationToken ct)
    {
        var type = Normalize(deviceType);
        if (system.SystemType == IntegrationSystemTypes.Pdks && type is not (IntegrationDeviceTypes.PdksTerminal or IntegrationDeviceTypes.Generic)) return ("INTEGRATION_DEVICE_TYPE_MISMATCH", "PDKS sistemi için cihaz türü uyumsuz.");
        if (system.SystemType == IntegrationSystemTypes.Meal && type is not (IntegrationDeviceTypes.MealTerminal or IntegrationDeviceTypes.Generic)) return ("INTEGRATION_DEVICE_TYPE_MISMATCH", "Yemek sistemi için cihaz türü uyumsuz.");
        if (type == IntegrationDeviceTypes.MealTerminal && campId is null) return ("INTEGRATION_DEVICE_CAMP_REQUIRED", "Yemek terminali için kamp scope zorunludur.");
        if (campId is not null)
        {
            var camp = await repository.FindCampAsync(campId.Value, ct); if (camp is null || camp.CompanyId != system.CompanyId) return ("CAMP_NOT_FOUND", "Şirket kapsamındaki kamp bulunamadı.");
        }
        return null;
    }

    private async Task<(string Code, string Message)?> ValidateMappingTargetAsync(Guid companyId, string entityType, Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty) return ("INTEGRATION_MAPPING_TARGET_INVALID", "Mapping hedefi zorunludur.");
        switch (entityType)
        {
            case IntegrationEntityTypes.Employee: { var x = await repository.FindEmployeeAsync(id, ct); return x is null || x.CompanyId != companyId ? ("EMPLOYEE_NOT_FOUND", "Şirket kapsamındaki personel bulunamadı.") : null; }
            case IntegrationEntityTypes.Camp: { var x = await repository.FindCampAsync(id, ct); return x is null || x.CompanyId != companyId ? ("CAMP_NOT_FOUND", "Şirket kapsamındaki kamp bulunamadı.") : null; }
            case IntegrationEntityTypes.MealType: return await repository.FindMealTypeAsync(id, ct) is null ? ("MEAL_TYPE_NOT_FOUND", "Öğün türü bulunamadı.") : null;
            case IntegrationEntityTypes.Project: { var x = await organizationRepository.FindProjectAsync(id, ct); return x is null || x.CompanyId != companyId ? ("PROJECT_NOT_FOUND", "Şirket kapsamındaki proje bulunamadı.") : null; }
            case IntegrationEntityTypes.CostCenter: { var x = await organizationRepository.FindCostCenterAsync(id, ct); return x is null || x.CompanyId != companyId ? ("COST_CENTER_NOT_FOUND", "Şirket kapsamındaki cost center bulunamadı.") : null; }
            default: return null;
        }
    }

    private async Task<(bool Global, HashSet<Guid> CompanyIds)> ResolveAccessAsync(Guid userId, CancellationToken ct)
    {
        var snapshot = await accessControlService.GetSnapshotAsync(userId, ct);
        return (snapshot.Scopes.Any(x => x.ScopeType == ScopeTypes.Global), snapshot.Scopes.Where(x => x.ScopeType == ScopeTypes.Company && x.ScopeId is not null).Select(x => x.ScopeId!.Value).ToHashSet());
    }

    private static IntegrationSystemSummary MapSystem(IntegrationSystem x) => new(x.Id, x.CompanyId, x.Code, x.Name, x.SystemType, x.IsActive, x.Version);
    private static IntegrationDeviceSummary MapDevice(IntegrationDevice x) => new(x.Id, x.CompanyId, x.IntegrationSystemId, x.Code, x.Name, x.DeviceType, x.ScopedCampId, x.IsActive, x.LastSeenAt, x.LastErrorAt, x.LastErrorMessage, x.Version);
    private static IntegrationMappingSummary MapMapping(ExternalEntityMapping x) => new(x.Id, x.CompanyId, x.IntegrationSystemId, x.EntityType, x.ExternalCode, x.InternalEntityId, x.IsActive, x.Version);
    private static StagingRecordSummary MapStaging(IntegrationStagingRecord x) => new(x.Id, x.CompanyId, x.IntegrationSystemId, x.DeviceId, x.EventType, x.ExternalEventId, x.Status, x.AttemptCount, x.NextRetryAt, x.ErrorCode, x.ErrorMessage, x.ProcessedEntityType, x.ProcessedEntityId, x.ReceivedAt, x.LastAttemptAt, x.ProcessedAt, x.Version);
    private static string Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
    private static string? NormalizeNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private static string GenerateKey() { var value = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)); return value.TrimEnd('=').Replace('+', '-').Replace('/', '_'); }
    private static string HashKey(string key) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key)));
    private static bool KeyMatches(string plaintext, string expectedHash) { try { var a = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(plaintext)); var b = Convert.FromHexString(expectedHash); return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b); } catch { return false; } }
    private static string DeviceHealth(IntegrationDevice device, DateTimeOffset now)
    {
        if (!device.IsActive) return "DISABLED";
        if (device.LastErrorAt is not null && (device.LastSeenAt is null || device.LastErrorAt > device.LastSeenAt)) return "ERROR";
        if (device.LastSeenAt is null) return "NEVER_SEEN";
        var age = now - device.LastSeenAt.Value; return age <= TimeSpan.FromMinutes(5) ? "HEALTHY" : age <= TimeSpan.FromHours(1) ? "STALE" : "OFFLINE";
    }
}
