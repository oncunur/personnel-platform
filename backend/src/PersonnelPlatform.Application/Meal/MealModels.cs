namespace PersonnelPlatform.Application.Meal;

public static class MealPermissions
{
    public const string TypeView = "meal.type.view";
    public const string RateView = "meal.rate.view";
    public const string RateManage = "meal.rate.manage";
    public const string ConsumptionView = "meal.consumption.view";
    public const string ConsumptionRecord = "meal.consumption.record";
}

public sealed record MealTypeSummary(Guid Id, string Code, string Name, int DisplayOrder);
public sealed record MealRateSummary(Guid Id, Guid CampId, Guid MealTypeId, string MealTypeCode, string MealTypeName, DateOnly ValidFrom, DateOnly? ValidUntilExclusive, decimal UnitPrice, string Currency, int Version);
public sealed record CreateMealRateRequest(Guid CampId, Guid MealTypeId, DateOnly ValidFrom, DateOnly? ValidUntilExclusive, decimal UnitPrice, string Currency);

public sealed record CreateMealConsumptionRequest(
    Guid EmployeeId,
    Guid CampId,
    Guid MealTypeId,
    DateOnly ConsumptionDate,
    decimal Quantity,
    string Source,
    string? ExternalEventId,
    string? Note);

public sealed record MealConsumptionSummary(
    Guid Id,
    Guid CompanyId,
    Guid EmployeeId,
    string EmployeeNo,
    string EmployeeName,
    Guid CampId,
    string CampCode,
    string CampName,
    Guid MealTypeId,
    string MealTypeCode,
    string MealTypeName,
    Guid MealRateId,
    DateOnly ConsumptionDate,
    decimal Quantity,
    decimal UnitPriceSnapshot,
    string CurrencySnapshot,
    decimal TotalCostSnapshot,
    Guid? ProjectIdSnapshot,
    Guid? CostCenterIdSnapshot,
    string Source,
    string? ExternalEventId,
    string? Note,
    int Version);

public sealed record MealConsumptionQuery(Guid? EmployeeId, Guid? CampId, Guid? MealTypeId, DateOnly? From, DateOnly? To, int Page, int PageSize);
public sealed record MealPagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
public sealed record MealProjectSnapshot(Guid ProjectId, Guid? CostCenterId);

public sealed record MealResult<T>(bool Succeeded, T? Value, string? ErrorCode, string? ErrorMessage) where T : class
{
    public static MealResult<T> Success(T value) => new(true, value, null, null);
    public static MealResult<T> Failure(string code, string message) => new(false, null, code, message);
}
