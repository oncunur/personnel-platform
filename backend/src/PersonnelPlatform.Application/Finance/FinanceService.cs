using System.Text.Json;
using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Application.Organization;
using PersonnelPlatform.Application.Personnel;
using PersonnelPlatform.Domain.Finance;
using PersonnelPlatform.Domain.Payroll;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Application.Finance;

public sealed class FinanceService(
    IFinanceRepository repository,
    IOrganizationRepository organizationRepository,
    IPersonnelRepository personnelRepository,
    AccessControlService accessControlService,
    TimeProvider timeProvider)
{
    public async Task<FinanceResult<IReadOnlyList<CostLedgerItem>>> ListCostLedgerAsync(
        Guid userId,
        Guid? companyId,
        Guid? projectId,
        Guid? costCenterId,
        Guid? employeeId,
        string? sourceType,
        DateOnly? from,
        DateOnly? to,
        int take,
        CancellationToken ct)
    {
        if (from is not null && to is not null && to < from)
            return FinanceResult<IReadOnlyList<CostLedgerItem>>.Failure("REPORT_DATE_RANGE_INVALID", "Bitiş tarihi başlangıç tarihinden önce olamaz.");
        var access = await ResolveAccessAsync(userId, ct);
        if (companyId is not null && !access.Global && !access.CompanyIds.Contains(companyId.Value))
            return FinanceResult<IReadOnlyList<CostLedgerItem>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return FinanceResult<IReadOnlyList<CostLedgerItem>>.Success(await repository.ListCostLedgerAsync(
            access.Global, access.CompanyIds, companyId, projectId, costCenterId, employeeId, Normalize(sourceType), from, to, Math.Clamp(take, 1, 2000), ct));
    }

    public async Task<FinanceResult<IReadOnlyList<PayrollAllocationSummary>>> ListManualAllocationAsync(Guid userId, Guid payrollPeriodId, Guid employeeId, CancellationToken ct)
    {
        var period = await repository.FindPayrollPeriodAsync(payrollPeriodId, ct);
        if (period is null) return FinanceResult<IReadOnlyList<PayrollAllocationSummary>>.Failure("PAYROLL_PERIOD_NOT_FOUND", "Bordro dönemi bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, period.CompanyId, ct))
            return FinanceResult<IReadOnlyList<PayrollAllocationSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return FinanceResult<IReadOnlyList<PayrollAllocationSummary>>.Success(await repository.ListManualAllocationSummariesAsync(payrollPeriodId, employeeId, ct));
    }

    public async Task<FinanceResult<IReadOnlyList<PayrollAllocationSummary>>> ReplaceManualAllocationAsync(
        Guid userId,
        Guid payrollPeriodId,
        Guid employeeId,
        ReplacePayrollAllocationRequest request,
        CancellationToken ct)
    {
        var period = await repository.FindPayrollPeriodAsync(payrollPeriodId, ct);
        if (period is null) return FinanceResult<IReadOnlyList<PayrollAllocationSummary>>.Failure("PAYROLL_PERIOD_NOT_FOUND", "Bordro dönemi bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, period.CompanyId, ct))
            return FinanceResult<IReadOnlyList<PayrollAllocationSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (period.Version != request.PayrollPeriodVersion)
            return FinanceResult<IReadOnlyList<PayrollAllocationSummary>>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Bordro dönemi değişmiş. Veriyi yenileyin.");

        var payrollResult = await repository.FindPayrollResultAsync(payrollPeriodId, employeeId, ct);
        if (payrollResult is null) return FinanceResult<IReadOnlyList<PayrollAllocationSummary>>.Failure("PAYROLL_RESULT_NOT_FOUND", "Personelin bu dönem için bordro sonucu bulunamadı.");
        if (await repository.SourceHasCostEntriesAsync(CostSourceTypes.Payroll, payrollResult.Id, ct))
            return FinanceResult<IReadOnlyList<PayrollAllocationSummary>>.Failure("COST_LEDGER_SOURCE_LOCKED", "Bu bordro sonucu maliyet ledger'a işlendiği için allocation değiştirilemez.");

        var employee = await personnelRepository.FindEmployeeAsync(employeeId, ct);
        if (employee is null || employee.CompanyId != period.CompanyId)
            return FinanceResult<IReadOnlyList<PayrollAllocationSummary>>.Failure("EMPLOYEE_NOT_FOUND", "Şirket kapsamındaki personel bulunamadı.");

        var lines = request.Lines?.ToArray() ?? [];
        if (lines.Length == 0) return FinanceResult<IReadOnlyList<PayrollAllocationSummary>>.Failure("COST_ALLOCATION_REQUIRED", "En az bir allocation satırı zorunludur.");
        if (lines.GroupBy(x => new { x.ProjectId, x.CostCenterId }).Any(g => g.Count() > 1))
            return FinanceResult<IReadOnlyList<PayrollAllocationSummary>>.Failure("COST_ALLOCATION_DUPLICATE", "Aynı proje ve cost center birden fazla kez kullanılamaz.");
        if (lines.Any(x => x.AllocationPercent <= 0 || x.AllocationPercent > 100))
            return FinanceResult<IReadOnlyList<PayrollAllocationSummary>>.Failure("COST_ALLOCATION_PERCENT_INVALID", "Allocation yüzdesi 0 ile 100 arasında olmalıdır.");
        var total = decimal.Round(lines.Sum(x => x.AllocationPercent), 4, MidpointRounding.AwayFromZero);
        if (Math.Abs(total - 100m) > 0.0001m)
            return FinanceResult<IReadOnlyList<PayrollAllocationSummary>>.Failure("COST_ALLOCATION_TOTAL_INVALID", "Allocation toplamı %100 olmalıdır.");

        foreach (var line in lines)
        {
            var project = await organizationRepository.FindProjectAsync(line.ProjectId, ct);
            if (project is null || project.CompanyId != period.CompanyId)
                return FinanceResult<IReadOnlyList<PayrollAllocationSummary>>.Failure("PROJECT_NOT_FOUND", "Allocation projesi şirket kapsamında bulunamadı.");
            if (line.CostCenterId is { } costCenterId)
            {
                var costCenter = await organizationRepository.FindCostCenterAsync(costCenterId, ct);
                if (costCenter is null || costCenter.CompanyId != period.CompanyId || (costCenter.ProjectId is not null && costCenter.ProjectId != project.Id))
                    return FinanceResult<IReadOnlyList<PayrollAllocationSummary>>.Failure("COST_CENTER_NOT_FOUND", "Allocation cost center seçilen proje ile uyumlu değil.");
            }
        }

        var now = timeProvider.GetUtcNow();
        var rows = lines.Select(x => PayrollCostAllocationOverride.Create(payrollPeriodId, period.CompanyId, employeeId, x.ProjectId, x.CostCenterId, x.AllocationPercent, now, userId)).ToArray();
        await repository.ReplaceManualAllocationsAsync(payrollPeriodId, period.CompanyId, employeeId, rows, ct);
        await repository.SaveChangesAsync(ct);
        return FinanceResult<IReadOnlyList<PayrollAllocationSummary>>.Success(await repository.ListManualAllocationSummariesAsync(payrollPeriodId, employeeId, ct));
    }

    public async Task<FinanceResult<CostSyncResult>> SyncAsync(Guid userId, Guid? companyId, CancellationToken ct)
    {
        var access = await ResolveAccessAsync(userId, ct);
        if (companyId is not null && !access.Global && !access.CompanyIds.Contains(companyId.Value))
            return FinanceResult<CostSyncResult>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");

        IReadOnlyCollection<Guid>? companies = companyId is not null ? [companyId.Value] : access.Global ? null : access.CompanyIds;
        if (companies is { Count: 0 }) return FinanceResult<CostSyncResult>.Failure("SCOPE_DENIED", "Maliyet işlemek için şirket kapsamınız bulunmuyor.");

        var payrollSources = await repository.ListClosedPayrollSourcesAsync(companies, ct);
        var mealSources = await repository.ListMealSourcesAsync(companies, ct);
        var accommodationSources = await repository.ListAccommodationSourcesAsync(companies, ct);
        var now = timeProvider.GetUtcNow();
        var payrollCreated = 0;
        var mealCreated = 0;
        var accommodationCreated = 0;
        var duplicates = 0;

        foreach (var source in payrollSources)
        {
            if (await repository.SourceHasCostEntriesAsync(CostSourceTypes.Payroll, source.Result.Id, ct)) { duplicates++; continue; }
            var slices = await ResolvePayrollAllocationAsync(source, ct);
            var amountRemaining = decimal.Round(source.Result.PayBeforeStatutory, 2, MidpointRounding.AwayFromZero);
            for (var i = 0; i < slices.Count; i++)
            {
                var slice = slices[i];
                var amount = i == slices.Count - 1
                    ? amountRemaining
                    : decimal.Round(source.Result.PayBeforeStatutory * slice.Percent / 100m, 2, MidpointRounding.AwayFromZero);
                amountRemaining -= amount;
                var lineKey = $"{slice.Basis}:{slice.ProjectId?.ToString("N") ?? "UNALLOCATED"}:{slice.CostCenterId?.ToString("N") ?? "NONE"}";
                var metadata = JsonSerializer.Serialize(new
                {
                    payrollPeriodId = source.Period.Id,
                    source.Result.EmployeeId,
                    source.Period.Year,
                    source.Period.Month,
                    source.Period.Revision,
                    allocationPercent = slice.Percent,
                    allocationBasis = slice.Basis,
                    source.Result.PlannedMinutes,
                    source.Result.WorkedMinutes,
                    source.Result.PaidLeaveMinutes,
                    source.Result.ApprovedOvertimeMinutes,
                    source.Result.BaseSalaryAmount,
                    source.Result.AbsenceDeductionAmount,
                    source.Result.OvertimeEarningAmount
                });
                var entry = CostEntry.Create(source.Result.CompanyId, CostSourceTypes.Payroll, source.Result.Id, lineKey, source.Result.EmployeeId,
                    slice.ProjectId, slice.CostCenterId, source.Period.PeriodEndExclusive.AddDays(-1), CostCategories.Payroll, slice.Percent, "PERCENT", amount,
                    source.Result.CurrencySnapshot, slice.Basis, metadata, now);
                if (await repository.TryInsertCostEntryAsync(entry, ct)) payrollCreated++; else duplicates++;
            }
        }

        foreach (var meal in mealSources)
        {
            var metadata = JsonSerializer.Serialize(new { meal.CampId, meal.MealTypeId, meal.MealRateId, meal.Source, meal.ExternalEventId });
            var entry = CostEntry.Create(meal.CompanyId, CostSourceTypes.Meal, meal.Id, "MEAL", meal.EmployeeId, meal.ProjectIdSnapshot, meal.CostCenterIdSnapshot,
                meal.ConsumptionDate, CostCategories.Meal, meal.Quantity, "MEAL", meal.TotalCostSnapshot, meal.CurrencySnapshot,
                meal.ProjectIdSnapshot is null ? CostAllocationBases.Unallocated : CostAllocationBases.Direct, metadata, now);
            if (await repository.TryInsertCostEntryAsync(entry, ct)) mealCreated++; else duplicates++;
        }

        foreach (var stay in accommodationSources)
        {
            if (stay.CheckOutDateExclusive is null || stay.TotalCostSnapshot <= 0) continue;
            var nights = stay.CheckOutDateExclusive.Value.DayNumber - stay.CheckInDate.DayNumber;
            var metadata = JsonSerializer.Serialize(new { stay.CampId, stay.RoomId, stay.BedId, stay.RateId, stay.CheckInDate, stay.CheckOutDateExclusive });
            var entry = CostEntry.Create(stay.CompanyId, CostSourceTypes.Accommodation, stay.Id, "ACCOMMODATION", stay.EmployeeId, stay.ProjectIdSnapshot, stay.CostCenterIdSnapshot,
                stay.CheckOutDateExclusive.Value.AddDays(-1), CostCategories.Accommodation, nights, "NIGHT", stay.TotalCostSnapshot, stay.CurrencySnapshot,
                stay.ProjectIdSnapshot is null ? CostAllocationBases.Unallocated : CostAllocationBases.Direct, metadata, now);
            if (await repository.TryInsertCostEntryAsync(entry, ct)) accommodationCreated++; else duplicates++;
        }

        return FinanceResult<CostSyncResult>.Success(new CostSyncResult(payrollSources.Count, payrollCreated, mealCreated, accommodationCreated, duplicates));
    }

    private async Task<IReadOnlyList<AllocationSlice>> ResolvePayrollAllocationAsync(PayrollCostSource source, CancellationToken ct)
    {
        var manual = await repository.ListManualAllocationsAsync(source.Period.Id, source.Result.EmployeeId, ct);
        if (manual.Count > 0)
            return NormalizeSlices(manual.Select(x => new AllocationSlice(x.ProjectId, x.CostCenterId, x.AllocationPercent, CostAllocationBases.Manual)));

        var from = source.Period.PeriodStart;
        var to = source.Period.PeriodEndExclusive;
        var assignments = await repository.ListProjectAssignmentsAsync(source.Result.EmployeeId, from, to, ct);
        if (assignments.Count == 0) return [new AllocationSlice(null, null, 100m, CostAllocationBases.Unallocated)];

        var attendance = await repository.ListDailyAttendanceAsync(source.Result.EmployeeId, from, to, ct);
        var attendanceWeights = new Dictionary<(Guid ProjectId, Guid? CostCenterId), decimal>();
        foreach (var day in attendance)
        {
            var minutes = day.WorkedMinutes + day.LeaveMinutes;
            if (minutes <= 0) continue;
            var active = assignments.Where(x => x.ValidFrom <= day.AttendanceDate && (x.ValidUntil == null || x.ValidUntil >= day.AttendanceDate)).ToArray();
            if (active.Length == 0) continue;
            var totalPct = active.Sum(x => x.AllocationPercent);
            if (totalPct <= 0) continue;
            foreach (var assignment in active)
            {
                var key = (assignment.ProjectId, assignment.CostCenterId);
                attendanceWeights[key] = attendanceWeights.GetValueOrDefault(key) + minutes * assignment.AllocationPercent / totalPct;
            }
        }
        if (attendanceWeights.Count > 0)
            return NormalizeSlices(attendanceWeights.Select(x => new AllocationSlice(x.Key.ProjectId, x.Key.CostCenterId, x.Value, CostAllocationBases.Attendance)));

        var fixedWeights = assignments
            .GroupBy(x => new { x.ProjectId, x.CostCenterId })
            .Select(g =>
            {
                decimal weight = 0;
                foreach (var assignment in g)
                {
                    var start = assignment.ValidFrom > from ? assignment.ValidFrom : from;
                    var assignmentEndExclusive = assignment.ValidUntil is { } until ? until.AddDays(1) : to;
                    var end = assignmentEndExclusive < to ? assignmentEndExclusive : to;
                    var days = Math.Max(0, end.DayNumber - start.DayNumber);
                    weight += Math.Max(1, days) * assignment.AllocationPercent;
                }
                return new AllocationSlice(g.Key.ProjectId, g.Key.CostCenterId, weight, CostAllocationBases.Fixed);
            });
        return NormalizeSlices(fixedWeights);
    }

    private static IReadOnlyList<AllocationSlice> NormalizeSlices(IEnumerable<AllocationSlice> source)
    {
        var rows = source.Where(x => x.Percent > 0).ToArray();
        if (rows.Length == 0) return [new AllocationSlice(null, null, 100m, CostAllocationBases.Unallocated)];
        var total = rows.Sum(x => x.Percent);
        var result = new List<AllocationSlice>(rows.Length);
        decimal allocated = 0;
        for (var i = 0; i < rows.Length; i++)
        {
            var percent = i == rows.Length - 1 ? 100m - allocated : decimal.Round(rows[i].Percent / total * 100m, 4, MidpointRounding.AwayFromZero);
            allocated += percent;
            result.Add(rows[i] with { Percent = percent });
        }
        return result;
    }

    private async Task<(bool Global, HashSet<Guid> CompanyIds)> ResolveAccessAsync(Guid userId, CancellationToken ct)
    {
        var snapshot = await accessControlService.GetSnapshotAsync(userId, ct);
        return (snapshot.Scopes.Any(x => x.ScopeType == ScopeTypes.Global), snapshot.Scopes.Where(x => x.ScopeType == ScopeTypes.Company && x.ScopeId is not null).Select(x => x.ScopeId!.Value).ToHashSet());
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private sealed record AllocationSlice(Guid? ProjectId, Guid? CostCenterId, decimal Percent, string Basis);
}
