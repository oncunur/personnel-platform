using PersonnelPlatform.Domain.Camp;
using PersonnelPlatform.Domain.Meal;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Application.Meal;

public interface IMealRepository
{
    Task<IReadOnlyList<MealTypeSummary>> ListMealTypesAsync(CancellationToken cancellationToken);
    Task<MealType?> FindMealTypeAsync(Guid mealTypeId, CancellationToken cancellationToken);
    Task<CampSite?> FindCampAsync(Guid campId, CancellationToken cancellationToken);
    Task<Employee?> FindEmployeeAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<MealProjectSnapshot?> FindProjectSnapshotAsync(Guid employeeId, DateOnly date, CancellationToken cancellationToken);

    Task<MealRate?> FindApplicableRateAsync(Guid campId, Guid mealTypeId, DateOnly date, CancellationToken cancellationToken);
    Task<IReadOnlyList<MealRateSummary>> ListRatesAsync(Guid campId, Guid? mealTypeId, CancellationToken cancellationToken);
    void AddRate(MealRate rate);

    Task<MealConsumption?> FindDuplicateConsumptionAsync(Guid employeeId, DateOnly date, Guid mealTypeId, CancellationToken cancellationToken);
    Task<MealConsumption?> FindByExternalEventAsync(Guid companyId, string source, string externalEventId, CancellationToken cancellationToken);
    Task<MealConsumptionSummary?> GetConsumptionSummaryAsync(Guid consumptionId, CancellationToken cancellationToken);
    Task<MealPagedResult<MealConsumptionSummary>> SearchConsumptionsAsync(MealConsumptionQuery query, bool globalAccess, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken);
    void AddConsumption(MealConsumption consumption);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
