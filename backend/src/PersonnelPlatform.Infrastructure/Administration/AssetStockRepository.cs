using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Administration;
using PersonnelPlatform.Domain.Administration;
using PersonnelPlatform.Domain.Personnel;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Administration;

public sealed class AssetStockRepository(ApplicationDbContext dbContext) : IAssetStockRepository
{
    public Task<Employee?> FindEmployeeAsync(Guid employeeId, CancellationToken cancellationToken) =>
        dbContext.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == employeeId && x.DeletedAt == null, cancellationToken);

    public async Task<ProjectCostSnapshot?> FindProjectSnapshotAsync(Guid employeeId, DateOnly date, CancellationToken cancellationToken) =>
        await dbContext.EmployeeProjectAssignments.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.DeletedAt == null && x.Status == ProjectAssignmentStatuses.Active && x.ValidFrom <= date && (x.ValidUntil == null || x.ValidUntil >= date))
            .OrderByDescending(x => x.AllocationPercent).ThenBy(x => x.ValidFrom)
            .Select(x => new ProjectCostSnapshot(x.ProjectId, x.CostCenterId))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<StockLocation?> FindLocationAsync(Guid locationId, CancellationToken cancellationToken) =>
        dbContext.StockLocations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == locationId && x.DeletedAt == null, cancellationToken);

    public Task<StockItem?> FindStockItemAsync(Guid itemId, CancellationToken cancellationToken) =>
        dbContext.StockItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == itemId && x.DeletedAt == null, cancellationToken);

    public Task<StockMovement?> FindMovementByExternalEventAsync(Guid companyId, string source, string externalEventId, CancellationToken cancellationToken) =>
        dbContext.StockMovements.AsNoTracking().FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Source == source && x.ExternalEventId == externalEventId && x.DeletedAt == null, cancellationToken);

    public async Task<decimal> GetBalanceAsync(Guid itemId, Guid locationId, CancellationToken cancellationToken) =>
        await dbContext.StockMovements.AsNoTracking()
            .Where(x => x.StockItemId == itemId && x.LocationId == locationId && x.DeletedAt == null)
            .SumAsync(x => x.MovementType == StockMovementTypes.Receipt || x.MovementType == StockMovementTypes.Return || x.MovementType == StockMovementTypes.CorrectionIn ? x.Quantity : -x.Quantity, cancellationToken);

    public async Task<IReadOnlyList<StockLocationSummary>> ListLocationsAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, CancellationToken cancellationToken)
    {
        var query = dbContext.StockLocations.AsNoTracking().Where(x => x.DeletedAt == null);
        if (!globalAccess) query = query.Where(x => companyIds.Contains(x.CompanyId));
        if (companyId is not null) query = query.Where(x => x.CompanyId == companyId.Value);
        return await query.OrderBy(x => x.Code).Select(x => new StockLocationSummary(x.Id, x.CompanyId, x.Code, x.Name, x.IsActive, x.Version)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StockItemSummary>> ListStockItemsAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, CancellationToken cancellationToken)
    {
        var items = dbContext.StockItems.AsNoTracking().Where(x => x.DeletedAt == null);
        if (!globalAccess) items = items.Where(x => companyIds.Contains(x.CompanyId));
        if (companyId is not null) items = items.Where(x => x.CompanyId == companyId.Value);
        return await items.OrderBy(x => x.Code)
            .Select(item => new StockItemSummary(
                item.Id, item.CompanyId, item.Code, item.Name, item.Unit, item.MinimumLevel, item.IsActive,
                dbContext.StockMovements.Where(m => m.StockItemId == item.Id && m.DeletedAt == null)
                    .Sum(m => (decimal?)(m.MovementType == StockMovementTypes.Receipt || m.MovementType == StockMovementTypes.Return || m.MovementType == StockMovementTypes.CorrectionIn ? m.Quantity : -m.Quantity)) ?? 0m,
                (dbContext.StockMovements.Where(m => m.StockItemId == item.Id && m.DeletedAt == null)
                    .Sum(m => (decimal?)(m.MovementType == StockMovementTypes.Receipt || m.MovementType == StockMovementTypes.Return || m.MovementType == StockMovementTypes.CorrectionIn ? m.Quantity : -m.Quantity)) ?? 0m) < item.MinimumLevel,
                item.Version))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StockBalanceSummary>> ListBalancesAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, Guid? itemId, CancellationToken cancellationToken)
    {
        var pairs =
            from movement in dbContext.StockMovements.AsNoTracking()
            join item in dbContext.StockItems.AsNoTracking() on movement.StockItemId equals item.Id
            join location in dbContext.StockLocations.AsNoTracking() on movement.LocationId equals location.Id
            where movement.DeletedAt == null && item.DeletedAt == null && location.DeletedAt == null
            select new { movement, item, location };
        if (!globalAccess) pairs = pairs.Where(x => companyIds.Contains(x.movement.CompanyId));
        if (companyId is not null) pairs = pairs.Where(x => x.movement.CompanyId == companyId.Value);
        if (itemId is not null) pairs = pairs.Where(x => x.item.Id == itemId.Value);

        return await pairs
            .GroupBy(x => new { x.item.Id, x.item.Code, x.item.Name, x.item.Unit, x.item.MinimumLevel, LocationId = x.location.Id, LocationCode = x.location.Code, LocationName = x.location.Name })
            .Select(g => new StockBalanceSummary(
                g.Key.Id, g.Key.Code, g.Key.Name, g.Key.Unit, g.Key.LocationId, g.Key.LocationCode, g.Key.LocationName,
                g.Sum(x => x.movement.MovementType == StockMovementTypes.Receipt || x.movement.MovementType == StockMovementTypes.Return || x.movement.MovementType == StockMovementTypes.CorrectionIn ? x.movement.Quantity : -x.movement.Quantity),
                g.Key.MinimumLevel,
                g.Sum(x => x.movement.MovementType == StockMovementTypes.Receipt || x.movement.MovementType == StockMovementTypes.Return || x.movement.MovementType == StockMovementTypes.CorrectionIn ? x.movement.Quantity : -x.movement.Quantity) < g.Key.MinimumLevel))
            .OrderBy(x => x.ItemCode).ThenBy(x => x.LocationCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StockMovementSummary>> ListMovementsAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, Guid? itemId, Guid? employeeId, DateTimeOffset? from, DateTimeOffset? to, int take, CancellationToken cancellationToken)
    {
        var query =
            from movement in dbContext.StockMovements.AsNoTracking()
            join item in dbContext.StockItems.AsNoTracking() on movement.StockItemId equals item.Id
            join location in dbContext.StockLocations.AsNoTracking() on movement.LocationId equals location.Id
            where movement.DeletedAt == null && item.DeletedAt == null && location.DeletedAt == null
            select new { movement, item, location };
        if (!globalAccess) query = query.Where(x => companyIds.Contains(x.movement.CompanyId));
        if (companyId is not null) query = query.Where(x => x.movement.CompanyId == companyId.Value);
        if (itemId is not null) query = query.Where(x => x.movement.StockItemId == itemId.Value);
        if (employeeId is not null) query = query.Where(x => x.movement.EmployeeId == employeeId.Value);
        if (from is not null) query = query.Where(x => x.movement.OccurredAt >= from.Value);
        if (to is not null) query = query.Where(x => x.movement.OccurredAt <= to.Value);

        return await query.OrderByDescending(x => x.movement.OccurredAt).Take(take)
            .Select(x => new StockMovementSummary(
                x.movement.Id, x.movement.CompanyId, x.item.Id, x.item.Code, x.item.Name, x.location.Id, x.location.Code,
                x.movement.EmployeeId,
                x.movement.EmployeeId == null ? null : dbContext.Employees.Where(e => e.Id == x.movement.EmployeeId && e.DeletedAt == null).Select(e => e.EmployeeNo).FirstOrDefault(),
                x.movement.EmployeeId == null ? null : dbContext.Employees.Where(e => e.Id == x.movement.EmployeeId && e.DeletedAt == null).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault(),
                x.movement.MovementType, x.movement.Quantity,
                x.movement.MovementType == StockMovementTypes.Receipt || x.movement.MovementType == StockMovementTypes.Return || x.movement.MovementType == StockMovementTypes.CorrectionIn ? x.movement.Quantity : -x.movement.Quantity,
                x.movement.Source, x.movement.ExternalEventId, x.movement.OccurredAt, x.movement.Note))
            .ToListAsync(cancellationToken);
    }

    public void AddLocation(StockLocation location) => dbContext.StockLocations.Add(location);
    public void AddStockItem(StockItem item) => dbContext.StockItems.Add(item);
    public void AddMovement(StockMovement movement) => dbContext.StockMovements.Add(movement);

    public Task<AssetItem?> FindAssetAsync(Guid assetId, CancellationToken cancellationToken) =>
        dbContext.Assets.FirstOrDefaultAsync(x => x.Id == assetId && x.DeletedAt == null, cancellationToken);

    public Task<AssetAssignment?> FindActiveAssignmentByAssetAsync(Guid assetId, CancellationToken cancellationToken) =>
        dbContext.AssetAssignments.FirstOrDefaultAsync(x => x.AssetId == assetId && x.Status == AssetAssignmentStatuses.Active && x.DeletedAt == null, cancellationToken);

    public async Task<IReadOnlyList<AssetSummary>> ListAssetsAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, string? status, CancellationToken cancellationToken)
    {
        var assets = dbContext.Assets.AsNoTracking().Where(x => x.DeletedAt == null);
        if (!globalAccess) assets = assets.Where(x => companyIds.Contains(x.CompanyId));
        if (companyId is not null) assets = assets.Where(x => x.CompanyId == companyId.Value);
        if (!string.IsNullOrWhiteSpace(status)) assets = assets.Where(x => x.Status == status);

        return await assets.OrderBy(x => x.AssetTag)
            .Select(asset => new AssetSummary(
                asset.Id, asset.CompanyId, asset.LocationId, asset.AssetTag, asset.Name, asset.Category, asset.SerialNumber, asset.Status, asset.PurchaseDate, asset.PurchaseCost, asset.Currency,
                dbContext.AssetAssignments.Where(a => a.AssetId == asset.Id && a.Status == AssetAssignmentStatuses.Active && a.DeletedAt == null).Select(a => (Guid?)a.EmployeeId).FirstOrDefault(),
                (from a in dbContext.AssetAssignments where a.AssetId == asset.Id && a.Status == AssetAssignmentStatuses.Active && a.DeletedAt == null join e in dbContext.Employees on a.EmployeeId equals e.Id select e.EmployeeNo).FirstOrDefault(),
                (from a in dbContext.AssetAssignments where a.AssetId == asset.Id && a.Status == AssetAssignmentStatuses.Active && a.DeletedAt == null join e in dbContext.Employees on a.EmployeeId equals e.Id select e.FirstName + " " + e.LastName).FirstOrDefault(),
                dbContext.AssetAssignments.Where(a => a.AssetId == asset.Id && a.Status == AssetAssignmentStatuses.Active && a.DeletedAt == null).Select(a => (Guid?)a.Id).FirstOrDefault(),
                asset.Version,
                dbContext.AssetAssignments.Where(a => a.AssetId == asset.Id && a.Status == AssetAssignmentStatuses.Active && a.DeletedAt == null).Select(a => (int?)a.Version).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AssetAssignmentSummary>> ListAssetAssignmentsAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, Guid? employeeId, string? status, CancellationToken cancellationToken)
    {
        var query =
            from assignment in dbContext.AssetAssignments.AsNoTracking()
            join asset in dbContext.Assets.AsNoTracking() on assignment.AssetId equals asset.Id
            join employee in dbContext.Employees.AsNoTracking() on assignment.EmployeeId equals employee.Id
            where assignment.DeletedAt == null && asset.DeletedAt == null && employee.DeletedAt == null
            select new { assignment, asset, employee };
        if (!globalAccess) query = query.Where(x => companyIds.Contains(x.assignment.CompanyId));
        if (companyId is not null) query = query.Where(x => x.assignment.CompanyId == companyId.Value);
        if (employeeId is not null) query = query.Where(x => x.assignment.EmployeeId == employeeId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.assignment.Status == status);
        return await query.OrderByDescending(x => x.assignment.AssignedDate)
            .Select(x => new AssetAssignmentSummary(x.assignment.Id, x.assignment.CompanyId, x.asset.Id, x.asset.AssetTag, x.employee.Id, x.employee.EmployeeNo, x.employee.FirstName + " " + x.employee.LastName, x.assignment.AssignedDate, x.assignment.DueDate, x.assignment.ReturnedDate, x.assignment.Status, x.assignment.ProjectIdSnapshot, x.assignment.CostCenterIdSnapshot, x.assignment.Note, x.assignment.Version))
            .ToListAsync(cancellationToken);
    }

    public void AddAsset(AssetItem asset) => dbContext.Assets.Add(asset);
    public void AddAssignment(AssetAssignment assignment) => dbContext.AssetAssignments.Add(assignment);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
