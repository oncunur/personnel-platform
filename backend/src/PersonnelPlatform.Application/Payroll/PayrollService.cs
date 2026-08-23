using System.Text.Json;
using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Domain.Attendance;
using PersonnelPlatform.Domain.Payroll;

namespace PersonnelPlatform.Application.Payroll;

public sealed class PayrollService(
    IPayrollRepository repository,
    AccessControlService accessControlService,
    TimeProvider timeProvider)
{
    public async Task<PayrollResult<IReadOnlyList<EmployeeCompensationSummary>>> ListCompensationsAsync(Guid userId, Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await repository.FindEmployeeAsync(employeeId, cancellationToken);
        if (employee is null) return PayrollResult<IReadOnlyList<EmployeeCompensationSummary>>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, employee.CompanyId, cancellationToken))
            return PayrollResult<IReadOnlyList<EmployeeCompensationSummary>>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");
        return PayrollResult<IReadOnlyList<EmployeeCompensationSummary>>.Success(await repository.ListCompensationsAsync(employeeId, cancellationToken));
    }

    public async Task<PayrollResult<EmployeeCompensationSummary>> CreateCompensationAsync(Guid userId, CreateEmployeeCompensationRequest request, CancellationToken cancellationToken)
    {
        var employee = await repository.FindEmployeeAsync(request.EmployeeId, cancellationToken);
        if (employee is null) return PayrollResult<EmployeeCompensationSummary>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, employee.CompanyId, cancellationToken))
            return PayrollResult<EmployeeCompensationSummary>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");

        if (await repository.FindOverlappingCompensationAsync(employee.Id, request.ValidFrom, request.ValidUntilExclusive, cancellationToken) is not null)
            return PayrollResult<EmployeeCompensationSummary>.Failure("PAYROLL_COMPENSATION_DATE_CONFLICT", "Personelin bu tarih aralığında başka bir ücret tanımı bulunuyor.");

        try
        {
            var row = EmployeeCompensation.Create(
                employee.CompanyId,
                employee.Id,
                request.ValidFrom,
                request.ValidUntilExclusive,
                request.MonthlyBaseSalary,
                request.Currency,
                request.OvertimeMultiplier,
                timeProvider.GetUtcNow(),
                userId);
            repository.AddCompensation(row);
            await repository.SaveChangesAsync(cancellationToken);
            return PayrollResult<EmployeeCompensationSummary>.Success(new(
                row.Id, row.CompanyId, row.EmployeeId, employee.EmployeeNo,
                $"{employee.FirstName} {employee.LastName}", row.ValidFrom, row.ValidUntilExclusive,
                row.MonthlyBaseSalary, row.Currency, row.OvertimeMultiplier, row.Version));
        }
        catch (ArgumentException)
        {
            return PayrollResult<EmployeeCompensationSummary>.Failure("PAYROLL_COMPENSATION_INVALID", "Ücret tanımı bilgileri geçersiz.");
        }
    }

    public async Task<PayrollResult<IReadOnlyList<PayrollPeriodSummary>>> ListPeriodsAsync(Guid userId, int? year, CancellationToken cancellationToken)
    {
        var access = await ResolveAccessAsync(userId, cancellationToken);
        return PayrollResult<IReadOnlyList<PayrollPeriodSummary>>.Success(
            await repository.ListPeriodsAsync(access.Global, access.CompanyIds, year, cancellationToken));
    }

    public async Task<PayrollResult<PayrollPeriodSummary>> CreatePeriodAsync(Guid userId, CreatePayrollPeriodRequest request, CancellationToken cancellationToken)
    {
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, request.CompanyId, cancellationToken))
            return PayrollResult<PayrollPeriodSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");

        var latest = await repository.FindLatestPeriodAsync(request.CompanyId, request.Year, request.Month, cancellationToken);
        if (latest is not null && latest.Status != PayrollPeriodStatuses.Closed)
            return PayrollResult<PayrollPeriodSummary>.Failure("PAYROLL_PERIOD_ALREADY_ACTIVE", "Bu şirket ve ay için kapanmamış bir bordro dönemi zaten bulunuyor.");

        try
        {
            var row = PayrollPeriod.Create(
                request.CompanyId,
                request.Year,
                request.Month,
                latest is null ? 1 : latest.Revision + 1,
                latest?.Id,
                timeProvider.GetUtcNow(),
                userId);
            repository.AddPeriod(row);
            await repository.SaveChangesAsync(cancellationToken);
            return PayrollResult<PayrollPeriodSummary>.Success(ToSummary(row));
        }
        catch (ArgumentException)
        {
            return PayrollResult<PayrollPeriodSummary>.Failure("PAYROLL_PERIOD_INVALID", "Bordro dönemi bilgileri geçersiz.");
        }
    }

    public Task<PayrollResult<PayrollPeriodSummary>> OpenPeriodAsync(Guid userId, Guid periodId, PayrollPeriodActionRequest request, CancellationToken cancellationToken) =>
        TransitionAsync(userId, periodId, request.Version, "OPEN", static (period, now, actor) => period.Open(now, actor), cancellationToken);

    public async Task<PayrollResult<PayrollPeriodSummary>> CalculateAsync(Guid userId, Guid periodId, PayrollPeriodActionRequest request, CancellationToken cancellationToken)
    {
        var periodResult = await FindAuthorizedPeriodAsync(userId, periodId, cancellationToken);
        if (!periodResult.Succeeded || periodResult.Value is null)
            return PayrollResult<PayrollPeriodSummary>.Failure(periodResult.ErrorCode!, periodResult.ErrorMessage!);
        var period = periodResult.Value;
        if (period.Version != request.Version)
            return PayrollResult<PayrollPeriodSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Bordro dönemi başka bir işlem tarafından değiştirildi. Veriyi yenileyin.");
        if (period.Status != PayrollPeriodStatuses.Open)
            return PayrollResult<PayrollPeriodSummary>.Failure("PAYROLL_PERIOD_STATE_INVALID", "Yalnız OPEN durumundaki bordro dönemi hesaplanabilir.");

        var sources = await repository.BuildCalculationSourcesAsync(period.CompanyId, period.PeriodStart, period.PeriodEndExclusive, cancellationToken);
        if (sources.Count == 0)
            return PayrollResult<PayrollPeriodSummary>.Failure("PAYROLL_NO_ELIGIBLE_EMPLOYEE", "Bu bordro döneminde hesaplanacak personel bulunamadı.");

        foreach (var source in sources)
        {
            if (source.CompensationId is null || source.MonthlyBaseSalary is null || source.Currency is null || source.OvertimeMultiplier is null)
                return PayrollResult<PayrollPeriodSummary>.Failure("PAYROLL_COMPENSATION_MISSING", $"{source.EmployeeNo} - {source.EmployeeName} için dönem başlangıcında geçerli ücret tanımı bulunmuyor.");
            if (source.UnapprovedAttendanceCount > 0)
                return PayrollResult<PayrollPeriodSummary>.Failure("PAYROLL_ATTENDANCE_NOT_APPROVED", $"{source.EmployeeNo} - {source.EmployeeName} için {source.UnapprovedAttendanceCount} adet onaylanmamış puantaj kaydı bulunuyor.");
            if (source.PlannedMinutes <= 0)
                return PayrollResult<PayrollPeriodSummary>.Failure("PAYROLL_PLANNED_MINUTES_MISSING", $"{source.EmployeeNo} - {source.EmployeeName} için hesaplanabilir planlanan çalışma süresi bulunmuyor.");

            var foreignCurrencies = source.MealCosts.Concat(source.AccommodationCosts)
                .Where(x => x.Amount != 0m && !string.Equals(x.Currency, source.Currency, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Currency)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (foreignCurrencies.Length > 0)
                return PayrollResult<PayrollPeriodSummary>.Failure(
                    "PAYROLL_FX_RATE_REQUIRED",
                    $"{source.EmployeeNo} - {source.EmployeeName} için {string.Join(", ", foreignCurrencies)} maliyetleri {source.Currency} bordrosuna çevrilebilmek için FX kuru gerektiriyor.");
        }

        var now = timeProvider.GetUtcNow();
        try
        {
            period.BeginCalculation(now, userId);
            foreach (var source in sources)
            {
                var currency = source.Currency!;
                var mealCost = source.MealCosts.Where(x => x.Currency == currency).Sum(x => x.Amount);
                var accommodationCost = source.AccommodationCosts.Where(x => x.Currency == currency).Sum(x => x.Amount);
                var snapshot = JsonSerializer.Serialize(new
                {
                    calculationVersion = period.CalculationVersion,
                    periodStart = period.PeriodStart,
                    periodEndExclusive = period.PeriodEndExclusive,
                    compensation = new { id = source.CompensationId, source.MonthlyBaseSalary, source.Currency, source.OvertimeMultiplier },
                    attendance = source.DailyAttendanceRefs,
                    approvedOvertime = source.ApprovedOvertimeRefs,
                    mealConsumptions = source.MealConsumptionRefs,
                    accommodationStays = source.AccommodationStayRefs,
                    projectAllocations = source.ProjectAllocations
                });

                repository.AddResult(PayrollEmployeeResult.Create(
                    period.Id,
                    period.CompanyId,
                    source.EmployeeId,
                    source.CompensationId!.Value,
                    source.MonthlyBaseSalary!.Value,
                    currency,
                    source.OvertimeMultiplier!.Value,
                    source.PlannedMinutes,
                    source.WorkedMinutes,
                    source.PaidLeaveMinutes,
                    source.ApprovedOvertimeMinutes,
                    mealCost,
                    accommodationCost,
                    snapshot,
                    now));
            }
            period.CompleteCalculation(now, userId);
            await repository.SaveChangesAsync(cancellationToken);
            return PayrollResult<PayrollPeriodSummary>.Success(ToSummary(period));
        }
        catch (InvalidOperationException)
        {
            return PayrollResult<PayrollPeriodSummary>.Failure("PAYROLL_PERIOD_STATE_INVALID", "Bordro dönemi mevcut durumunda hesaplanamıyor.");
        }
    }

    public Task<PayrollResult<PayrollPeriodSummary>> StartReviewAsync(Guid userId, Guid periodId, PayrollPeriodActionRequest request, CancellationToken cancellationToken) =>
        TransitionAsync(userId, periodId, request.Version, "REVIEW", static (period, now, actor) => period.StartReview(now, actor), cancellationToken);

    public Task<PayrollResult<PayrollPeriodSummary>> ApproveAsync(Guid userId, Guid periodId, PayrollPeriodActionRequest request, CancellationToken cancellationToken) =>
        TransitionAsync(userId, periodId, request.Version, "APPROVE", static (period, now, actor) => period.Approve(now, actor), cancellationToken);

    public Task<PayrollResult<PayrollPeriodSummary>> CloseAsync(Guid userId, Guid periodId, PayrollPeriodActionRequest request, CancellationToken cancellationToken) =>
        TransitionAsync(userId, periodId, request.Version, "CLOSE", static (period, now, actor) => period.Close(now, actor), cancellationToken);

    public async Task<PayrollResult<IReadOnlyList<PayrollEmployeeResultSummary>>> ListResultsAsync(Guid userId, Guid periodId, CancellationToken cancellationToken)
    {
        var periodResult = await FindAuthorizedPeriodAsync(userId, periodId, cancellationToken);
        if (!periodResult.Succeeded || periodResult.Value is null)
            return PayrollResult<IReadOnlyList<PayrollEmployeeResultSummary>>.Failure(periodResult.ErrorCode!, periodResult.ErrorMessage!);
        return PayrollResult<IReadOnlyList<PayrollEmployeeResultSummary>>.Success(await repository.ListResultsAsync(periodId, cancellationToken));
    }

    private async Task<PayrollResult<PayrollPeriodSummary>> TransitionAsync(
        Guid userId,
        Guid periodId,
        int version,
        string action,
        Action<PayrollPeriod, DateTimeOffset, Guid> transition,
        CancellationToken cancellationToken)
    {
        var authorized = await FindAuthorizedPeriodAsync(userId, periodId, cancellationToken);
        if (!authorized.Succeeded || authorized.Value is null)
            return PayrollResult<PayrollPeriodSummary>.Failure(authorized.ErrorCode!, authorized.ErrorMessage!);
        var period = await repository.FindPeriodAsync(periodId, cancellationToken);
        if (period is null) return PayrollResult<PayrollPeriodSummary>.Failure("PAYROLL_PERIOD_NOT_FOUND", "Bordro dönemi bulunamadı.");
        if (period.Version != version)
            return PayrollResult<PayrollPeriodSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Bordro dönemi başka bir işlem tarafından değiştirildi. Veriyi yenileyin.");
        try
        {
            transition(period, timeProvider.GetUtcNow(), userId);
            await repository.SaveChangesAsync(cancellationToken);
            return PayrollResult<PayrollPeriodSummary>.Success(ToSummary(period));
        }
        catch (InvalidOperationException)
        {
            return PayrollResult<PayrollPeriodSummary>.Failure("PAYROLL_PERIOD_STATE_INVALID", $"Bordro dönemi {action} işlemi için uygun durumda değil.");
        }
    }

    private async Task<PayrollResult<PayrollPeriod>> FindAuthorizedPeriodAsync(Guid userId, Guid periodId, CancellationToken cancellationToken)
    {
        var period = await repository.FindPeriodAsync(periodId, cancellationToken);
        if (period is null) return PayrollResult<PayrollPeriod>.Failure("PAYROLL_PERIOD_NOT_FOUND", "Bordro dönemi bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, period.CompanyId, cancellationToken))
            return PayrollResult<PayrollPeriod>.Failure("SCOPE_DENIED", "Bordro döneminin şirket kapsamına erişiminiz yok.");
        return PayrollResult<PayrollPeriod>.Success(period);
    }

    private async Task<(bool Global, Guid[] CompanyIds)> ResolveAccessAsync(Guid userId, CancellationToken cancellationToken)
    {
        var snapshot = await accessControlService.GetSnapshotAsync(userId, cancellationToken);
        return (
            snapshot.Scopes.Any(x => x.ScopeType == ScopeTypes.Global),
            snapshot.Scopes.Where(x => x.ScopeType == ScopeTypes.Company && x.ScopeId is not null).Select(x => x.ScopeId!.Value).Distinct().ToArray());
    }

    private static PayrollPeriodSummary ToSummary(PayrollPeriod period) => new(
        period.Id, period.CompanyId, period.Year, period.Month, period.Revision, period.PreviousRevisionId,
        period.Status, period.CalculationVersion, period.CalculatedAt, period.ApprovedAt, period.ClosedAt, period.Version);
}
