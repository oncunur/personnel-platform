using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Administration;

public static class AssetStatuses
{
    public const string Available = "AVAILABLE";
    public const string Assigned = "ASSIGNED";
    public const string Maintenance = "MAINTENANCE";
    public const string Lost = "LOST";
    public const string Retired = "RETIRED";
    public static bool IsKnown(string value) => value is Available or Assigned or Maintenance or Lost or Retired;
}

public static class AssetAssignmentStatuses
{
    public const string Active = "ACTIVE";
    public const string Returned = "RETURNED";
    public const string Damaged = "DAMAGED";
    public const string Lost = "LOST";
    public static bool IsKnown(string value) => value is Active or Returned or Damaged or Lost;
}

public static class StockMovementTypes
{
    public const string Receipt = "RECEIPT";
    public const string Issue = "ISSUE";
    public const string Return = "RETURN";
    public const string CorrectionIn = "CORRECTION_IN";
    public const string CorrectionOut = "CORRECTION_OUT";
    public static bool IsKnown(string value) => value is Receipt or Issue or Return or CorrectionIn or CorrectionOut;
    public static bool IsInbound(string value) => value is Receipt or Return or CorrectionIn;
}

public static class StockMovementSources
{
    public const string Manual = "MANUAL";
    public const string Import = "IMPORT";
    public const string Integration = "INTEGRATION";
    public static bool IsKnown(string value) => value is Manual or Import or Integration;
}

public sealed class StockLocation : AuditableEntity
{
    private StockLocation() { }
    public Guid CompanyId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public static StockLocation Create(Guid companyId, string code, string name, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Company and actor are required.");
        return new StockLocation { CompanyId = companyId, Code = Required(code, 80), Name = Required(name, 150), IsActive = true, CreatedAt = now.ToUniversalTime(), CreatedBy = actorUserId };
    }

    private static string Required(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
}

public sealed class StockItem : AuditableEntity
{
    private StockItem() { }
    public Guid CompanyId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Unit { get; private set; } = "EA";
    public decimal MinimumLevel { get; private set; }
    public bool IsActive { get; private set; }

    public static StockItem Create(Guid companyId, string code, string name, string unit, decimal minimumLevel, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Company and actor are required.");
        if (minimumLevel < 0) throw new ArgumentOutOfRangeException(nameof(minimumLevel));
        return new StockItem { CompanyId = companyId, Code = Required(code, 80), Name = Required(name, 150), Unit = Required(unit, 20).ToUpperInvariant(), MinimumLevel = decimal.Round(minimumLevel, 3), IsActive = true, CreatedAt = now.ToUniversalTime(), CreatedBy = actorUserId };
    }

    private static string Required(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
}

public sealed class StockMovement : AuditableEntity
{
    private StockMovement() { }
    public Guid CompanyId { get; private set; }
    public Guid StockItemId { get; private set; }
    public Guid LocationId { get; private set; }
    public Guid? EmployeeId { get; private set; }
    public Guid? ProjectIdSnapshot { get; private set; }
    public Guid? CostCenterIdSnapshot { get; private set; }
    public string MovementType { get; private set; } = StockMovementTypes.Receipt;
    public decimal Quantity { get; private set; }
    public string Source { get; private set; } = StockMovementSources.Manual;
    public string? ExternalEventId { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public decimal SignedQuantity => StockMovementTypes.IsInbound(MovementType) ? Quantity : -Quantity;

    public static StockMovement Create(Guid companyId, Guid stockItemId, Guid locationId, Guid? employeeId, Guid? projectIdSnapshot, Guid? costCenterIdSnapshot, string movementType, decimal quantity, string source, string? externalEventId, string? note, DateTimeOffset occurredAt, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || stockItemId == Guid.Empty || locationId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Company, item, location and actor are required.");
        if (quantity <= 0 || quantity > 1_000_000_000m) throw new ArgumentOutOfRangeException(nameof(quantity));
        var type = Required(movementType, 30).ToUpperInvariant();
        var movementSource = Required(source, 20).ToUpperInvariant();
        if (!StockMovementTypes.IsKnown(type)) throw new ArgumentException("Stock movement type is invalid.", nameof(movementType));
        if (!StockMovementSources.IsKnown(movementSource)) throw new ArgumentException("Stock movement source is invalid.", nameof(source));
        var external = Optional(externalEventId, 200);
        if (movementSource != StockMovementSources.Manual && external is null) throw new ArgumentException("External event id is required for import/integration movements.", nameof(externalEventId));
        return new StockMovement { CompanyId = companyId, StockItemId = stockItemId, LocationId = locationId, EmployeeId = employeeId, ProjectIdSnapshot = projectIdSnapshot, CostCenterIdSnapshot = costCenterIdSnapshot, MovementType = type, Quantity = decimal.Round(quantity, 3), Source = movementSource, ExternalEventId = external, Note = Optional(note, 1000), OccurredAt = occurredAt.ToUniversalTime(), CreatedAt = now.ToUniversalTime(), CreatedBy = actorUserId };
    }

    private static string Required(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
    private static string? Optional(string? value, int max) { if (string.IsNullOrWhiteSpace(value)) return null; var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
}

public sealed class AssetItem : AuditableEntity
{
    private AssetItem() { }
    public Guid CompanyId { get; private set; }
    public Guid? LocationId { get; private set; }
    public string AssetTag { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string? SerialNumber { get; private set; }
    public string Status { get; private set; } = AssetStatuses.Available;
    public DateOnly? PurchaseDate { get; private set; }
    public decimal? PurchaseCost { get; private set; }
    public string? Currency { get; private set; }
    public string? Note { get; private set; }

    public static AssetItem Create(Guid companyId, Guid? locationId, string assetTag, string name, string category, string? serialNumber, DateOnly? purchaseDate, decimal? purchaseCost, string? currency, string? note, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Company and actor are required.");
        if (purchaseCost is < 0) throw new ArgumentOutOfRangeException(nameof(purchaseCost));
        var normalizedCurrency = Optional(currency, 3)?.ToUpperInvariant();
        if (purchaseCost is > 0 && normalizedCurrency?.Length != 3) throw new ArgumentException("Currency is required for purchase cost.", nameof(currency));
        return new AssetItem { CompanyId = companyId, LocationId = locationId, AssetTag = Required(assetTag, 80), Name = Required(name, 150), Category = Required(category, 100), SerialNumber = Optional(serialNumber, 150), Status = AssetStatuses.Available, PurchaseDate = purchaseDate, PurchaseCost = purchaseCost is null ? null : decimal.Round(purchaseCost.Value, 2), Currency = normalizedCurrency, Note = Optional(note, 1000), CreatedAt = now.ToUniversalTime(), CreatedBy = actorUserId };
    }

    public void Assign(DateTimeOffset now, Guid actorUserId)
    {
        if (Status != AssetStatuses.Available) throw new InvalidOperationException("Only available assets can be assigned.");
        Status = AssetStatuses.Assigned; UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
    }

    public void Return(bool damaged, Guid? locationId, DateTimeOffset now, Guid actorUserId)
    {
        if (Status != AssetStatuses.Assigned) throw new InvalidOperationException("Only assigned assets can be returned.");
        Status = damaged ? AssetStatuses.Maintenance : AssetStatuses.Available; LocationId = locationId; UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
    }

    public void MarkLost(DateTimeOffset now, Guid actorUserId)
    {
        if (Status != AssetStatuses.Assigned) throw new InvalidOperationException("Only assigned assets can be marked lost.");
        Status = AssetStatuses.Lost; UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
    }

    private static string Required(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
    private static string? Optional(string? value, int max) { if (string.IsNullOrWhiteSpace(value)) return null; var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
}

public sealed class AssetAssignment : AuditableEntity
{
    private AssetAssignment() { }
    public Guid CompanyId { get; private set; }
    public Guid AssetId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid? ProjectIdSnapshot { get; private set; }
    public Guid? CostCenterIdSnapshot { get; private set; }
    public DateOnly AssignedDate { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public DateOnly? ReturnedDate { get; private set; }
    public string Status { get; private set; } = AssetAssignmentStatuses.Active;
    public string? Note { get; private set; }

    public static AssetAssignment Create(Guid companyId, Guid assetId, Guid employeeId, Guid? projectIdSnapshot, Guid? costCenterIdSnapshot, DateOnly assignedDate, DateOnly? dueDate, string? note, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || assetId == Guid.Empty || employeeId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Company, asset, employee and actor are required.");
        if (dueDate is not null && dueDate < assignedDate) throw new ArgumentException("Due date cannot be before assignment date.");
        return new AssetAssignment { CompanyId = companyId, AssetId = assetId, EmployeeId = employeeId, ProjectIdSnapshot = projectIdSnapshot, CostCenterIdSnapshot = costCenterIdSnapshot, AssignedDate = assignedDate, DueDate = dueDate, Status = AssetAssignmentStatuses.Active, Note = Optional(note, 1000), CreatedAt = now.ToUniversalTime(), CreatedBy = actorUserId };
    }

    public void Return(DateOnly returnedDate, bool damaged, DateTimeOffset now, Guid actorUserId)
    {
        if (Status != AssetAssignmentStatuses.Active) throw new InvalidOperationException("Only active assignments can be returned.");
        if (returnedDate < AssignedDate) throw new ArgumentException("Return date cannot be before assignment date.", nameof(returnedDate));
        ReturnedDate = returnedDate; Status = damaged ? AssetAssignmentStatuses.Damaged : AssetAssignmentStatuses.Returned; UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
    }

    public void MarkLost(DateOnly date, DateTimeOffset now, Guid actorUserId)
    {
        if (Status != AssetAssignmentStatuses.Active) throw new InvalidOperationException("Only active assignments can be marked lost.");
        if (date < AssignedDate) throw new ArgumentException("Lost date cannot be before assignment date.", nameof(date));
        ReturnedDate = date; Status = AssetAssignmentStatuses.Lost; UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
    }

    private static string? Optional(string? value, int max) { if (string.IsNullOrWhiteSpace(value)) return null; var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
}
