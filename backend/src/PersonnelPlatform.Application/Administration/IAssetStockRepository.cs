using PersonnelPlatform.Domain.Administration;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Application.Administration;

public interface IAssetStockRepository
{
    Task<Employee?> FindEmployeeAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<ProjectCostSnapshot?> FindProjectSnapshotAsync(Guid employeeId, DateOnly date, CancellationToken cancellationToken);

    Task<StockLocation?> FindLocationAsync(Guid locationId, CancellationToken cancellationToken);
    Task<StockItem?> FindStockItemAsync(Guid itemId, CancellationToken cancellationToken);
    Task<StockMovement?> FindMovementByExternalEventAsync(Guid companyId, string source, string externalEventId, CancellationToken cancellationToken);
    Task<decimal> GetBalanceAsync(Guid itemId, Guid locationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<StockLocationSummary>> ListLocationsAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, CancellationToken cancellationToken);
    Task<IReadOnlyList<StockItemSummary>> ListStockItemsAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, CancellationToken cancellationToken);
    Task<IReadOnlyList<StockBalanceSummary>> ListBalancesAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, Guid? itemId, CancellationToken cancellationToken);
    Task<IReadOnlyList<StockMovementSummary>> ListMovementsAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, Guid? itemId, Guid? employeeId, DateTimeOffset? from, DateTimeOffset? to, int take, CancellationToken cancellationToken);
    void AddLocation(StockLocation location);
    void AddStockItem(StockItem item);
    void AddMovement(StockMovement movement);

    Task<AssetItem?> FindAssetAsync(Guid assetId, CancellationToken cancellationToken);
    Task<AssetAssignment?> FindActiveAssignmentByAssetAsync(Guid assetId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssetSummary>> ListAssetsAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, string? status, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssetAssignmentSummary>> ListAssetAssignmentsAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, Guid? employeeId, string? status, CancellationToken cancellationToken);
    void AddAsset(AssetItem asset);
    void AddAssignment(AssetAssignment assignment);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
