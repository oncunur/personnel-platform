namespace PersonnelPlatform.Application.Payroll;

public static class PayrollPermissions
{
    public const string CompensationView = "payroll.compensation.view";
    public const string CompensationManage = "payroll.compensation.manage";
    public const string PeriodView = "payroll.period.view";
    public const string PeriodManage = "payroll.period.manage";
    public const string Calculate = "payroll.calculate";
    public const string Review = "payroll.review";
    public const string Approve = "payroll.approve";
    public const string Close = "payroll.close";
}

public sealed record CreateEmployeeCompensationRequest(Guid EmployeeId, DateOnly ValidFrom, DateOnly? ValidUntilExclusive, decimal MonthlyBaseSalary, string Currency, decimal OvertimeMultiplier);
public sealed record EmployeeCompensationSummary(Guid Id, Guid CompanyId, Guid EmployeeId, string EmployeeNo, string EmployeeName, DateOnly ValidFrom, DateOnly? ValidUntilExclusive, decimal MonthlyBaseSalary, string Currency, decimal OvertimeMultiplier, int Version);

public sealed record CreatePayrollPeriodRequest(Guid CompanyId, int Year, int Month);
public sealed record PayrollPeriodActionRequest(int Version);
public sealed record PayrollPeriodSummary(Guid Id, Guid CompanyId, int Year, int Month, int Revision, Guid? PreviousRevisionId, string Status, string CalculationVersion, DateTimeOffset? CalculatedAt, DateTimeOffset? ApprovedAt, DateTimeOffset? ClosedAt, int Version);

public sealed record PayrollEmployeeResultSummary(
    Guid Id,
    Guid PayrollPeriodId,
    Guid EmployeeId,
    string EmployeeNo,
    string EmployeeName,
    decimal MonthlyBaseSalarySnapshot,
    string CurrencySnapshot,
    decimal OvertimeMultiplierSnapshot,
    int PlannedMinutes,
    int WorkedMinutes,
    int PaidLeaveMinutes,
    int ApprovedOvertimeMinutes,
    decimal BaseSalaryAmount,
    decimal AbsenceDeductionAmount,
    decimal OvertimeEarningAmount,
    decimal PayBeforeStatutory,
    decimal MealEmployerCost,
    decimal AccommodationEmployerCost,
    decimal EmployerCostBeforeStatutory,
    DateTimeOffset CalculatedAt);

public sealed record PayrollCurrencyAmount(string Currency, decimal Amount);
public sealed record PayrollSourceRef(Guid Id, int Version);
public sealed record PayrollProjectAllocationSnapshot(Guid AssignmentId, Guid ProjectId, Guid? CostCenterId, DateOnly ValidFrom, DateOnly? ValidUntil, decimal AllocationPercent);

public sealed record PayrollCalculationSource(
    Guid EmployeeId,
    string EmployeeNo,
    string EmployeeName,
    Guid? CompensationId,
    decimal? MonthlyBaseSalary,
    string? Currency,
    decimal? OvertimeMultiplier,
    int PlannedMinutes,
    int WorkedMinutes,
    int PaidLeaveMinutes,
    int ApprovedOvertimeMinutes,
    int UnapprovedAttendanceCount,
    IReadOnlyList<PayrollCurrencyAmount> MealCosts,
    IReadOnlyList<PayrollCurrencyAmount> AccommodationCosts,
    IReadOnlyList<PayrollSourceRef> DailyAttendanceRefs,
    IReadOnlyList<PayrollSourceRef> ApprovedOvertimeRefs,
    IReadOnlyList<PayrollSourceRef> MealConsumptionRefs,
    IReadOnlyList<PayrollSourceRef> AccommodationStayRefs,
    IReadOnlyList<PayrollProjectAllocationSnapshot> ProjectAllocations);

public sealed record PayrollResult<T>(bool Succeeded, T? Value, string? ErrorCode, string? ErrorMessage) where T : class
{
    public static PayrollResult<T> Success(T value) => new(true, value, null, null);
    public static PayrollResult<T> Failure(string code, string message) => new(false, null, code, message);
}
