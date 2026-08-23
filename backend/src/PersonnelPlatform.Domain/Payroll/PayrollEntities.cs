using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Payroll;

public static class PayrollPeriodStatuses
{
    public const string Draft = "DRAFT";
    public const string Open = "OPEN";
    public const string Calculating = "CALCULATING";
    public const string Calculated = "CALCULATED";
    public const string UnderReview = "UNDER_REVIEW";
    public const string Approved = "APPROVED";
    public const string Closed = "CLOSED";

    public static bool IsKnown(string value) => value is Draft or Open or Calculating or Calculated or UnderReview or Approved or Closed;
}

public sealed class EmployeeCompensation : AuditableEntity
{
    private EmployeeCompensation() { }

    public Guid CompanyId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateOnly ValidFrom { get; private set; }
    public DateOnly? ValidUntilExclusive { get; private set; }
    public decimal MonthlyBaseSalary { get; private set; }
    public string Currency { get; private set; } = "TRY";
    public decimal OvertimeMultiplier { get; private set; }

    public static EmployeeCompensation Create(Guid companyId, Guid employeeId, DateOnly validFrom, DateOnly? validUntilExclusive, decimal monthlyBaseSalary, string currency, decimal overtimeMultiplier, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || employeeId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Company, employee and actor are required.");
        if (validFrom.Day != 1 || (validUntilExclusive is not null && validUntilExclusive.Value.Day != 1)) throw new ArgumentException("Compensation periods must start/end on month boundaries.");
        if (validUntilExclusive is not null && validUntilExclusive <= validFrom) throw new ArgumentException("Compensation end date must be after start date.");
        if (monthlyBaseSalary <= 0) throw new ArgumentOutOfRangeException(nameof(monthlyBaseSalary));
        if (overtimeMultiplier < 1m || overtimeMultiplier > 5m) throw new ArgumentOutOfRangeException(nameof(overtimeMultiplier));
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        if (normalizedCurrency.Length != 3) throw new ArgumentException("Currency must be a three-letter code.", nameof(currency));
        return new EmployeeCompensation
        {
            CompanyId = companyId,
            EmployeeId = employeeId,
            ValidFrom = validFrom,
            ValidUntilExclusive = validUntilExclusive,
            MonthlyBaseSalary = decimal.Round(monthlyBaseSalary, 2, MidpointRounding.AwayFromZero),
            Currency = normalizedCurrency,
            OvertimeMultiplier = decimal.Round(overtimeMultiplier, 4, MidpointRounding.AwayFromZero),
            CreatedAt = now.ToUniversalTime(),
            CreatedBy = actorUserId
        };
    }
}

public sealed class PayrollPeriod : AuditableEntity
{
    private PayrollPeriod() { }

    public Guid CompanyId { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public int Revision { get; private set; }
    public Guid? PreviousRevisionId { get; private set; }
    public string Status { get; private set; } = PayrollPeriodStatuses.Draft;
    public string CalculationVersion { get; private set; } = "PAYROLL_BASE_V1";
    public DateTimeOffset? CalculatedAt { get; private set; }
    public Guid? CalculatedBy { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public Guid? ClosedBy { get; private set; }

    public DateOnly PeriodStart => new(Year, Month, 1);
    public DateOnly PeriodEndExclusive => PeriodStart.AddMonths(1);

    public static PayrollPeriod Create(Guid companyId, int year, int month, int revision, Guid? previousRevisionId, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Company and actor are required.");
        if (year < 2000 || year > 2200) throw new ArgumentOutOfRangeException(nameof(year));
        if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
        if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));
        if (revision == 1 && previousRevisionId is not null) throw new ArgumentException("First revision cannot reference a previous revision.");
        if (revision > 1 && previousRevisionId is null) throw new ArgumentException("A revision must reference its previous period.");
        return new PayrollPeriod
        {
            CompanyId = companyId,
            Year = year,
            Month = month,
            Revision = revision,
            PreviousRevisionId = previousRevisionId,
            Status = PayrollPeriodStatuses.Draft,
            CreatedAt = now.ToUniversalTime(),
            CreatedBy = actorUserId
        };
    }

    public void Open(DateTimeOffset now, Guid actorUserId) => Transition(PayrollPeriodStatuses.Draft, PayrollPeriodStatuses.Open, now, actorUserId);

    public void BeginCalculation(DateTimeOffset now, Guid actorUserId) => Transition(PayrollPeriodStatuses.Open, PayrollPeriodStatuses.Calculating, now, actorUserId);

    public void CompleteCalculation(DateTimeOffset now, Guid actorUserId)
    {
        Transition(PayrollPeriodStatuses.Calculating, PayrollPeriodStatuses.Calculated, now, actorUserId);
        CalculatedAt = now.ToUniversalTime();
        CalculatedBy = actorUserId;
    }

    public void StartReview(DateTimeOffset now, Guid actorUserId) => Transition(PayrollPeriodStatuses.Calculated, PayrollPeriodStatuses.UnderReview, now, actorUserId);

    public void Approve(DateTimeOffset now, Guid actorUserId)
    {
        Transition(PayrollPeriodStatuses.UnderReview, PayrollPeriodStatuses.Approved, now, actorUserId);
        ApprovedAt = now.ToUniversalTime();
        ApprovedBy = actorUserId;
    }

    public void Close(DateTimeOffset now, Guid actorUserId)
    {
        Transition(PayrollPeriodStatuses.Approved, PayrollPeriodStatuses.Closed, now, actorUserId);
        ClosedAt = now.ToUniversalTime();
        ClosedBy = actorUserId;
    }

    private void Transition(string expected, string next, DateTimeOffset now, Guid actorUserId)
    {
        if (Status != expected) throw new InvalidOperationException($"Payroll period must be {expected} before transition to {next}.");
        if (actorUserId == Guid.Empty) throw new ArgumentException("Actor is required.", nameof(actorUserId));
        Status = next;
        UpdatedAt = now.ToUniversalTime();
        UpdatedBy = actorUserId;
        Version++;
    }
}

public sealed class PayrollEmployeeResult : Entity
{
    private PayrollEmployeeResult() { }

    public Guid PayrollPeriodId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid CompensationId { get; private set; }
    public decimal MonthlyBaseSalarySnapshot { get; private set; }
    public string CurrencySnapshot { get; private set; } = "TRY";
    public decimal OvertimeMultiplierSnapshot { get; private set; }
    public int PlannedMinutes { get; private set; }
    public int WorkedMinutes { get; private set; }
    public int PaidLeaveMinutes { get; private set; }
    public int ApprovedOvertimeMinutes { get; private set; }
    public decimal BaseSalaryAmount { get; private set; }
    public decimal AbsenceDeductionAmount { get; private set; }
    public decimal OvertimeEarningAmount { get; private set; }
    public decimal PayBeforeStatutory { get; private set; }
    public decimal MealEmployerCost { get; private set; }
    public decimal AccommodationEmployerCost { get; private set; }
    public decimal EmployerCostBeforeStatutory { get; private set; }
    public string SourceSnapshotJson { get; private set; } = "{}";
    public DateTimeOffset CalculatedAt { get; private set; }

    public static PayrollEmployeeResult Create(
        Guid payrollPeriodId,
        Guid companyId,
        Guid employeeId,
        Guid compensationId,
        decimal monthlyBaseSalarySnapshot,
        string currencySnapshot,
        decimal overtimeMultiplierSnapshot,
        int plannedMinutes,
        int workedMinutes,
        int paidLeaveMinutes,
        int approvedOvertimeMinutes,
        decimal mealEmployerCost,
        decimal accommodationEmployerCost,
        string sourceSnapshotJson,
        DateTimeOffset calculatedAt)
    {
        if (payrollPeriodId == Guid.Empty || companyId == Guid.Empty || employeeId == Guid.Empty || compensationId == Guid.Empty) throw new ArgumentException("Payroll period, company, employee and compensation are required.");
        if (monthlyBaseSalarySnapshot <= 0 || overtimeMultiplierSnapshot < 1) throw new ArgumentOutOfRangeException(nameof(monthlyBaseSalarySnapshot));
        if (plannedMinutes <= 0 || workedMinutes < 0 || paidLeaveMinutes < 0 || approvedOvertimeMinutes < 0) throw new ArgumentOutOfRangeException(nameof(plannedMinutes));
        ArgumentException.ThrowIfNullOrWhiteSpace(currencySnapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSnapshotJson);

        var payableMinutes = Math.Min(plannedMinutes, workedMinutes + paidLeaveMinutes);
        var missingMinutes = Math.Max(0, plannedMinutes - payableMinutes);
        var minuteRate = monthlyBaseSalarySnapshot / plannedMinutes;
        var absenceDeduction = decimal.Round(minuteRate * missingMinutes, 2, MidpointRounding.AwayFromZero);
        var overtimeEarning = decimal.Round(minuteRate * approvedOvertimeMinutes * overtimeMultiplierSnapshot, 2, MidpointRounding.AwayFromZero);
        var baseSalary = decimal.Round(monthlyBaseSalarySnapshot, 2, MidpointRounding.AwayFromZero);
        var payBeforeStatutory = decimal.Round(baseSalary - absenceDeduction + overtimeEarning, 2, MidpointRounding.AwayFromZero);
        var meal = decimal.Round(mealEmployerCost, 2, MidpointRounding.AwayFromZero);
        var accommodation = decimal.Round(accommodationEmployerCost, 2, MidpointRounding.AwayFromZero);

        return new PayrollEmployeeResult
        {
            PayrollPeriodId = payrollPeriodId,
            CompanyId = companyId,
            EmployeeId = employeeId,
            CompensationId = compensationId,
            MonthlyBaseSalarySnapshot = baseSalary,
            CurrencySnapshot = currencySnapshot.Trim().ToUpperInvariant(),
            OvertimeMultiplierSnapshot = overtimeMultiplierSnapshot,
            PlannedMinutes = plannedMinutes,
            WorkedMinutes = workedMinutes,
            PaidLeaveMinutes = paidLeaveMinutes,
            ApprovedOvertimeMinutes = approvedOvertimeMinutes,
            BaseSalaryAmount = baseSalary,
            AbsenceDeductionAmount = absenceDeduction,
            OvertimeEarningAmount = overtimeEarning,
            PayBeforeStatutory = payBeforeStatutory,
            MealEmployerCost = meal,
            AccommodationEmployerCost = accommodation,
            EmployerCostBeforeStatutory = decimal.Round(payBeforeStatutory + meal + accommodation, 2, MidpointRounding.AwayFromZero),
            SourceSnapshotJson = sourceSnapshotJson,
            CalculatedAt = calculatedAt.ToUniversalTime()
        };
    }
}
