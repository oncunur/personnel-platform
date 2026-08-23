using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Payroll;
using PersonnelPlatform.Domain.Attendance;
using PersonnelPlatform.Domain.Camp;
using PersonnelPlatform.Domain.Payroll;
using PersonnelPlatform.Domain.Personnel;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Payroll;

public sealed class PayrollRepository(ApplicationDbContext dbContext) : IPayrollRepository
{
    public Task<Employee?> FindEmployeeAsync(Guid employeeId, CancellationToken cancellationToken) =>
        dbContext.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == employeeId && x.DeletedAt == null, cancellationToken);

    public Task<EmployeeCompensation?> FindCompensationAsync(Guid compensationId, CancellationToken cancellationToken) =>
        dbContext.EmployeeCompensations.FirstOrDefaultAsync(x => x.Id == compensationId && x.DeletedAt == null, cancellationToken);

    public Task<EmployeeCompensation?> FindOverlappingCompensationAsync(Guid employeeId, DateOnly validFrom, DateOnly? validUntilExclusive, CancellationToken cancellationToken) =>
        dbContext.EmployeeCompensations.AsNoTracking().FirstOrDefaultAsync(
            x => x.EmployeeId == employeeId
                 && x.DeletedAt == null
                 && (validUntilExclusive == null || x.ValidFrom < validUntilExclusive.Value)
                 && (x.ValidUntilExclusive == null || x.ValidUntilExclusive > validFrom),
            cancellationToken);

    public async Task<IReadOnlyList<EmployeeCompensationSummary>> ListCompensationsAsync(Guid employeeId, CancellationToken cancellationToken) =>
        await (
            from compensation in dbContext.EmployeeCompensations.AsNoTracking()
            join employee in dbContext.Employees.AsNoTracking() on compensation.EmployeeId equals employee.Id
            where compensation.EmployeeId == employeeId && compensation.DeletedAt == null && employee.DeletedAt == null
            orderby compensation.ValidFrom descending
            select new EmployeeCompensationSummary(
                compensation.Id,
                compensation.CompanyId,
                compensation.EmployeeId,
                employee.EmployeeNo,
                employee.FirstName + " " + employee.LastName,
                compensation.ValidFrom,
                compensation.ValidUntilExclusive,
                compensation.MonthlyBaseSalary,
                compensation.Currency,
                compensation.OvertimeMultiplier,
                compensation.Version))
        .ToListAsync(cancellationToken);

    public void AddCompensation(EmployeeCompensation compensation) => dbContext.EmployeeCompensations.Add(compensation);

    public Task<PayrollPeriod?> FindPeriodAsync(Guid periodId, CancellationToken cancellationToken) =>
        dbContext.PayrollPeriods.FirstOrDefaultAsync(x => x.Id == periodId && x.DeletedAt == null, cancellationToken);

    public Task<PayrollPeriod?> FindLatestPeriodAsync(Guid companyId, int year, int month, CancellationToken cancellationToken) =>
        dbContext.PayrollPeriods.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.Year == year && x.Month == month && x.DeletedAt == null)
            .OrderByDescending(x => x.Revision)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PayrollPeriodSummary>> ListPeriodsAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, int? year, CancellationToken cancellationToken)
    {
        var query = dbContext.PayrollPeriods.AsNoTracking().Where(x => x.DeletedAt == null);
        if (!globalAccess) query = query.Where(x => companyIds.Contains(x.CompanyId));
        if (year is not null) query = query.Where(x => x.Year == year.Value);
        return await query
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .ThenByDescending(x => x.Revision)
            .Select(x => new PayrollPeriodSummary(
                x.Id, x.CompanyId, x.Year, x.Month, x.Revision, x.PreviousRevisionId, x.Status,
                x.CalculationVersion, x.CalculatedAt, x.ApprovedAt, x.ClosedAt, x.Version))
            .ToListAsync(cancellationToken);
    }

    public void AddPeriod(PayrollPeriod period) => dbContext.PayrollPeriods.Add(period);

    public async Task<IReadOnlyList<PayrollCalculationSource>> BuildCalculationSourcesAsync(Guid companyId, DateOnly periodStart, DateOnly periodEndExclusive, CancellationToken cancellationToken)
    {
        var employees = await dbContext.Employees.AsNoTracking()
            .Where(x => x.CompanyId == companyId
                        && x.DeletedAt == null
                        && x.HireDate < periodEndExclusive
                        && (x.TerminationDate == null || x.TerminationDate >= periodStart))
            .OrderBy(x => x.EmployeeNo)
            .ToListAsync(cancellationToken);
        if (employees.Count == 0) return [];

        var employeeIds = employees.Select(x => x.Id).ToArray();
        var compensations = await dbContext.EmployeeCompensations.AsNoTracking()
            .Where(x => x.CompanyId == companyId
                        && employeeIds.Contains(x.EmployeeId)
                        && x.DeletedAt == null
                        && x.ValidFrom <= periodStart
                        && (x.ValidUntilExclusive == null || x.ValidUntilExclusive > periodStart))
            .OrderByDescending(x => x.ValidFrom)
            .ToListAsync(cancellationToken);
        var compensationByEmployee = compensations.GroupBy(x => x.EmployeeId).ToDictionary(x => x.Key, x => x.First());

        var dailyRows = await dbContext.DailyAttendances.AsNoTracking()
            .Where(x => x.CompanyId == companyId
                        && employeeIds.Contains(x.EmployeeId)
                        && x.DeletedAt == null
                        && x.AttendanceDate >= periodStart
                        && x.AttendanceDate < periodEndExclusive)
            .ToListAsync(cancellationToken);

        var leaveIds = dailyRows.Where(x => x.LeaveId is not null).Select(x => x.LeaveId!.Value).Distinct().ToArray();
        var paidLeaveIds = leaveIds.Length == 0
            ? new HashSet<Guid>()
            : (await (
                from leave in dbContext.LeaveRequests.AsNoTracking()
                join leaveType in dbContext.LeaveTypes.AsNoTracking() on leave.LeaveTypeId equals leaveType.Id
                where leaveIds.Contains(leave.Id)
                      && leave.DeletedAt == null
                      && leaveType.DeletedAt == null
                      && leaveType.IsPaid
                select leave.Id)
                .ToListAsync(cancellationToken)).ToHashSet();

        var overtimeRows = await dbContext.OvertimeRequests.AsNoTracking()
            .Where(x => x.CompanyId == companyId
                        && employeeIds.Contains(x.EmployeeId)
                        && x.DeletedAt == null
                        && x.Status == OvertimeRequestStatuses.Approved
                        && x.AttendanceDate >= periodStart
                        && x.AttendanceDate < periodEndExclusive)
            .ToListAsync(cancellationToken);

        var mealRows = await dbContext.MealConsumptions.AsNoTracking()
            .Where(x => x.CompanyId == companyId
                        && employeeIds.Contains(x.EmployeeId)
                        && x.DeletedAt == null
                        && x.ConsumptionDate >= periodStart
                        && x.ConsumptionDate < periodEndExclusive)
            .ToListAsync(cancellationToken);

        var stayRows = await dbContext.AccommodationStays.AsNoTracking()
            .Where(x => x.CompanyId == companyId
                        && employeeIds.Contains(x.EmployeeId)
                        && x.DeletedAt == null
                        && x.Status != AccommodationStayStatuses.Cancelled
                        && x.CheckInDate < periodEndExclusive
                        && (x.CheckOutDateExclusive == null || x.CheckOutDateExclusive > periodStart))
            .ToListAsync(cancellationToken);

        var assignments = await dbContext.EmployeeProjectAssignments.AsNoTracking()
            .Where(x => employeeIds.Contains(x.EmployeeId)
                        && x.DeletedAt == null
                        && x.ValidFrom < periodEndExclusive
                        && (x.ValidUntil == null || x.ValidUntil >= periodStart))
            .ToListAsync(cancellationToken);

        var result = new List<PayrollCalculationSource>(employees.Count);
        foreach (var employee in employees)
        {
            compensationByEmployee.TryGetValue(employee.Id, out var compensation);
            var employeeDaily = dailyRows.Where(x => x.EmployeeId == employee.Id).ToArray();
            var employeeOvertime = overtimeRows.Where(x => x.EmployeeId == employee.Id).ToArray();
            var employeeMeals = mealRows.Where(x => x.EmployeeId == employee.Id).ToArray();
            var employeeStays = stayRows.Where(x => x.EmployeeId == employee.Id).ToArray();

            var mealCosts = employeeMeals
                .GroupBy(x => x.CurrencySnapshot)
                .Select(x => new PayrollCurrencyAmount(x.Key, decimal.Round(x.Sum(y => y.TotalCostSnapshot), 2, MidpointRounding.AwayFromZero)))
                .OrderBy(x => x.Currency)
                .ToArray();

            var accommodationEntries = employeeStays.Select(x =>
            {
                var from = x.CheckInDate < periodStart ? periodStart : x.CheckInDate;
                var rawEnd = x.CheckOutDateExclusive ?? periodEndExclusive;
                var to = rawEnd > periodEndExclusive ? periodEndExclusive : rawEnd;
                var nights = Math.Max(0, to.DayNumber - from.DayNumber);
                return new { x.CurrencySnapshot, Cost = decimal.Round(nights * x.NightlyRateSnapshot, 2, MidpointRounding.AwayFromZero) };
            }).Where(x => x.Cost > 0m).ToArray();

            var accommodationCosts = accommodationEntries
                .GroupBy(x => x.CurrencySnapshot)
                .Select(x => new PayrollCurrencyAmount(x.Key, decimal.Round(x.Sum(y => y.Cost), 2, MidpointRounding.AwayFromZero)))
                .OrderBy(x => x.Currency)
                .ToArray();

            result.Add(new PayrollCalculationSource(
                employee.Id,
                employee.EmployeeNo,
                $"{employee.FirstName} {employee.LastName}",
                compensation?.Id,
                compensation?.MonthlyBaseSalary,
                compensation?.Currency,
                compensation?.OvertimeMultiplier,
                employeeDaily.Sum(x => x.PlannedMinutes),
                employeeDaily.Sum(x => x.WorkedMinutes),
                employeeDaily.Where(x => x.LeaveId is not null && paidLeaveIds.Contains(x.LeaveId.Value)).Sum(x => x.LeaveMinutes),
                employeeOvertime.Sum(x => x.ApprovedMinutes),
                employeeDaily.Count(x => x.ProcessingStatus is not (DailyAttendanceProcessingStatuses.Approved or DailyAttendanceProcessingStatuses.Locked)),
                mealCosts,
                accommodationCosts,
                employeeDaily.Select(x => new PayrollSourceRef(x.Id, x.Version)).OrderBy(x => x.Id).ToArray(),
                employeeOvertime.Select(x => new PayrollSourceRef(x.Id, x.Version)).OrderBy(x => x.Id).ToArray(),
                employeeMeals.Select(x => new PayrollSourceRef(x.Id, x.Version)).OrderBy(x => x.Id).ToArray(),
                employeeStays.Select(x => new PayrollSourceRef(x.Id, x.Version)).OrderBy(x => x.Id).ToArray(),
                assignments.Where(x => x.EmployeeId == employee.Id)
                    .Select(x => new PayrollProjectAllocationSnapshot(x.Id, x.ProjectId, x.CostCenterId, x.ValidFrom, x.ValidUntil, x.AllocationPercent))
                    .OrderBy(x => x.ValidFrom)
                    .ThenBy(x => x.ProjectId)
                    .ToArray()));
        }
        return result;
    }

    public async Task<IReadOnlyList<PayrollEmployeeResultSummary>> ListResultsAsync(Guid periodId, CancellationToken cancellationToken) =>
        await (
            from result in dbContext.PayrollEmployeeResults.AsNoTracking()
            join employee in dbContext.Employees.AsNoTracking() on result.EmployeeId equals employee.Id
            where result.PayrollPeriodId == periodId && employee.DeletedAt == null
            orderby employee.EmployeeNo
            select new PayrollEmployeeResultSummary(
                result.Id,
                result.PayrollPeriodId,
                result.EmployeeId,
                employee.EmployeeNo,
                employee.FirstName + " " + employee.LastName,
                result.MonthlyBaseSalarySnapshot,
                result.CurrencySnapshot,
                result.OvertimeMultiplierSnapshot,
                result.PlannedMinutes,
                result.WorkedMinutes,
                result.PaidLeaveMinutes,
                result.ApprovedOvertimeMinutes,
                result.BaseSalaryAmount,
                result.AbsenceDeductionAmount,
                result.OvertimeEarningAmount,
                result.PayBeforeStatutory,
                result.MealEmployerCost,
                result.AccommodationEmployerCost,
                result.EmployerCostBeforeStatutory,
                result.CalculatedAt))
        .ToListAsync(cancellationToken);

    public void AddResult(PayrollEmployeeResult result) => dbContext.PayrollEmployeeResults.Add(result);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
