using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Meal;

public static class MealConsumptionSources
{
    public const string Manual = "MANUAL";
    public const string Import = "IMPORT";
    public const string Integration = "INTEGRATION";
    public static bool IsKnown(string value) => value is Manual or Import or Integration;
}

public sealed class MealType : Entity
{
    private MealType() { }
    private MealType(Guid id, string code, string name, int displayOrder)
    {
        Id = id;
        Code = code;
        Name = name;
        DisplayOrder = displayOrder;
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public int DisplayOrder { get; private set; }

    public static MealType Seed(Guid id, string code, string name, int displayOrder) => new(id, code, name, displayOrder);
}

public sealed class MealRate : AuditableEntity
{
    private MealRate() { }

    public Guid CampId { get; private set; }
    public Guid MealTypeId { get; private set; }
    public DateOnly ValidFrom { get; private set; }
    public DateOnly? ValidUntilExclusive { get; private set; }
    public decimal UnitPrice { get; private set; }
    public string Currency { get; private set; } = "TRY";

    public static MealRate Create(Guid campId, Guid mealTypeId, DateOnly validFrom, DateOnly? validUntilExclusive, decimal unitPrice, string currency, DateTimeOffset now, Guid actorUserId)
    {
        if (campId == Guid.Empty || mealTypeId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Camp, meal type and actor are required.");
        if (validUntilExclusive is not null && validUntilExclusive <= validFrom) throw new ArgumentException("Rate end date must be after start date.");
        if (unitPrice <= 0) throw new ArgumentOutOfRangeException(nameof(unitPrice));
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        if (normalizedCurrency.Length != 3) throw new ArgumentException("Currency must be a three-letter code.", nameof(currency));
        return new MealRate
        {
            CampId = campId,
            MealTypeId = mealTypeId,
            ValidFrom = validFrom,
            ValidUntilExclusive = validUntilExclusive,
            UnitPrice = decimal.Round(unitPrice, 2, MidpointRounding.AwayFromZero),
            Currency = normalizedCurrency,
            CreatedAt = now.ToUniversalTime(),
            CreatedBy = actorUserId
        };
    }
}

public sealed class MealConsumption : AuditableEntity
{
    private MealConsumption() { }

    public Guid CompanyId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid CampId { get; private set; }
    public Guid MealTypeId { get; private set; }
    public Guid MealRateId { get; private set; }
    public DateOnly ConsumptionDate { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitPriceSnapshot { get; private set; }
    public string CurrencySnapshot { get; private set; } = "TRY";
    public decimal TotalCostSnapshot { get; private set; }
    public Guid? ProjectIdSnapshot { get; private set; }
    public Guid? CostCenterIdSnapshot { get; private set; }
    public string Source { get; private set; } = MealConsumptionSources.Manual;
    public string? ExternalEventId { get; private set; }
    public string? Note { get; private set; }

    public static MealConsumption Create(
        Guid companyId,
        Guid employeeId,
        Guid campId,
        Guid mealTypeId,
        Guid mealRateId,
        DateOnly consumptionDate,
        decimal quantity,
        decimal unitPriceSnapshot,
        string currencySnapshot,
        Guid? projectIdSnapshot,
        Guid? costCenterIdSnapshot,
        string source,
        string? externalEventId,
        string? note,
        DateTimeOffset now,
        Guid actorUserId)
    {
        if (companyId == Guid.Empty || employeeId == Guid.Empty || campId == Guid.Empty || mealTypeId == Guid.Empty || mealRateId == Guid.Empty || actorUserId == Guid.Empty)
            throw new ArgumentException("Company, employee, camp, meal type, rate and actor are required.");
        if (quantity <= 0 || quantity > 10) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (unitPriceSnapshot <= 0) throw new ArgumentOutOfRangeException(nameof(unitPriceSnapshot));
        ArgumentException.ThrowIfNullOrWhiteSpace(currencySnapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        var currency = currencySnapshot.Trim().ToUpperInvariant();
        if (currency.Length != 3) throw new ArgumentException("Currency must be a three-letter code.", nameof(currencySnapshot));
        var normalizedSource = source.Trim().ToUpperInvariant();
        if (!MealConsumptionSources.IsKnown(normalizedSource)) throw new ArgumentException("Meal consumption source is invalid.", nameof(source));
        var externalId = Normalize(externalEventId, 200);
        if (normalizedSource != MealConsumptionSources.Manual && externalId is null)
            throw new ArgumentException("External event id is required for import and integration sources.", nameof(externalEventId));
        var roundedQuantity = decimal.Round(quantity, 2, MidpointRounding.AwayFromZero);
        var roundedPrice = decimal.Round(unitPriceSnapshot, 2, MidpointRounding.AwayFromZero);
        return new MealConsumption
        {
            CompanyId = companyId,
            EmployeeId = employeeId,
            CampId = campId,
            MealTypeId = mealTypeId,
            MealRateId = mealRateId,
            ConsumptionDate = consumptionDate,
            Quantity = roundedQuantity,
            UnitPriceSnapshot = roundedPrice,
            CurrencySnapshot = currency,
            TotalCostSnapshot = decimal.Round(roundedQuantity * roundedPrice, 2, MidpointRounding.AwayFromZero),
            ProjectIdSnapshot = projectIdSnapshot,
            CostCenterIdSnapshot = costCenterIdSnapshot,
            Source = normalizedSource,
            ExternalEventId = externalId,
            Note = Normalize(note, 1000),
            CreatedAt = now.ToUniversalTime(),
            CreatedBy = actorUserId
        };
    }

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentException("Text is too long.");
        return normalized;
    }
}
