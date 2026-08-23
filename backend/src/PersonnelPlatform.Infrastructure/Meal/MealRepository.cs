using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Meal;
using PersonnelPlatform.Domain.Camp;
using PersonnelPlatform.Domain.Meal;
using PersonnelPlatform.Domain.Personnel;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Meal;

public sealed class MealRepository(ApplicationDbContext dbContext) : IMealRepository
{
    public async Task<IReadOnlyList<MealTypeSummary>> ListMealTypesAsync(CancellationToken cancellationToken) =>
        await dbContext.MealTypes.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder)
            .Select(x => new MealTypeSummary(x.Id, x.Code, x.Name, x.DisplayOrder)).ToListAsync(cancellationToken);

    public Task<MealType?> FindMealTypeAsync(Guid mealTypeId, CancellationToken cancellationToken) =>
        dbContext.MealTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == mealTypeId && x.IsActive, cancellationToken);

    public Task<CampSite?> FindCampAsync(Guid campId, CancellationToken cancellationToken) =>
        dbContext.Camps.AsNoTracking().FirstOrDefaultAsync(x => x.Id == campId && x.DeletedAt == null, cancellationToken);

    public Task<Employee?> FindEmployeeAsync(Guid employeeId, CancellationToken cancellationToken) =>
        dbContext.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == employeeId && x.DeletedAt == null, cancellationToken);

    public Task<MealProjectSnapshot?> FindProjectSnapshotAsync(Guid employeeId, DateOnly date, CancellationToken cancellationToken) =>
        dbContext.EmployeeProjectAssignments.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.DeletedAt == null && x.Status == ProjectAssignmentStatuses.Active && x.ValidFrom <= date && (x.ValidUntil == null || x.ValidUntil >= date))
            .OrderByDescending(x => x.AllocationPercent).ThenByDescending(x => x.ValidFrom)
            .Select(x => new MealProjectSnapshot(x.ProjectId, x.CostCenterId))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<MealRate?> FindApplicableRateAsync(Guid campId, Guid mealTypeId, DateOnly date, CancellationToken cancellationToken) =>
        dbContext.MealRates.AsNoTracking().FirstOrDefaultAsync(
            x => x.CampId == campId && x.MealTypeId == mealTypeId && x.DeletedAt == null && x.ValidFrom <= date && (x.ValidUntilExclusive == null || date < x.ValidUntilExclusive),
            cancellationToken);

    public async Task<IReadOnlyList<MealRateSummary>> ListRatesAsync(Guid campId, Guid? mealTypeId, CancellationToken cancellationToken)
    {
        var query =
            from rate in dbContext.MealRates.AsNoTracking()
            join type in dbContext.MealTypes.AsNoTracking() on rate.MealTypeId equals type.Id
            where rate.CampId == campId && rate.DeletedAt == null && type.IsActive
            select new { rate, type };
        if (mealTypeId is not null) query = query.Where(x => x.rate.MealTypeId == mealTypeId.Value);
        return await query.OrderBy(x => x.type.DisplayOrder).ThenByDescending(x => x.rate.ValidFrom)
            .Select(x => new MealRateSummary(x.rate.Id, x.rate.CampId, x.rate.MealTypeId, x.type.Code, x.type.Name, x.rate.ValidFrom, x.rate.ValidUntilExclusive, x.rate.UnitPrice, x.rate.Currency, x.rate.Version))
            .ToListAsync(cancellationToken);
    }

    public void AddRate(MealRate rate) => dbContext.MealRates.Add(rate);

    public Task<MealConsumption?> FindDuplicateConsumptionAsync(Guid employeeId, DateOnly date, Guid mealTypeId, CancellationToken cancellationToken) =>
        dbContext.MealConsumptions.AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.ConsumptionDate == date && x.MealTypeId == mealTypeId && x.DeletedAt == null, cancellationToken);

    public Task<MealConsumption?> FindByExternalEventAsync(Guid companyId, string source, string externalEventId, CancellationToken cancellationToken) =>
        dbContext.MealConsumptions.AsNoTracking().FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Source == source && x.ExternalEventId == externalEventId && x.DeletedAt == null, cancellationToken);

    public async Task<MealConsumptionSummary?> GetConsumptionSummaryAsync(Guid consumptionId, CancellationToken cancellationToken) =>
        await SummaryQuery().FirstOrDefaultAsync(x => x.Id == consumptionId, cancellationToken);

    public async Task<MealPagedResult<MealConsumptionSummary>> SearchConsumptionsAsync(MealConsumptionQuery query, bool globalAccess, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken)
    {
        var source = SummaryQuery();
        if (!globalAccess) source = source.Where(x => companyIds.Contains(x.CompanyId));
        if (query.EmployeeId is not null) source = source.Where(x => x.EmployeeId == query.EmployeeId.Value);
        if (query.CampId is not null) source = source.Where(x => x.CampId == query.CampId.Value);
        if (query.MealTypeId is not null) source = source.Where(x => x.MealTypeId == query.MealTypeId.Value);
        if (query.From is not null) source = source.Where(x => x.ConsumptionDate >= query.From.Value);
        if (query.To is not null) source = source.Where(x => x.ConsumptionDate <= query.To.Value);
        var total = await source.CountAsync(cancellationToken);
        var items = await source.OrderByDescending(x => x.ConsumptionDate).ThenBy(x => x.EmployeeNo)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return new MealPagedResult<MealConsumptionSummary>(items, query.Page, query.PageSize, total);
    }

    public void AddConsumption(MealConsumption consumption) => dbContext.MealConsumptions.Add(consumption);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);

    private IQueryable<MealConsumptionSummary> SummaryQuery() =>
        from consumption in dbContext.MealConsumptions.AsNoTracking()
        join employee in dbContext.Employees.AsNoTracking() on consumption.EmployeeId equals employee.Id
        join camp in dbContext.Camps.AsNoTracking() on consumption.CampId equals camp.Id
        join type in dbContext.MealTypes.AsNoTracking() on consumption.MealTypeId equals type.Id
        where consumption.DeletedAt == null && employee.DeletedAt == null && camp.DeletedAt == null
        select new MealConsumptionSummary(
            consumption.Id,
            consumption.CompanyId,
            consumption.EmployeeId,
            employee.EmployeeNo,
            employee.FirstName + " " + employee.LastName,
            consumption.CampId,
            camp.Code,
            camp.Name,
            consumption.MealTypeId,
            type.Code,
            type.Name,
            consumption.MealRateId,
            consumption.ConsumptionDate,
            consumption.Quantity,
            consumption.UnitPriceSnapshot,
            consumption.CurrencySnapshot,
            consumption.TotalCostSnapshot,
            consumption.ProjectIdSnapshot,
            consumption.CostCenterIdSnapshot,
            consumption.Source,
            consumption.ExternalEventId,
            consumption.Note,
            consumption.Version);
}
