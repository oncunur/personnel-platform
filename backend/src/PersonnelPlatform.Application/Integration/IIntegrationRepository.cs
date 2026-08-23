using PersonnelPlatform.Domain.Attendance;
using PersonnelPlatform.Domain.Camp;
using PersonnelPlatform.Domain.Integration;
using PersonnelPlatform.Domain.Meal;
using PersonnelPlatform.Domain.Organization;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Application.Integration;

public sealed record IntegrationDeviceAuthSource(Company Company, IntegrationSystem System, IntegrationDevice Device);

public interface IIntegrationRepository
{
    Task<IReadOnlyList<IntegrationSystem>> ListSystemsAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, CancellationToken ct);
    Task<IntegrationSystem?> FindSystemAsync(Guid systemId, CancellationToken ct);
    Task<bool> SystemCodeExistsAsync(Guid companyId, string code, CancellationToken ct);
    void AddSystem(IntegrationSystem system);

    Task<IReadOnlyList<IntegrationDevice>> ListDevicesAsync(Guid systemId, CancellationToken ct);
    Task<IntegrationDevice?> FindDeviceAsync(Guid deviceId, CancellationToken ct);
    Task<bool> DeviceCodeExistsAsync(Guid systemId, string code, CancellationToken ct);
    Task<IntegrationDeviceAuthSource?> FindDeviceForAuthenticationAsync(string companyCode, string systemCode, string deviceCode, CancellationToken ct);
    void AddDevice(IntegrationDevice device);

    Task<IReadOnlyList<ExternalEntityMapping>> ListMappingsAsync(Guid systemId, string? entityType, CancellationToken ct);
    Task<ExternalEntityMapping?> FindMappingAsync(Guid mappingId, CancellationToken ct);
    Task<ExternalEntityMapping?> FindActiveMappingAsync(Guid systemId, string entityType, string externalCode, CancellationToken ct);
    Task<bool> MappingExistsAsync(Guid systemId, string entityType, string externalCode, CancellationToken ct);
    void AddMapping(ExternalEntityMapping mapping);

    Task<IntegrationStagingRecord?> FindStagingByExternalIdAsync(Guid systemId, string eventType, string externalEventId, CancellationToken ct);
    Task<IntegrationStagingRecord?> FindStagingAsync(Guid stagingId, CancellationToken ct);
    Task<IReadOnlyList<IntegrationStagingRecord>> ListStagingAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, IntegrationQueueQuery query, CancellationToken ct);
    Task<IReadOnlyList<IntegrationStagingHistory>> ListStagingHistoryAsync(Guid stagingId, CancellationToken ct);
    Task<IReadOnlyList<IntegrationStagingRecord>> ListDueStagingAsync(DateTimeOffset now, int take, CancellationToken ct);
    Task<IReadOnlyList<IntegrationStagingRecord>> ListCompanyStagingAsync(Guid companyId, CancellationToken ct);
    void AddStaging(IntegrationStagingRecord record);
    void AddStagingHistory(IntegrationStagingHistory history);

    Task<Employee?> FindEmployeeAsync(Guid employeeId, CancellationToken ct);
    Task<RawAttendanceEvent?> FindRawAttendanceByExternalIdAsync(Guid companyId, string externalEventId, CancellationToken ct);
    void AddRawAttendance(RawAttendanceEvent row);

    Task<CampSite?> FindCampAsync(Guid campId, CancellationToken ct);
    Task<MealType?> FindMealTypeAsync(Guid mealTypeId, CancellationToken ct);
    Task<MealRate?> FindApplicableMealRateAsync(Guid campId, Guid mealTypeId, DateOnly date, CancellationToken ct);
    Task<MealConsumption?> FindDuplicateMealAsync(Guid employeeId, DateOnly date, Guid mealTypeId, CancellationToken ct);
    Task<MealConsumption?> FindMealByExternalIdAsync(Guid companyId, string externalEventId, CancellationToken ct);
    Task<EmployeeProjectAssignment?> FindProjectAssignmentAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    void AddMealConsumption(MealConsumption row);

    Task<int> SaveChangesAsync(CancellationToken ct);
}
