namespace PersonnelPlatform.Application.Finance;

public static class FinancePermissions
{
    public const string CostView = "finance.cost.view";
    public const string CostProcess = "finance.cost.process";
    public const string AllocationView = "finance.allocation.view";
    public const string AllocationManage = "finance.allocation.manage";
}

public sealed record CostLedgerItem(
    Guid Id,
    Guid CompanyId,
    string SourceType,
    Guid SourceId,
    string SourceLineKey,
    Guid? EmployeeId,
    string? EmployeeNo,
    string? EmployeeName,
    Guid? ProjectId,
    string? ProjectCode,
    string? ProjectName,
    Guid? CostCenterId,
    string? CostCenterCode,
    DateOnly CostDate,
    string Category,
    decimal Quantity,
    string Unit,
    decimal Amount,
    string Currency,
    string AllocationBasis,
    string MetadataJson,
    DateTimeOffset CreatedAt);

public sealed record PayrollAllocationLine(Guid ProjectId, Guid? CostCenterId, decimal AllocationPercent);
public sealed record ReplacePayrollAllocationRequest(int PayrollPeriodVersion, IReadOnlyList<PayrollAllocationLine> Lines);
public sealed record PayrollAllocationSummary(Guid Id, Guid PayrollPeriodId, Guid CompanyId, Guid EmployeeId, Guid ProjectId, Guid? CostCenterId, decimal AllocationPercent, int Version);

public sealed record CostSyncResult(int PayrollSources, int PayrollEntriesCreated, int MealEntriesCreated, int AccommodationEntriesCreated, int Duplicates);

public sealed record FinanceResult<T>(bool Succeeded, T? Value, string? ErrorCode, string? ErrorMessage) where T : class
{
    public static FinanceResult<T> Success(T value) => new(true, value, null, null);
    public static FinanceResult<T> Failure(string code, string message) => new(false, null, code, message);
}
