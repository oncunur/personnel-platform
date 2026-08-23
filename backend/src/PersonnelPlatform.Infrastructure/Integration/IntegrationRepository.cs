using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Integration;
using PersonnelPlatform.Domain.Attendance;
using PersonnelPlatform.Domain.Camp;
using PersonnelPlatform.Domain.Integration;
using PersonnelPlatform.Domain.Meal;
using PersonnelPlatform.Domain.Personnel;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Integration;

public sealed class IntegrationRepository(ApplicationDbContext db) : IIntegrationRepository
{
    public async Task<IReadOnlyList<IntegrationSystem>> ListSystemsAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, CancellationToken ct)
    {
        var q = db.IntegrationSystems.AsNoTracking().Where(x => x.DeletedAt == null);
        if (!globalAccess) q = q.Where(x => companyIds.Contains(x.CompanyId));
        if (companyId is not null) q = q.Where(x => x.CompanyId == companyId.Value);
        return await q.OrderBy(x => x.Code).ToListAsync(ct);
    }

    public Task<IntegrationSystem?> FindSystemAsync(Guid systemId, CancellationToken ct) =>
        db.IntegrationSystems.FirstOrDefaultAsync(x => x.Id == systemId && x.DeletedAt == null, ct);

    public Task<bool> SystemCodeExistsAsync(Guid companyId, string code, CancellationToken ct) =>
        db.IntegrationSystems.AsNoTracking().AnyAsync(x => x.CompanyId == companyId && x.Code == code && x.DeletedAt == null, ct);

    public void AddSystem(IntegrationSystem system) => db.IntegrationSystems.Add(system);

    public async Task<IReadOnlyList<IntegrationDevice>> ListDevicesAsync(Guid systemId, CancellationToken ct) =>
        await db.IntegrationDevices.AsNoTracking().Where(x => x.IntegrationSystemId == systemId && x.DeletedAt == null).OrderBy(x => x.Code).ToListAsync(ct);

    public Task<IntegrationDevice?> FindDeviceAsync(Guid deviceId, CancellationToken ct) =>
        db.IntegrationDevices.FirstOrDefaultAsync(x => x.Id == deviceId && x.DeletedAt == null, ct);

    public Task<bool> DeviceCodeExistsAsync(Guid systemId, string code, CancellationToken ct) =>
        db.IntegrationDevices.AsNoTracking().AnyAsync(x => x.IntegrationSystemId == systemId && x.Code == code && x.DeletedAt == null, ct);

    public async Task<IntegrationDeviceAuthSource?> FindDeviceForAuthenticationAsync(string companyCode, string systemCode, string deviceCode, CancellationToken ct)
    {
        var company = await db.Companies.FirstOrDefaultAsync(x => x.Code == companyCode && x.DeletedAt == null, ct);
        if (company is null) return null;
        var system = await db.IntegrationSystems.FirstOrDefaultAsync(x => x.CompanyId == company.Id && x.Code == systemCode && x.DeletedAt == null, ct);
        if (system is null) return null;
        var device = await db.IntegrationDevices.FirstOrDefaultAsync(x => x.IntegrationSystemId == system.Id && x.Code == deviceCode && x.DeletedAt == null, ct);
        return device is null ? null : new IntegrationDeviceAuthSource(company, system, device);
    }

    public void AddDevice(IntegrationDevice device) => db.IntegrationDevices.Add(device);

    public async Task<IReadOnlyList<ExternalEntityMapping>> ListMappingsAsync(Guid systemId, string? entityType, CancellationToken ct)
    {
        var q = db.ExternalEntityMappings.AsNoTracking().Where(x => x.IntegrationSystemId == systemId && x.DeletedAt == null);
        if (entityType is not null) q = q.Where(x => x.EntityType == entityType);
        return await q.OrderBy(x => x.EntityType).ThenBy(x => x.ExternalCode).ToListAsync(ct);
    }

    public Task<ExternalEntityMapping?> FindMappingAsync(Guid mappingId, CancellationToken ct) =>
        db.ExternalEntityMappings.FirstOrDefaultAsync(x => x.Id == mappingId && x.DeletedAt == null, ct);

    public Task<ExternalEntityMapping?> FindActiveMappingAsync(Guid systemId, string entityType, string externalCode, CancellationToken ct) =>
        db.ExternalEntityMappings.AsNoTracking().FirstOrDefaultAsync(x => x.IntegrationSystemId == systemId && x.EntityType == entityType && x.ExternalCode == externalCode && x.IsActive && x.DeletedAt == null, ct);

    public Task<bool> MappingExistsAsync(Guid systemId, string entityType, string externalCode, CancellationToken ct) =>
        db.ExternalEntityMappings.AsNoTracking().AnyAsync(x => x.IntegrationSystemId == systemId && x.EntityType == entityType && x.ExternalCode == externalCode && x.DeletedAt == null, ct);

    public void AddMapping(ExternalEntityMapping mapping) => db.ExternalEntityMappings.Add(mapping);

    public Task<IntegrationStagingRecord?> FindStagingByExternalIdAsync(Guid systemId, string eventType, string externalEventId, CancellationToken ct) =>
        db.IntegrationStagingRecords.FirstOrDefaultAsync(x => x.IntegrationSystemId == systemId && x.EventType == eventType && x.ExternalEventId == externalEventId && x.DeletedAt == null, ct);

    public Task<IntegrationStagingRecord?> FindStagingAsync(Guid stagingId, CancellationToken ct) =>
        db.IntegrationStagingRecords.FirstOrDefaultAsync(x => x.Id == stagingId && x.DeletedAt == null, ct);

    public async Task<IReadOnlyList<IntegrationStagingRecord>> ListStagingAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, IntegrationQueueQuery query, CancellationToken ct)
    {
        var q = db.IntegrationStagingRecords.AsNoTracking().Where(x => x.DeletedAt == null);
        if (!globalAccess) q = q.Where(x => companyIds.Contains(x.CompanyId));
        if (query.CompanyId is not null) q = q.Where(x => x.CompanyId == query.CompanyId.Value);
        if (query.IntegrationSystemId is not null) q = q.Where(x => x.IntegrationSystemId == query.IntegrationSystemId.Value);
        if (query.EventType is not null) q = q.Where(x => x.EventType == query.EventType);
        if (query.Status is not null) q = q.Where(x => x.Status == query.Status);
        return await q.OrderByDescending(x => x.ReceivedAt).Take(query.Take).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<IntegrationStagingHistory>> ListStagingHistoryAsync(Guid stagingId, CancellationToken ct) =>
        await db.IntegrationStagingHistories.AsNoTracking().Where(x => x.StagingRecordId == stagingId).OrderByDescending(x => x.OccurredAt).ToListAsync(ct);

    public async Task<IReadOnlyList<IntegrationStagingRecord>> ListDueStagingAsync(DateTimeOffset now, int take, CancellationToken ct) =>
        await db.IntegrationStagingRecords
            .Where(x => x.DeletedAt == null && (x.Status == IntegrationStagingStatuses.Received || (x.Status == IntegrationStagingStatuses.TechnicalError && x.NextRetryAt <= now)))
            .OrderBy(x => x.ReceivedAt)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<IntegrationStagingRecord>> ListCompanyStagingAsync(Guid companyId, CancellationToken ct) =>
        await db.IntegrationStagingRecords.AsNoTracking().Where(x => x.CompanyId == companyId && x.DeletedAt == null).OrderByDescending(x => x.ReceivedAt).Take(5000).ToListAsync(ct);

    public async Task<bool> TryInsertStagingAsync(IntegrationStagingRecord x, CancellationToken ct)
    {
        var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO integration.staging_records
                (id, company_id, integration_system_id, device_id, event_type, external_event_id, payload_json, status, attempt_count, next_retry_at,
                 error_code, error_message, processed_entity_type, processed_entity_id, received_at, last_attempt_at, processed_at,
                 created_at, created_by, updated_at, updated_by, deleted_at, deleted_by, version)
            VALUES ({x.Id}, {x.CompanyId}, {x.IntegrationSystemId}, {x.DeviceId}, {x.EventType}, {x.ExternalEventId}, CAST({x.PayloadJson} AS jsonb), {x.Status}, {x.AttemptCount}, {x.NextRetryAt},
                    {x.ErrorCode}, {x.ErrorMessage}, {x.ProcessedEntityType}, {x.ProcessedEntityId}, {x.ReceivedAt}, {x.LastAttemptAt}, {x.ProcessedAt},
                    {x.CreatedAt}, NULL, NULL, NULL, NULL, NULL, {x.Version})
            ON CONFLICT (integration_system_id, event_type, external_event_id) DO NOTHING
            """, ct);
        return affected > 0;
    }

    public void AddStagingHistory(IntegrationStagingHistory history) => db.IntegrationStagingHistories.Add(history);

    public Task<Employee?> FindEmployeeAsync(Guid employeeId, CancellationToken ct) =>
        db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == employeeId && x.DeletedAt == null, ct);

    public Task<RawAttendanceEvent?> FindRawAttendanceByExternalIdAsync(Guid companyId, string externalEventId, CancellationToken ct) =>
        db.RawAttendanceEvents.AsNoTracking().FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Source == RawAttendanceSources.Integration && x.ExternalEventId == externalEventId, ct);

    public void AddRawAttendance(RawAttendanceEvent row) => db.RawAttendanceEvents.Add(row);

    public Task<CampSite?> FindCampAsync(Guid campId, CancellationToken ct) =>
        db.Camps.AsNoTracking().FirstOrDefaultAsync(x => x.Id == campId && x.DeletedAt == null, ct);

    public Task<MealType?> FindMealTypeAsync(Guid mealTypeId, CancellationToken ct) =>
        db.MealTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == mealTypeId, ct);

    public Task<MealRate?> FindApplicableMealRateAsync(Guid campId, Guid mealTypeId, DateOnly date, CancellationToken ct) =>
        db.MealRates.AsNoTracking().Where(x => x.CampId == campId && x.MealTypeId == mealTypeId && x.DeletedAt == null && x.ValidFrom <= date && (x.ValidUntilExclusive == null || x.ValidUntilExclusive > date)).OrderByDescending(x => x.ValidFrom).FirstOrDefaultAsync(ct);

    public Task<MealConsumption?> FindDuplicateMealAsync(Guid employeeId, DateOnly date, Guid mealTypeId, CancellationToken ct) =>
        db.MealConsumptions.AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.ConsumptionDate == date && x.MealTypeId == mealTypeId && x.DeletedAt == null, ct);

    public Task<MealConsumption?> FindMealByExternalIdAsync(Guid companyId, string externalEventId, CancellationToken ct) =>
        db.MealConsumptions.AsNoTracking().FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Source == MealConsumptionSources.Integration && x.ExternalEventId == externalEventId && x.DeletedAt == null, ct);

    public async Task<EmployeeProjectAssignment?> FindProjectAssignmentAsync(Guid employeeId, DateOnly date, CancellationToken ct) =>
        await db.EmployeeProjectAssignments.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.DeletedAt == null && x.Status == ProjectAssignmentStatuses.Active && x.ValidFrom <= date && (x.ValidUntil == null || x.ValidUntil >= date))
            .OrderByDescending(x => x.AllocationPercent).ThenByDescending(x => x.ValidFrom)
            .FirstOrDefaultAsync(ct);

    public void AddMealConsumption(MealConsumption row) => db.MealConsumptions.Add(row);

    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
