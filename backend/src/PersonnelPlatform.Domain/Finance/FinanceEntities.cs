using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Finance;

public static class CostSourceTypes
{
    public const string Payroll = "PAYROLL";
    public const string Meal = "MEAL";
    public const string Accommodation = "ACCOMMODATION";
    public static bool IsKnown(string value) => value is Payroll or Meal or Accommodation;
}

public static class CostCategories
{
    public const string Payroll = "PAYROLL";
    public const string Meal = "MEAL";
    public const string Accommodation = "ACCOMMODATION";
}

public static class CostAllocationBases
{
    public const string Direct = "DIRECT";
    public const string Attendance = "ATTENDANCE";
    public const string Fixed = "FIXED";
    public const string Manual = "MANUAL";
    public const string Unallocated = "UNALLOCATED";
    public static bool IsKnown(string value) => value is Direct or Attendance or Fixed or Manual or Unallocated;
}

public sealed class CostEntry : Entity
{
    private CostEntry() { }

    public Guid CompanyId { get; private set; }
    public string SourceType { get; private set; } = string.Empty;
    public Guid SourceId { get; private set; }
    public string SourceLineKey { get; private set; } = string.Empty;
    public Guid? EmployeeId { get; private set; }
    public Guid? ProjectId { get; private set; }
    public Guid? CostCenterId { get; private set; }
    public DateOnly CostDate { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public string Unit { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "TRY";
    public string AllocationBasis { get; private set; } = CostAllocationBases.Direct;
    public string MetadataJson { get; private set; } = "{}";
    public DateTimeOffset CreatedAt { get; private set; }

    public static CostEntry Create(
        Guid companyId,
        string sourceType,
        Guid sourceId,
        string sourceLineKey,
        Guid? employeeId,
        Guid? projectId,
        Guid? costCenterId,
        DateOnly costDate,
        string category,
        decimal quantity,
        string unit,
        decimal amount,
        string currency,
        string allocationBasis,
        string metadataJson,
        DateTimeOffset createdAt)
    {
        if (companyId == Guid.Empty || sourceId == Guid.Empty) throw new ArgumentException("Company and source are required.");
        var normalizedSource = Required(sourceType, 40).ToUpperInvariant();
        if (!CostSourceTypes.IsKnown(normalizedSource)) throw new ArgumentException("Cost source type is invalid.", nameof(sourceType));
        var normalizedBasis = Required(allocationBasis, 30).ToUpperInvariant();
        if (!CostAllocationBases.IsKnown(normalizedBasis)) throw new ArgumentException("Allocation basis is invalid.", nameof(allocationBasis));
        var normalizedCurrency = Required(currency, 3).ToUpperInvariant();
        if (normalizedCurrency.Length != 3) throw new ArgumentException("Currency must be a three-letter code.", nameof(currency));
        if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));

        return new CostEntry
        {
            CompanyId = companyId,
            SourceType = normalizedSource,
            SourceId = sourceId,
            SourceLineKey = Required(sourceLineKey, 220),
            EmployeeId = employeeId,
            ProjectId = projectId,
            CostCenterId = costCenterId,
            CostDate = costDate,
            Category = Required(category, 50).ToUpperInvariant(),
            Quantity = decimal.Round(quantity, 4, MidpointRounding.AwayFromZero),
            Unit = Required(unit, 30).ToUpperInvariant(),
            Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero),
            Currency = normalizedCurrency,
            AllocationBasis = normalizedBasis,
            MetadataJson = Required(metadataJson, 100_000),
            CreatedAt = createdAt.ToUniversalTime()
        };
    }

    private static string Required(string value, int max)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > max) throw new ArgumentException("Text is too long.");
        return normalized;
    }
}

public sealed class PayrollCostAllocationOverride : AuditableEntity
{
    private PayrollCostAllocationOverride() { }

    public Guid PayrollPeriodId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid? CostCenterId { get; private set; }
    public decimal AllocationPercent { get; private set; }

    public static PayrollCostAllocationOverride Create(
        Guid payrollPeriodId,
        Guid companyId,
        Guid employeeId,
        Guid projectId,
        Guid? costCenterId,
        decimal allocationPercent,
        DateTimeOffset now,
        Guid actorUserId)
    {
        if (payrollPeriodId == Guid.Empty || companyId == Guid.Empty || employeeId == Guid.Empty || projectId == Guid.Empty || actorUserId == Guid.Empty)
            throw new ArgumentException("Payroll period, company, employee, project and actor are required.");
        if (allocationPercent <= 0 || allocationPercent > 100) throw new ArgumentOutOfRangeException(nameof(allocationPercent));
        return new PayrollCostAllocationOverride
        {
            PayrollPeriodId = payrollPeriodId,
            CompanyId = companyId,
            EmployeeId = employeeId,
            ProjectId = projectId,
            CostCenterId = costCenterId,
            AllocationPercent = decimal.Round(allocationPercent, 4, MidpointRounding.AwayFromZero),
            CreatedAt = now.ToUniversalTime(),
            CreatedBy = actorUserId
        };
    }
}
