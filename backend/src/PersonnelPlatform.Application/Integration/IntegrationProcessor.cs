using System.Text.Json;
using PersonnelPlatform.Domain.Attendance;
using PersonnelPlatform.Domain.Integration;
using PersonnelPlatform.Domain.Meal;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Application.Integration;

public sealed class IntegrationProcessor(IIntegrationRepository repository, TimeProvider timeProvider)
{
    private const int MaxTechnicalAttempts = 5;

    public async Task<IntegrationProcessResult> RunAsync(CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var rows = await repository.ListDueStagingAsync(now, 100, ct);
        var processed = 0;
        var businessErrors = 0;
        var technicalErrors = 0;
        var deadLetters = 0;

        foreach (var row in rows)
        {
            try
            {
                var from = row.BeginProcessing(timeProvider.GetUtcNow());
                repository.AddStagingHistory(IntegrationStagingHistory.Create(row.Id, row.EventType, from, row.Status, null, null, null, timeProvider.GetUtcNow()));
                await repository.SaveChangesAsync(ct);

                var entity = row.EventType switch
                {
                    IntegrationEventTypes.AttendanceEvent => await ProcessAttendanceAsync(row, ct),
                    IntegrationEventTypes.MealConsumption => await ProcessMealAsync(row, ct),
                    _ => throw new IntegrationBusinessException("INTEGRATION_EVENT_TYPE_UNSUPPORTED", "Bu staging event türü henüz desteklenmiyor.")
                };

                from = row.Complete(entity.EntityType, entity.EntityId, timeProvider.GetUtcNow());
                repository.AddStagingHistory(IntegrationStagingHistory.Create(row.Id, row.EventType, from, row.Status, null, null, null, timeProvider.GetUtcNow()));
                await repository.SaveChangesAsync(ct);
                processed++;
            }
            catch (IntegrationBusinessException ex)
            {
                try
                {
                    var from = row.BusinessError(ex.Code, ex.Message, timeProvider.GetUtcNow());
                    repository.AddStagingHistory(IntegrationStagingHistory.Create(row.Id, row.EventType, from, row.Status, row.ErrorCode, row.ErrorMessage, null, timeProvider.GetUtcNow()));
                    await repository.SaveChangesAsync(ct);
                    businessErrors++;
                }
                catch (InvalidOperationException) { }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                try
                {
                    var message = Sanitize(ex.Message);
                    var from = row.TechnicalError("INTEGRATION_TECHNICAL_ERROR", message, MaxTechnicalAttempts, timeProvider.GetUtcNow());
                    repository.AddStagingHistory(IntegrationStagingHistory.Create(row.Id, row.EventType, from, row.Status, row.ErrorCode, row.ErrorMessage, null, timeProvider.GetUtcNow()));
                    await repository.SaveChangesAsync(ct);
                    if (row.Status == IntegrationStagingStatuses.DeadLetter) deadLetters++; else technicalErrors++;
                }
                catch (InvalidOperationException) { }
            }
        }

        return new IntegrationProcessResult(rows.Count, processed, businessErrors, technicalErrors, deadLetters);
    }

    private async Task<ProcessedEntity> ProcessAttendanceAsync(IntegrationStagingRecord staging, CancellationToken ct)
    {
        var payload = Deserialize<AttendanceIntegrationEventRequest>(staging.PayloadJson);
        var employeeMapping = await RequireMappingAsync(staging.IntegrationSystemId, IntegrationEntityTypes.Employee, payload.ExternalEmployeeCode, "INTEGRATION_EMPLOYEE_MAPPING_NOT_FOUND", "External personel kodu için mapping bulunamadı.", ct);
        var employee = await repository.FindEmployeeAsync(employeeMapping.InternalEntityId, ct);
        if (employee is null || employee.CompanyId != staging.CompanyId)
            throw new IntegrationBusinessException("EMPLOYEE_NOT_FOUND", "Mapping hedefindeki personel şirket kapsamında bulunamadı.");

        var direction = Normalize(payload.Direction);
        if (!RawAttendanceDirections.IsKnown(direction))
            throw new IntegrationBusinessException("ATTENDANCE_DIRECTION_INVALID", "PDKS yön bilgisi IN, OUT veya UNKNOWN olmalıdır.");

        var existing = await repository.FindRawAttendanceByExternalIdAsync(staging.CompanyId, staging.ExternalEventId, ct);
        if (existing is not null) return new("RAW_ATTENDANCE_EVENT", existing.Id);

        string? deviceCode = null;
        if (staging.DeviceId is { } deviceId)
            deviceCode = (await repository.FindDeviceAsync(deviceId, ct))?.Code;

        var receiver = staging.DeviceId ?? staging.IntegrationSystemId;
        RawAttendanceEvent row;
        try
        {
            row = RawAttendanceEvent.Create(
                staging.CompanyId,
                employee.Id,
                RawAttendanceSources.Integration,
                direction,
                payload.EventAt,
                deviceCode,
                staging.ExternalEventId,
                staging.PayloadJson,
                staging.ReceivedAt,
                receiver);
        }
        catch (ArgumentException ex) { throw new IntegrationBusinessException("RAW_ATTENDANCE_EVENT_INVALID", Sanitize(ex.Message)); }

        repository.AddRawAttendance(row);
        await repository.SaveChangesAsync(ct);
        return new("RAW_ATTENDANCE_EVENT", row.Id);
    }

    private async Task<ProcessedEntity> ProcessMealAsync(IntegrationStagingRecord staging, CancellationToken ct)
    {
        var payload = Deserialize<MealIntegrationEventRequest>(staging.PayloadJson);
        var employeeMapping = await RequireMappingAsync(staging.IntegrationSystemId, IntegrationEntityTypes.Employee, payload.ExternalEmployeeCode, "INTEGRATION_EMPLOYEE_MAPPING_NOT_FOUND", "External personel kodu için mapping bulunamadı.", ct);
        var mealTypeMapping = await RequireMappingAsync(staging.IntegrationSystemId, IntegrationEntityTypes.MealType, payload.ExternalMealTypeCode, "INTEGRATION_MEAL_TYPE_MAPPING_NOT_FOUND", "External öğün kodu için mapping bulunamadı.", ct);

        var employee = await repository.FindEmployeeAsync(employeeMapping.InternalEntityId, ct);
        if (employee is null || employee.CompanyId != staging.CompanyId)
            throw new IntegrationBusinessException("EMPLOYEE_NOT_FOUND", "Mapping hedefindeki personel şirket kapsamında bulunamadı.");
        if (employee.Status != EmployeeStatuses.Active)
            throw new IntegrationBusinessException("EMPLOYEE_INACTIVE", "Yalnız aktif personel için yemek tüketimi işlenebilir.");

        if (staging.DeviceId is null) throw new IntegrationBusinessException("INTEGRATION_DEVICE_REQUIRED", "Yemek olayı cihaz kimliği olmadan işlenemez.");
        var device = await repository.FindDeviceAsync(staging.DeviceId.Value, ct);
        if (device?.ScopedCampId is null) throw new IntegrationBusinessException("INTEGRATION_DEVICE_CAMP_REQUIRED", "Yemek terminali için kamp scope bulunamadı.");
        var camp = await repository.FindCampAsync(device.ScopedCampId.Value, ct);
        if (camp is null || !camp.IsActive || camp.CompanyId != staging.CompanyId)
            throw new IntegrationBusinessException("CAMP_NOT_FOUND", "Cihaz scope'undaki aktif kamp şirket kapsamında bulunamadı.");

        var mealType = await repository.FindMealTypeAsync(mealTypeMapping.InternalEntityId, ct);
        if (mealType is null || !mealType.IsActive)
            throw new IntegrationBusinessException("MEAL_TYPE_NOT_FOUND", "Mapping hedefindeki aktif öğün türü bulunamadı.");

        var existingExternal = await repository.FindMealByExternalIdAsync(staging.CompanyId, staging.ExternalEventId, ct);
        if (existingExternal is not null) return new("MEAL_CONSUMPTION", existingExternal.Id);

        var consumptionDate = DateOnly.FromDateTime(payload.ConsumedAt.DateTime);
        if (await repository.FindDuplicateMealAsync(employee.Id, consumptionDate, mealType.Id, ct) is not null)
            throw new IntegrationBusinessException("MEAL_ALREADY_CONSUMED", "Personel için bu gün ve öğün türünde tüketim kaydı zaten bulunuyor.");
        var rate = await repository.FindApplicableMealRateAsync(camp.Id, mealType.Id, consumptionDate, ct);
        if (rate is null) throw new IntegrationBusinessException("MEAL_RATE_NOT_FOUND", "Bu tarih, kamp ve öğün için geçerli fiyat bulunamadı.");
        if (payload.Quantity <= 0 || payload.Quantity > 10)
            throw new IntegrationBusinessException("MEAL_QUANTITY_INVALID", "Yemek miktarı 0'dan büyük ve 10'dan küçük/eşit olmalıdır.");

        var project = await repository.FindProjectAssignmentAsync(employee.Id, consumptionDate, ct);
        MealConsumption row;
        try
        {
            row = MealConsumption.Create(
                staging.CompanyId,
                employee.Id,
                camp.Id,
                mealType.Id,
                rate.Id,
                consumptionDate,
                payload.Quantity,
                rate.UnitPrice,
                rate.Currency,
                project?.ProjectId,
                project?.CostCenterId,
                MealConsumptionSources.Integration,
                staging.ExternalEventId,
                $"Integration device: {device.Code}",
                staging.ReceivedAt,
                staging.DeviceId.Value);
        }
        catch (ArgumentException ex) { throw new IntegrationBusinessException("MEAL_CONSUMPTION_INVALID", Sanitize(ex.Message)); }

        repository.AddMealConsumption(row);
        await repository.SaveChangesAsync(ct);
        return new("MEAL_CONSUMPTION", row.Id);
    }

    private async Task<ExternalEntityMapping> RequireMappingAsync(Guid systemId, string entityType, string externalCode, string errorCode, string errorMessage, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(externalCode)) throw new IntegrationBusinessException(errorCode, errorMessage);
        return await repository.FindActiveMappingAsync(systemId, entityType, Normalize(externalCode), ct)
            ?? throw new IntegrationBusinessException(errorCode, errorMessage);
    }

    private static T Deserialize<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new JsonException(); }
        catch (JsonException) { throw new IntegrationBusinessException("INTEGRATION_PAYLOAD_INVALID", "Staging payload JSON beklenen sözleşmeyle eşleşmiyor."); }
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
    private static string Sanitize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "Beklenmeyen entegrasyon hatası.";
        var value = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return value.Length <= 1800 ? value : value[..1800];
    }

    private sealed record ProcessedEntity(string EntityType, Guid EntityId);
}

public sealed class IntegrationBusinessException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
