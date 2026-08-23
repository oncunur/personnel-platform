using PersonnelPlatform.Domain.Attendance;
using PersonnelPlatform.Domain.Finance;
using PersonnelPlatform.Domain.Meal;
using PersonnelPlatform.Domain.Camp;
using PersonnelPlatform.Domain.Payroll;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Application.Finance;

public sealed record PayrollCostSource(PayrollPeriod Period, PayrollEmployeeResult Result);

public interface IFinanceRepository
{
    Task<PayrollPeriod?> FindPayrollPeriodAsync(Guid payrollPeriodId, CancellationToken ct);
    Task<IReadOnlyList<PayrollCostAllocationOverride>> ListManualAllocationsAsync(Guid payrollPeriodId, Guid employeeId, CancellationToken ct);
    Task ReplaceManualAllocationsAsync(Guid payrollPeriodId, Guid companyId, Guid employeeId, IReadOnlyList<PayrollCostAllocationOverride> rows, CancellationToken ct);
    Task<IReadOnlyList<PayrollAllocationSummary>> ListManualAllocationSummariesAsync(Guid payrollPeriodId, Guid employeeId, CancellationToken ct);

    Task<IReadOnlyList<PayrollCostSource>> ListClosedPayrollSourcesAsync(IReadOnlyCollection<Guid>? companyIds, CancellationToken ct);
    Task<IReadOnlyList<DailyAttendance>> ListDailyAttendanceAsync(Guid employeeId, DateOnly from, DateOnly toExclusive, CancellationToken ct);
    Task<IReadOnlyList<EmployeeProjectAssignment>> ListProjectAssignmentsAsync(Guid employeeId, DateOnly from, DateOnly toExclusive, CancellationToken ct);
    Task<IReadOnlyList<MealConsumption>> ListMealSourcesAsync(IReadOnlyCollection<Guid>? companyIds, CancellationToken ct);
    Task<IReadOnlyList<AccommodationStay>> ListAccommodationSourcesAsync(IReadOnlyCollection<Guid>? companyIds, CancellationToken ct);

    Task<bool> CostEntryExistsAsync(string sourceType, Guid sourceId, string sourceLineKey, CancellationToken ct);
    Task<bool> TryInsertCostEntryAsync(CostEntry entry, CancellationToken ct);
    Task<IReadOnlyList<CostLedgerItem>> ListCostLedgerAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, Guid? projectId, Guid? costCenterId, Guid? employeeId, string? sourceType, DateOnly? from, DateOnly? to, int take, CancellationToken ct);
    Task<int> SaveChangesAsync(CancellationToken ct);
}
