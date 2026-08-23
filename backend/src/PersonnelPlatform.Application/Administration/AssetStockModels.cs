namespace PersonnelPlatform.Application.Administration;

public static class AdministrationPermissions
{
    public const string StockView = "administration.stock.view";
    public const string StockManage = "administration.stock.manage";
    public const string StockMovementRecord = "administration.stock.movement.record";
    public const string AssetView = "administration.asset.view";
    public const string AssetManage = "administration.asset.manage";
    public const string AssetAssign = "administration.asset.assign";
}

public sealed record CreateStockLocationRequest(Guid CompanyId, string Code, string Name);
public sealed record StockLocationSummary(Guid Id, Guid CompanyId, string Code, string Name, bool IsActive, int Version);
public sealed record CreateStockItemRequest(Guid CompanyId, string Code, string Name, string Unit, decimal MinimumLevel);
public sealed record StockItemSummary(Guid Id, Guid CompanyId, string Code, string Name, string Unit, decimal MinimumLevel, bool IsActive, decimal TotalBalance, bool IsBelowMinimum, int Version);
public sealed record StockBalanceSummary(Guid StockItemId, string ItemCode, string ItemName, string Unit, Guid LocationId, string LocationCode, string LocationName, decimal Balance, decimal MinimumLevel, bool IsBelowMinimum);
public sealed record CreateStockMovementRequest(Guid StockItemId, Guid LocationId, Guid? EmployeeId, string MovementType, decimal Quantity, string Source, string? ExternalEventId, string? Note, DateTimeOffset OccurredAt);
public sealed record StockMovementSummary(Guid Id, Guid CompanyId, Guid StockItemId, string ItemCode, string ItemName, Guid LocationId, string LocationCode, Guid? EmployeeId, string? EmployeeNo, string? EmployeeName, string MovementType, decimal Quantity, decimal SignedQuantity, string Source, string? ExternalEventId, DateTimeOffset OccurredAt, string? Note);

public sealed record CreateAssetRequest(Guid CompanyId, Guid? LocationId, string AssetTag, string Name, string Category, string? SerialNumber, DateOnly? PurchaseDate, decimal? PurchaseCost, string? Currency, string? Note);
public sealed record AssetSummary(Guid Id, Guid CompanyId, Guid? LocationId, string AssetTag, string Name, string Category, string? SerialNumber, string Status, DateOnly? PurchaseDate, decimal? PurchaseCost, string? Currency, Guid? AssignedEmployeeId, string? AssignedEmployeeNo, string? AssignedEmployeeName, Guid? ActiveAssignmentId, int Version, int? AssignmentVersion);
public sealed record AssignAssetRequest(Guid AssetId, Guid EmployeeId, DateOnly AssignedDate, DateOnly? DueDate, string? Note);
public sealed record ReturnAssetRequest(int AssetVersion, int AssignmentVersion, DateOnly ReturnedDate, bool Damaged, Guid? LocationId);
public sealed record MarkAssetLostRequest(int AssetVersion, int AssignmentVersion, DateOnly LostDate);
public sealed record AssetAssignmentSummary(Guid Id, Guid CompanyId, Guid AssetId, string AssetTag, Guid EmployeeId, string EmployeeNo, string EmployeeName, DateOnly AssignedDate, DateOnly? DueDate, DateOnly? ReturnedDate, string Status, Guid? ProjectIdSnapshot, Guid? CostCenterIdSnapshot, string? Note, int Version);

public sealed record ProjectCostSnapshot(Guid ProjectId, Guid? CostCenterId);
public sealed record AdministrationResult<T>(bool Succeeded, T? Value, string? ErrorCode, string? ErrorMessage) where T : class
{
    public static AdministrationResult<T> Success(T value) => new(true, value, null, null);
    public static AdministrationResult<T> Failure(string code, string message) => new(false, null, code, message);
}
