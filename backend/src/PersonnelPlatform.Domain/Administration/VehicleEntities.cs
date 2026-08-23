using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Administration;

public static class VehicleStatuses
{
    public const string Active = "ACTIVE";
    public const string Maintenance = "MAINTENANCE";
    public const string OutOfService = "OUT_OF_SERVICE";
    public const string Retired = "RETIRED";
    public static bool IsKnown(string value) => value is Active or Maintenance or OutOfService or Retired;
}

public static class VehicleAssignmentStatuses
{
    public const string Active = "ACTIVE";
    public const string Closed = "CLOSED";
}

public static class VehicleEventSources
{
    public const string Manual = "MANUAL";
    public const string Import = "IMPORT";
    public const string Integration = "INTEGRATION";
    public static bool IsKnown(string value) => value is Manual or Import or Integration;
}

public sealed class Vehicle : AuditableEntity
{
    private Vehicle() { }
    public Guid CompanyId { get; private set; }
    public string Plate { get; private set; } = string.Empty;
    public string? Vin { get; private set; }
    public string Brand { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public int? ModelYear { get; private set; }
    public string Status { get; private set; } = VehicleStatuses.Active;
    public DateOnly? InsuranceValidUntil { get; private set; }
    public DateOnly? InspectionValidUntil { get; private set; }
    public string? Note { get; private set; }

    public static Vehicle Create(Guid companyId, string plate, string? vin, string brand, string model, int? modelYear, DateOnly? insuranceValidUntil, DateOnly? inspectionValidUntil, string? note, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Company and actor are required.");
        if (modelYear is not null && (modelYear < 1900 || modelYear > 2200)) throw new ArgumentOutOfRangeException(nameof(modelYear));
        return new Vehicle { CompanyId = companyId, Plate = NormalizePlate(plate), Vin = Optional(vin, 50)?.ToUpperInvariant(), Brand = Required(brand, 100), Model = Required(model, 100), ModelYear = modelYear, Status = VehicleStatuses.Active, InsuranceValidUntil = insuranceValidUntil, InspectionValidUntil = inspectionValidUntil, Note = Optional(note, 1000), CreatedAt = now.ToUniversalTime(), CreatedBy = actorUserId };
    }

    public void SetStatus(string status, DateTimeOffset now, Guid actorUserId)
    {
        var normalized = Required(status, 30).ToUpperInvariant();
        if (!VehicleStatuses.IsKnown(normalized)) throw new ArgumentException("Vehicle status is invalid.", nameof(status));
        Status = normalized; UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
    }

    private static string NormalizePlate(string value) => Required(value, 30).Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
    private static string Required(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
    private static string? Optional(string? value, int max) { if (string.IsNullOrWhiteSpace(value)) return null; var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
}

public sealed class VehicleAssignment : AuditableEntity
{
    private VehicleAssignment() { }
    public Guid CompanyId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid? ProjectIdSnapshot { get; private set; }
    public Guid? CostCenterIdSnapshot { get; private set; }
    public DateOnly ValidFrom { get; private set; }
    public DateOnly? ValidUntilExclusive { get; private set; }
    public string Status { get; private set; } = VehicleAssignmentStatuses.Active;
    public string? Note { get; private set; }

    public static VehicleAssignment Create(Guid companyId, Guid vehicleId, Guid employeeId, Guid? projectIdSnapshot, Guid? costCenterIdSnapshot, DateOnly validFrom, DateOnly? validUntilExclusive, string? note, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || vehicleId == Guid.Empty || employeeId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Company, vehicle, employee and actor are required.");
        if (validUntilExclusive is not null && validUntilExclusive <= validFrom) throw new ArgumentException("Assignment end must be after start.");
        return new VehicleAssignment { CompanyId = companyId, VehicleId = vehicleId, EmployeeId = employeeId, ProjectIdSnapshot = projectIdSnapshot, CostCenterIdSnapshot = costCenterIdSnapshot, ValidFrom = validFrom, ValidUntilExclusive = validUntilExclusive, Status = VehicleAssignmentStatuses.Active, Note = Optional(note, 1000), CreatedAt = now.ToUniversalTime(), CreatedBy = actorUserId };
    }

    public void Close(DateOnly validUntilExclusive, DateTimeOffset now, Guid actorUserId)
    {
        if (Status != VehicleAssignmentStatuses.Active) throw new InvalidOperationException("Only active vehicle assignments can be closed.");
        if (validUntilExclusive <= ValidFrom) throw new ArgumentException("Assignment end must be after start.", nameof(validUntilExclusive));
        ValidUntilExclusive = validUntilExclusive; Status = VehicleAssignmentStatuses.Closed; UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
    }

    private static string? Optional(string? value, int max) { if (string.IsNullOrWhiteSpace(value)) return null; var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
}

public sealed class VehicleOdometerEvent : AuditableEntity
{
    private VehicleOdometerEvent() { }
    public Guid CompanyId { get; private set; }
    public Guid VehicleId { get; private set; }
    public int OdometerKm { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string Source { get; private set; } = VehicleEventSources.Manual;
    public string? ExternalEventId { get; private set; }
    public string? Note { get; private set; }

    public static VehicleOdometerEvent Create(Guid companyId, Guid vehicleId, int odometerKm, DateTimeOffset occurredAt, string source, string? externalEventId, string? note, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || vehicleId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Company, vehicle and actor are required.");
        if (odometerKm < 0 || odometerKm > 10_000_000) throw new ArgumentOutOfRangeException(nameof(odometerKm));
        var normalizedSource = Required(source, 20).ToUpperInvariant();
        if (!VehicleEventSources.IsKnown(normalizedSource)) throw new ArgumentException("Vehicle event source is invalid.", nameof(source));
        var external = Optional(externalEventId, 200);
        if (normalizedSource != VehicleEventSources.Manual && external is null) throw new ArgumentException("External event id is required for import/integration events.", nameof(externalEventId));
        return new VehicleOdometerEvent { CompanyId = companyId, VehicleId = vehicleId, OdometerKm = odometerKm, OccurredAt = occurredAt.ToUniversalTime(), Source = normalizedSource, ExternalEventId = external, Note = Optional(note, 1000), CreatedAt = now.ToUniversalTime(), CreatedBy = actorUserId };
    }

    private static string Required(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
    private static string? Optional(string? value, int max) { if (string.IsNullOrWhiteSpace(value)) return null; var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
}

public sealed class VehicleMaintenanceRecord : AuditableEntity
{
    private VehicleMaintenanceRecord() { }
    public Guid CompanyId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid OdometerEventId { get; private set; }
    public string MaintenanceType { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal Cost { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public DateOnly ServiceDate { get; private set; }
    public DateOnly? NextDueDate { get; private set; }
    public int? NextDueOdometerKm { get; private set; }
    public string? Vendor { get; private set; }

    public static VehicleMaintenanceRecord Create(Guid companyId, Guid vehicleId, Guid odometerEventId, string maintenanceType, string description, decimal cost, string currency, DateOnly serviceDate, DateOnly? nextDueDate, int? nextDueOdometerKm, string? vendor, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || vehicleId == Guid.Empty || odometerEventId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Required identifiers are missing.");
        if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
        if (nextDueDate is not null && nextDueDate < serviceDate) throw new ArgumentException("Next due date cannot be before service date.");
        if (nextDueOdometerKm is < 0) throw new ArgumentOutOfRangeException(nameof(nextDueOdometerKm));
        var curr = Required(currency, 3).ToUpperInvariant(); if (curr.Length != 3) throw new ArgumentException("Currency must be ISO-4217 code.");
        return new VehicleMaintenanceRecord { CompanyId = companyId, VehicleId = vehicleId, OdometerEventId = odometerEventId, MaintenanceType = Required(maintenanceType, 80), Description = Required(description, 1000), Cost = decimal.Round(cost, 2), Currency = curr, ServiceDate = serviceDate, NextDueDate = nextDueDate, NextDueOdometerKm = nextDueOdometerKm, Vendor = Optional(vendor, 200), CreatedAt = now.ToUniversalTime(), CreatedBy = actorUserId };
    }

    private static string Required(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
    private static string? Optional(string? value, int max) { if (string.IsNullOrWhiteSpace(value)) return null; var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
}

public sealed class VehicleFuelRecord : AuditableEntity
{
    private VehicleFuelRecord() { }
    public Guid CompanyId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid OdometerEventId { get; private set; }
    public decimal Liters { get; private set; }
    public decimal TotalCost { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public DateTimeOffset FueledAt { get; private set; }
    public string? Station { get; private set; }
    public string Source { get; private set; } = VehicleEventSources.Manual;
    public string? ExternalEventId { get; private set; }

    public static VehicleFuelRecord Create(Guid companyId, Guid vehicleId, Guid odometerEventId, decimal liters, decimal totalCost, string currency, DateTimeOffset fueledAt, string? station, string source, string? externalEventId, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || vehicleId == Guid.Empty || odometerEventId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Required identifiers are missing.");
        if (liters <= 0 || liters > 10000) throw new ArgumentOutOfRangeException(nameof(liters));
        if (totalCost < 0) throw new ArgumentOutOfRangeException(nameof(totalCost));
        var curr = Required(currency, 3).ToUpperInvariant(); if (curr.Length != 3) throw new ArgumentException("Currency must be ISO-4217 code.");
        var normalizedSource = Required(source, 20).ToUpperInvariant(); if (!VehicleEventSources.IsKnown(normalizedSource)) throw new ArgumentException("Vehicle event source is invalid.");
        var external = Optional(externalEventId, 200); if (normalizedSource != VehicleEventSources.Manual && external is null) throw new ArgumentException("External event id is required for import/integration fuel records.");
        return new VehicleFuelRecord { CompanyId = companyId, VehicleId = vehicleId, OdometerEventId = odometerEventId, Liters = decimal.Round(liters, 3), TotalCost = decimal.Round(totalCost, 2), Currency = curr, FueledAt = fueledAt.ToUniversalTime(), Station = Optional(station, 200), Source = normalizedSource, ExternalEventId = external, CreatedAt = now.ToUniversalTime(), CreatedBy = actorUserId };
    }

    private static string Required(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
    private static string? Optional(string? value, int max) { if (string.IsNullOrWhiteSpace(value)) return null; var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
}
