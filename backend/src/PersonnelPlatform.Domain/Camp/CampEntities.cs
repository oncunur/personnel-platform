using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Camp;

public static class AccommodationStayStatuses
{
    public const string Active = "ACTIVE";
    public const string Closed = "CLOSED";
    public const string Cancelled = "CANCELLED";

    public static bool IsKnown(string value) => value is Active or Closed or Cancelled;
}

public sealed class CampSite : AuditableEntity
{
    private CampSite() { }

    public Guid CompanyId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Address { get; private set; }
    public bool IsActive { get; private set; }

    public static CampSite Create(Guid companyId, string code, string name, string? address, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Company and actor are required.");
        return new CampSite
        {
            CompanyId = companyId,
            Code = NormalizeRequired(code, 80),
            Name = NormalizeRequired(name, 150),
            Address = Normalize(address, 1000),
            IsActive = true,
            CreatedAt = now.ToUniversalTime(),
            CreatedBy = actorUserId
        };
    }

    private static string NormalizeRequired(string value, int maxLength) => Normalize(value, maxLength) ?? throw new ArgumentException("Value is required.");
    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentException("Text is too long.");
        return normalized;
    }
}

public sealed class CampRoom : AuditableEntity
{
    private CampRoom() { }

    public Guid CampId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public int? Floor { get; private set; }
    public bool IsActive { get; private set; }

    public static CampRoom Create(Guid campId, string code, string name, int? floor, DateTimeOffset now, Guid actorUserId)
    {
        if (campId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Camp and actor are required.");
        return new CampRoom
        {
            CampId = campId,
            Code = NormalizeRequired(code, 80),
            Name = NormalizeRequired(name, 150),
            Floor = floor,
            IsActive = true,
            CreatedAt = now.ToUniversalTime(),
            CreatedBy = actorUserId
        };
    }

    private static string NormalizeRequired(string value, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentException("Text is too long.");
        return normalized;
    }
}

public sealed class CampBed : AuditableEntity
{
    private CampBed() { }

    public Guid RoomId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public static CampBed Create(Guid roomId, string code, DateTimeOffset now, Guid actorUserId)
    {
        if (roomId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Room and actor are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var normalized = code.Trim();
        if (normalized.Length > 80) throw new ArgumentException("Bed code is too long.");
        return new CampBed
        {
            RoomId = roomId,
            Code = normalized,
            IsActive = true,
            CreatedAt = now.ToUniversalTime(),
            CreatedBy = actorUserId
        };
    }
}

public sealed class AccommodationRate : AuditableEntity
{
    private AccommodationRate() { }

    public Guid CampId { get; private set; }
    public DateOnly ValidFrom { get; private set; }
    public DateOnly? ValidUntilExclusive { get; private set; }
    public decimal NightlyRate { get; private set; }
    public string Currency { get; private set; } = "TRY";

    public static AccommodationRate Create(Guid campId, DateOnly validFrom, DateOnly? validUntilExclusive, decimal nightlyRate, string currency, DateTimeOffset now, Guid actorUserId)
    {
        if (campId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Camp and actor are required.");
        if (validUntilExclusive is not null && validUntilExclusive <= validFrom) throw new ArgumentException("Rate end date must be after start date.");
        if (nightlyRate <= 0) throw new ArgumentOutOfRangeException(nameof(nightlyRate));
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        if (normalizedCurrency.Length != 3) throw new ArgumentException("Currency must be a three-letter code.", nameof(currency));
        return new AccommodationRate
        {
            CampId = campId,
            ValidFrom = validFrom,
            ValidUntilExclusive = validUntilExclusive,
            NightlyRate = decimal.Round(nightlyRate, 2, MidpointRounding.AwayFromZero),
            Currency = normalizedCurrency,
            CreatedAt = now.ToUniversalTime(),
            CreatedBy = actorUserId
        };
    }
}

public sealed class AccommodationStay : AuditableEntity
{
    private AccommodationStay() { }

    public Guid CompanyId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid CampId { get; private set; }
    public Guid RoomId { get; private set; }
    public Guid BedId { get; private set; }
    public Guid RateId { get; private set; }
    public Guid? ProjectIdSnapshot { get; private set; }
    public Guid? CostCenterIdSnapshot { get; private set; }
    public DateOnly CheckInDate { get; private set; }
    public DateOnly? CheckOutDateExclusive { get; private set; }
    public decimal NightlyRateSnapshot { get; private set; }
    public string CurrencySnapshot { get; private set; } = "TRY";
    public decimal TotalCostSnapshot { get; private set; }
    public string Status { get; private set; } = AccommodationStayStatuses.Active;
    public string? Note { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public Guid? ClosedBy { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public Guid? CancelledBy { get; private set; }

    public static AccommodationStay Create(
        Guid companyId,
        Guid employeeId,
        Guid campId,
        Guid roomId,
        Guid bedId,
        Guid rateId,
        Guid? projectIdSnapshot,
        Guid? costCenterIdSnapshot,
        DateOnly checkInDate,
        DateOnly? checkOutDateExclusive,
        decimal nightlyRateSnapshot,
        string currencySnapshot,
        string? note,
        DateTimeOffset now,
        Guid actorUserId)
    {
        if (companyId == Guid.Empty || employeeId == Guid.Empty || campId == Guid.Empty || roomId == Guid.Empty || bedId == Guid.Empty || rateId == Guid.Empty || actorUserId == Guid.Empty)
            throw new ArgumentException("Company, employee, camp, room, bed, rate and actor are required.");
        if (checkOutDateExclusive is not null && checkOutDateExclusive <= checkInDate)
            throw new ArgumentException("Check-out date must be after check-in date.");
        if (nightlyRateSnapshot <= 0) throw new ArgumentOutOfRangeException(nameof(nightlyRateSnapshot));
        ArgumentException.ThrowIfNullOrWhiteSpace(currencySnapshot);
        var currency = currencySnapshot.Trim().ToUpperInvariant();
        if (currency.Length != 3) throw new ArgumentException("Currency must be a three-letter code.", nameof(currencySnapshot));

        return new AccommodationStay
        {
            CompanyId = companyId,
            EmployeeId = employeeId,
            CampId = campId,
            RoomId = roomId,
            BedId = bedId,
            RateId = rateId,
            ProjectIdSnapshot = projectIdSnapshot,
            CostCenterIdSnapshot = costCenterIdSnapshot,
            CheckInDate = checkInDate,
            CheckOutDateExclusive = checkOutDateExclusive,
            NightlyRateSnapshot = decimal.Round(nightlyRateSnapshot, 2, MidpointRounding.AwayFromZero),
            CurrencySnapshot = currency,
            TotalCostSnapshot = 0m,
            Status = AccommodationStayStatuses.Active,
            Note = Normalize(note, 2000),
            CreatedAt = now.ToUniversalTime(),
            CreatedBy = actorUserId
        };
    }

    public int NightsAsOf(DateOnly asOfExclusive)
    {
        var end = CheckOutDateExclusive ?? asOfExclusive;
        return Math.Max(0, end.DayNumber - CheckInDate.DayNumber);
    }

    public decimal CostAsOf(DateOnly asOfExclusive) => decimal.Round(NightsAsOf(asOfExclusive) * NightlyRateSnapshot, 2, MidpointRounding.AwayFromZero);

    public void Close(DateOnly checkOutDateExclusive, DateTimeOffset now, Guid actorUserId)
    {
        if (Status != AccommodationStayStatuses.Active) throw new InvalidOperationException("Only active accommodation can be closed.");
        if (checkOutDateExclusive <= CheckInDate) throw new ArgumentException("Check-out date must be after check-in date.", nameof(checkOutDateExclusive));
        CheckOutDateExclusive = checkOutDateExclusive;
        TotalCostSnapshot = CostAsOf(checkOutDateExclusive);
        Status = AccommodationStayStatuses.Closed;
        ClosedAt = now.ToUniversalTime();
        ClosedBy = actorUserId;
        UpdatedAt = now.ToUniversalTime();
        UpdatedBy = actorUserId;
        Version++;
    }

    public void Cancel(DateTimeOffset now, Guid actorUserId)
    {
        if (Status != AccommodationStayStatuses.Active) throw new InvalidOperationException("Only active accommodation can be cancelled.");
        Status = AccommodationStayStatuses.Cancelled;
        TotalCostSnapshot = 0m;
        CancelledAt = now.ToUniversalTime();
        CancelledBy = actorUserId;
        UpdatedAt = now.ToUniversalTime();
        UpdatedBy = actorUserId;
        Version++;
    }

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentException("Text is too long.");
        return normalized;
    }
}
