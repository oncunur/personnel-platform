namespace PersonnelPlatform.Application.Administration;

public static class VehiclePermissions
{
    public const string View = "administration.vehicle.view";
    public const string Manage = "administration.vehicle.manage";
    public const string Assign = "administration.vehicle.assign";
    public const string OdometerRecord = "administration.vehicle.odometer.record";
    public const string MaintenanceManage = "administration.vehicle.maintenance.manage";
    public const string FuelRecord = "administration.vehicle.fuel.record";
}

public sealed record CreateVehicleRequest(Guid CompanyId, string Plate, string? Vin, string Brand, string Model, int? ModelYear, DateOnly? InsuranceValidUntil, DateOnly? InspectionValidUntil, string? Note);
public sealed record SetVehicleStatusRequest(int Version, string Status);
public sealed record UpdateVehicleComplianceRequest(int Version, DateOnly? InsuranceValidUntil, DateOnly? InspectionValidUntil);
public sealed record VehicleComplianceSummary(Guid Id, DateOnly? InsuranceValidUntil, DateOnly? InspectionValidUntil, int Version);
public sealed record VehicleSummary(Guid Id, Guid CompanyId, string Plate, string? Vin, string Brand, string Model, int? ModelYear, string Status, DateOnly? InsuranceValidUntil, DateOnly? InspectionValidUntil, int? CurrentOdometerKm, Guid? AssignedEmployeeId, string? AssignedEmployeeNo, string? AssignedEmployeeName, int Version);

public sealed record AssignVehicleRequest(Guid VehicleId, Guid EmployeeId, DateOnly ValidFrom, DateOnly? ValidUntilExclusive, string? Note);
public sealed record CloseVehicleAssignmentRequest(int Version, DateOnly ValidUntilExclusive);
public sealed record VehicleAssignmentSummary(Guid Id, Guid CompanyId, Guid VehicleId, string Plate, Guid EmployeeId, string EmployeeNo, string EmployeeName, DateOnly ValidFrom, DateOnly? ValidUntilExclusive, string Status, Guid? ProjectIdSnapshot, Guid? CostCenterIdSnapshot, string? Note, int Version);

public sealed record RecordOdometerRequest(int OdometerKm, DateTimeOffset OccurredAt, string Source, string? ExternalEventId, string? Note);
public sealed record VehicleOdometerSummary(Guid Id, Guid VehicleId, int OdometerKm, DateTimeOffset OccurredAt, string Source, string? ExternalEventId, string? Note);
public sealed record CreateMaintenanceRequest(int OdometerKm, DateTimeOffset OccurredAt, string MaintenanceType, string Description, decimal Cost, string Currency, DateOnly ServiceDate, DateOnly? NextDueDate, int? NextDueOdometerKm, string? Vendor);
public sealed record VehicleMaintenanceSummary(Guid Id, Guid VehicleId, string Plate, Guid OdometerEventId, int OdometerKm, string MaintenanceType, string Description, decimal Cost, string Currency, DateOnly ServiceDate, DateOnly? NextDueDate, int? NextDueOdometerKm, string? Vendor);
public sealed record CreateFuelRecordRequest(int OdometerKm, DateTimeOffset FueledAt, decimal Liters, decimal TotalCost, string Currency, string? Station, string Source, string? ExternalEventId);
public sealed record VehicleFuelSummary(Guid Id, Guid VehicleId, string Plate, Guid OdometerEventId, int OdometerKm, decimal Liters, decimal TotalCost, string Currency, DateTimeOffset FueledAt, string? Station, string Source, string? ExternalEventId);

public sealed record VehicleResult<T>(bool Succeeded, T? Value, string? ErrorCode, string? ErrorMessage) where T : class
{
    public static VehicleResult<T> Success(T value) => new(true, value, null, null);
    public static VehicleResult<T> Failure(string code, string message) => new(false, null, code, message);
}
