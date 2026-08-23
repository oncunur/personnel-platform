using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Finance;
using PersonnelPlatform.Domain.Camp;
using PersonnelPlatform.Domain.Finance;
using PersonnelPlatform.Domain.Payroll;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Finance;

public sealed class FinanceRepository(ApplicationDbContext db) : IFinanceRepository
{
    public Task<PayrollPeriod?> FindPayrollPeriodAsync(Guid payrollPeriodId, CancellationToken ct) =>
        db.PayrollPeriods.FirstOrDefaultAsync(x => x.Id == payrollPeriodId && x.DeletedAt == null, ct);

    public Task<PayrollEmployeeResult?> FindPayrollResultAsync(Guid payrollPeriodId, Guid employeeId, CancellationToken ct) =>
        db.PayrollEmployeeResults.FirstOrDefaultAsync(x => x.PayrollPeriodId == payrollPeriodId && x.EmployeeId == employeeId, ct);

    public async Task<IReadOnlyList<PayrollCostAllocationOverride>> ListManualAllocationsAsync(Guid payrollPeriodId, Guid employeeId, CancellationToken ct) =>
        await db.PayrollCostAllocationOverrides.AsNoTracking().Where(x => x.PayrollPeriodId == payrollPeriodId && x.EmployeeId == employeeId && x.DeletedAt == null).OrderBy(x => x.ProjectId).ThenBy(x => x.CostCenterId).ToListAsync(ct);

    public async Task ReplaceManualAllocationsAsync(Guid payrollPeriodId, Guid companyId, Guid employeeId, IReadOnlyList<PayrollCostAllocationOverride> rows, CancellationToken ct)
    {
        var existing = await db.PayrollCostAllocationOverrides.Where(x => x.PayrollPeriodId == payrollPeriodId && x.EmployeeId == employeeId).ToListAsync(ct);
        db.PayrollCostAllocationOverrides.RemoveRange(existing);
        db.PayrollCostAllocationOverrides.AddRange(rows);
    }

    public async Task<IReadOnlyList<PayrollAllocationSummary>> ListManualAllocationSummariesAsync(Guid payrollPeriodId, Guid employeeId, CancellationToken ct) =>
        await db.PayrollCostAllocationOverrides.AsNoTracking()
            .Where(x => x.PayrollPeriodId == payrollPeriodId && x.EmployeeId == employeeId && x.DeletedAt == null)
            .OrderBy(x => x.ProjectId).ThenBy(x => x.CostCenterId)
            .Select(x => new PayrollAllocationSummary(x.Id, x.PayrollPeriodId, x.CompanyId, x.EmployeeId, x.ProjectId, x.CostCenterId, x.AllocationPercent, x.Version))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PayrollCostSource>> ListClosedPayrollSourcesAsync(IReadOnlyCollection<Guid>? companyIds, CancellationToken ct)
    {
        var q = from result in db.PayrollEmployeeResults.AsNoTracking()
                join period in db.PayrollPeriods.AsNoTracking() on result.PayrollPeriodId equals period.Id
                where period.DeletedAt == null && period.Status == PayrollPeriodStatuses.Closed
                select new { period, result };
        if (companyIds is not null) q = q.Where(x => companyIds.Contains(x.result.CompanyId));
        var rows = await q.OrderBy(x => x.period.Year).ThenBy(x => x.period.Month).ThenBy(x => x.result.EmployeeId).ToListAsync(ct);
        return rows.Select(x => new PayrollCostSource(x.period, x.result)).ToArray();
    }

    public async Task<IReadOnlyList<Domain.Attendance.DailyAttendance>> ListDailyAttendanceAsync(Guid employeeId, DateOnly from, DateOnly toExclusive, CancellationToken ct) =>
        await db.DailyAttendances.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.DeletedAt == null && x.AttendanceDate >= from && x.AttendanceDate < toExclusive).OrderBy(x => x.AttendanceDate).ToListAsync(ct);

    public async Task<IReadOnlyList<Domain.Personnel.EmployeeProjectAssignment>> ListProjectAssignmentsAsync(Guid employeeId, DateOnly from, DateOnly toExclusive, CancellationToken ct) =>
        await db.EmployeeProjectAssignments.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.DeletedAt == null && x.Status == Domain.Personnel.ProjectAssignmentStatuses.Active && x.ValidFrom < toExclusive && (x.ValidUntil == null || x.ValidUntil >= from)).OrderBy(x => x.ValidFrom).ToListAsync(ct);

    public async Task<IReadOnlyList<Domain.Meal.MealConsumption>> ListMealSourcesAsync(IReadOnlyCollection<Guid>? companyIds, CancellationToken ct)
    {
        var q = db.MealConsumptions.AsNoTracking().Where(x => x.DeletedAt == null);
        if (companyIds is not null) q = q.Where(x => companyIds.Contains(x.CompanyId));
        return await q.OrderBy(x => x.ConsumptionDate).ThenBy(x => x.Id).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AccommodationStay>> ListAccommodationSourcesAsync(IReadOnlyCollection<Guid>? companyIds, CancellationToken ct)
    {
        var q = db.AccommodationStays.AsNoTracking().Where(x => x.DeletedAt == null && x.Status == AccommodationStayStatuses.Closed);
        if (companyIds is not null) q = q.Where(x => companyIds.Contains(x.CompanyId));
        return await q.OrderBy(x => x.CheckInDate).ThenBy(x => x.Id).ToListAsync(ct);
    }

    public Task<bool> SourceHasCostEntriesAsync(string sourceType, Guid sourceId, CancellationToken ct) =>
        db.CostEntries.AsNoTracking().AnyAsync(x => x.SourceType == sourceType && x.SourceId == sourceId, ct);

    public Task<bool> CostEntryExistsAsync(string sourceType, Guid sourceId, string sourceLineKey, CancellationToken ct) =>
        db.CostEntries.AsNoTracking().AnyAsync(x => x.SourceType == sourceType && x.SourceId == sourceId && x.SourceLineKey == sourceLineKey, ct);

    public async Task<bool> TryInsertCostEntryAsync(CostEntry e, CancellationToken ct)
    {
        var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO finance.cost_entries
                (id, company_id, source_type, source_id, source_line_key, employee_id, project_id, cost_center_id, cost_date,
                 category, quantity, unit, amount, currency, allocation_basis, metadata_json, created_at)
            VALUES ({e.Id}, {e.CompanyId}, {e.SourceType}, {e.SourceId}, {e.SourceLineKey}, {e.EmployeeId}, {e.ProjectId}, {e.CostCenterId}, {e.CostDate},
                    {e.Category}, {e.Quantity}, {e.Unit}, {e.Amount}, {e.Currency}, {e.AllocationBasis}, CAST({e.MetadataJson} AS jsonb), {e.CreatedAt})
            ON CONFLICT (source_type, source_id, source_line_key) DO NOTHING
            """, ct);
        return affected > 0;
    }

    public async Task<IReadOnlyList<CostLedgerItem>> ListCostLedgerAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, Guid? projectId, Guid? costCenterId, Guid? employeeId, string? sourceType, DateOnly? from, DateOnly? to, int take, CancellationToken ct)
    {
        var q = db.CostEntries.AsNoTracking().AsQueryable();
        if (!globalAccess) q = q.Where(x => companyIds.Contains(x.CompanyId));
        if (companyId is not null) q = q.Where(x => x.CompanyId == companyId.Value);
        if (projectId is not null) q = q.Where(x => x.ProjectId == projectId.Value);
        if (costCenterId is not null) q = q.Where(x => x.CostCenterId == costCenterId.Value);
        if (employeeId is not null) q = q.Where(x => x.EmployeeId == employeeId.Value);
        if (sourceType is not null) q = q.Where(x => x.SourceType == sourceType);
        if (from is not null) q = q.Where(x => x.CostDate >= from.Value);
        if (to is not null) q = q.Where(x => x.CostDate <= to.Value);
        var rows = await q.OrderByDescending(x => x.CostDate).ThenByDescending(x => x.CreatedAt).Take(take).ToListAsync(ct);
        if (rows.Count == 0) return [];

        var employeeIds = rows.Where(x => x.EmployeeId != null).Select(x => x.EmployeeId!.Value).Distinct().ToArray();
        var projectIds = rows.Where(x => x.ProjectId != null).Select(x => x.ProjectId!.Value).Distinct().ToArray();
        var costCenterIds = rows.Where(x => x.CostCenterId != null).Select(x => x.CostCenterId!.Value).Distinct().ToArray();
        var employees = await db.Employees.AsNoTracking().Where(x => employeeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var projects = await db.Projects.AsNoTracking().Where(x => projectIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var costCenters = await db.CostCenters.AsNoTracking().Where(x => costCenterIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);

        return rows.Select(x =>
        {
            employees.TryGetValue(x.EmployeeId ?? Guid.Empty, out var employee);
            projects.TryGetValue(x.ProjectId ?? Guid.Empty, out var project);
            costCenters.TryGetValue(x.CostCenterId ?? Guid.Empty, out var costCenter);
            return new CostLedgerItem(x.Id, x.CompanyId, x.SourceType, x.SourceId, x.SourceLineKey, x.EmployeeId,
                employee?.EmployeeNo, employee is null ? null : $"{employee.FirstName} {employee.LastName}",
                x.ProjectId, project?.Code, project?.Name, x.CostCenterId, costCenter?.Code, x.CostDate, x.Category, x.Quantity,
                x.Unit, x.Amount, x.Currency, x.AllocationBasis, x.MetadataJson, x.CreatedAt);
        }).ToArray();
    }

    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
