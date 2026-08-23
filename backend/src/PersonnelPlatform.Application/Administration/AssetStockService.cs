using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Domain.Administration;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Application.Administration;

public sealed class AssetStockService(
    IAssetStockRepository repository,
    AccessControlService accessControlService,
    TimeProvider timeProvider)
{
    public async Task<AdministrationResult<IReadOnlyList<StockLocationSummary>>> ListLocationsAsync(Guid userId, Guid? companyId, CancellationToken cancellationToken)
    {
        var access = await ResolveAccessAsync(userId, cancellationToken);
        if (companyId is not null && !access.Global && !access.CompanyIds.Contains(companyId.Value))
            return AdministrationResult<IReadOnlyList<StockLocationSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return AdministrationResult<IReadOnlyList<StockLocationSummary>>.Success(await repository.ListLocationsAsync(access.Global, access.CompanyIds, companyId, cancellationToken));
    }

    public async Task<AdministrationResult<StockLocationSummary>> CreateLocationAsync(Guid userId, CreateStockLocationRequest request, CancellationToken cancellationToken)
    {
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, request.CompanyId, cancellationToken))
            return AdministrationResult<StockLocationSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        try
        {
            var row = StockLocation.Create(request.CompanyId, request.Code, request.Name, timeProvider.GetUtcNow(), userId);
            repository.AddLocation(row); await repository.SaveChangesAsync(cancellationToken);
            return AdministrationResult<StockLocationSummary>.Success(new(row.Id, row.CompanyId, row.Code, row.Name, row.IsActive, row.Version));
        }
        catch (ArgumentException) { return AdministrationResult<StockLocationSummary>.Failure("STOCK_LOCATION_INVALID", "Stok lokasyonu bilgileri geçersiz."); }
    }

    public async Task<AdministrationResult<IReadOnlyList<StockItemSummary>>> ListStockItemsAsync(Guid userId, Guid? companyId, CancellationToken cancellationToken)
    {
        var access = await ResolveAccessAsync(userId, cancellationToken);
        if (companyId is not null && !access.Global && !access.CompanyIds.Contains(companyId.Value))
            return AdministrationResult<IReadOnlyList<StockItemSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return AdministrationResult<IReadOnlyList<StockItemSummary>>.Success(await repository.ListStockItemsAsync(access.Global, access.CompanyIds, companyId, cancellationToken));
    }

    public async Task<AdministrationResult<StockItemSummary>> CreateStockItemAsync(Guid userId, CreateStockItemRequest request, CancellationToken cancellationToken)
    {
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, request.CompanyId, cancellationToken))
            return AdministrationResult<StockItemSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        try
        {
            var row = StockItem.Create(request.CompanyId, request.Code, request.Name, request.Unit, request.MinimumLevel, timeProvider.GetUtcNow(), userId);
            repository.AddStockItem(row); await repository.SaveChangesAsync(cancellationToken);
            return AdministrationResult<StockItemSummary>.Success(new(row.Id, row.CompanyId, row.Code, row.Name, row.Unit, row.MinimumLevel, row.IsActive, 0m, row.MinimumLevel > 0m, row.Version));
        }
        catch (ArgumentException) { return AdministrationResult<StockItemSummary>.Failure("STOCK_ITEM_INVALID", "Stok kalemi bilgileri geçersiz."); }
    }

    public async Task<AdministrationResult<IReadOnlyList<StockBalanceSummary>>> ListBalancesAsync(Guid userId, Guid? companyId, Guid? itemId, CancellationToken cancellationToken)
    {
        var access = await ResolveAccessAsync(userId, cancellationToken);
        if (companyId is not null && !access.Global && !access.CompanyIds.Contains(companyId.Value))
            return AdministrationResult<IReadOnlyList<StockBalanceSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return AdministrationResult<IReadOnlyList<StockBalanceSummary>>.Success(await repository.ListBalancesAsync(access.Global, access.CompanyIds, companyId, itemId, cancellationToken));
    }

    public async Task<AdministrationResult<StockMovementSummary>> RecordMovementAsync(Guid userId, CreateStockMovementRequest request, CancellationToken cancellationToken)
    {
        var item = await repository.FindStockItemAsync(request.StockItemId, cancellationToken);
        if (item is null) return AdministrationResult<StockMovementSummary>.Failure("STOCK_ITEM_NOT_FOUND", "Stok kalemi bulunamadı.");
        if (!item.IsActive) return AdministrationResult<StockMovementSummary>.Failure("STOCK_ITEM_INACTIVE", "Pasif stok kalemi için hareket kaydedilemez.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, item.CompanyId, cancellationToken))
            return AdministrationResult<StockMovementSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");

        var location = await repository.FindLocationAsync(request.LocationId, cancellationToken);
        if (location is null) return AdministrationResult<StockMovementSummary>.Failure("STOCK_LOCATION_NOT_FOUND", "Stok lokasyonu bulunamadı.");
        if (!location.IsActive) return AdministrationResult<StockMovementSummary>.Failure("STOCK_LOCATION_INACTIVE", "Pasif stok lokasyonunda hareket kaydedilemez.");
        if (location.CompanyId != item.CompanyId) return AdministrationResult<StockMovementSummary>.Failure("STOCK_COMPANY_MISMATCH", "Stok kalemi ve lokasyon aynı şirkete bağlı olmalıdır.");

        Employee? employee = null;
        if (request.EmployeeId is not null)
        {
            employee = await repository.FindEmployeeAsync(request.EmployeeId.Value, cancellationToken);
            if (employee is null) return AdministrationResult<StockMovementSummary>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
            if (employee.CompanyId != item.CompanyId) return AdministrationResult<StockMovementSummary>.Failure("STOCK_EMPLOYEE_COMPANY_MISMATCH", "Personel ve stok aynı şirkete bağlı olmalıdır.");
        }

        var source = string.IsNullOrWhiteSpace(request.Source) ? StockMovementSources.Manual : request.Source.Trim().ToUpperInvariant();
        if (source != StockMovementSources.Manual && !string.IsNullOrWhiteSpace(request.ExternalEventId))
        {
            if (await repository.FindMovementByExternalEventAsync(item.CompanyId, source, request.ExternalEventId.Trim(), cancellationToken) is not null)
                return AdministrationResult<StockMovementSummary>.Failure("STOCK_EXTERNAL_EVENT_DUPLICATE", "Aynı harici stok hareketi daha önce kaydedilmiş.");
        }

        var type = request.MovementType.Trim().ToUpperInvariant();
        if (type is StockMovementTypes.Issue or StockMovementTypes.CorrectionOut)
        {
            var balance = await repository.GetBalanceAsync(item.Id, location.Id, cancellationToken);
            if (balance < request.Quantity)
                return AdministrationResult<StockMovementSummary>.Failure("STOCK_NEGATIVE_NOT_ALLOWED", $"Yetersiz stok. Mevcut bakiye: {balance:0.###} {item.Unit}.");
        }

        try
        {
            var localDate = DateOnly.FromDateTime(request.OccurredAt.DateTime);
            var project = employee is null ? null : await repository.FindProjectSnapshotAsync(employee.Id, localDate, cancellationToken);
            var row = StockMovement.Create(item.CompanyId, item.Id, location.Id, employee?.Id, project?.ProjectId, project?.CostCenterId, type, request.Quantity, source, request.ExternalEventId, request.Note, request.OccurredAt, timeProvider.GetUtcNow(), userId);
            repository.AddMovement(row); await repository.SaveChangesAsync(cancellationToken);
            return AdministrationResult<StockMovementSummary>.Success(new(row.Id, row.CompanyId, row.StockItemId, item.Code, item.Name, row.LocationId, location.Code, row.EmployeeId, employee?.EmployeeNo, employee is null ? null : $"{employee.FirstName} {employee.LastName}", row.MovementType, row.Quantity, row.SignedQuantity, row.Source, row.ExternalEventId, row.OccurredAt, row.Note));
        }
        catch (ArgumentException) { return AdministrationResult<StockMovementSummary>.Failure("STOCK_MOVEMENT_INVALID", "Stok hareketi bilgileri geçersiz."); }
    }

    public async Task<AdministrationResult<IReadOnlyList<StockMovementSummary>>> ListMovementsAsync(Guid userId, Guid? companyId, Guid? itemId, Guid? employeeId, DateTimeOffset? from, DateTimeOffset? to, int take, CancellationToken cancellationToken)
    {
        var access = await ResolveAccessAsync(userId, cancellationToken);
        if (companyId is not null && !access.Global && !access.CompanyIds.Contains(companyId.Value))
            return AdministrationResult<IReadOnlyList<StockMovementSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return AdministrationResult<IReadOnlyList<StockMovementSummary>>.Success(await repository.ListMovementsAsync(access.Global, access.CompanyIds, companyId, itemId, employeeId, from, to, Math.Clamp(take, 1, 500), cancellationToken));
    }

    public async Task<AdministrationResult<IReadOnlyList<AssetSummary>>> ListAssetsAsync(Guid userId, Guid? companyId, string? status, CancellationToken cancellationToken)
    {
        var access = await ResolveAccessAsync(userId, cancellationToken);
        if (companyId is not null && !access.Global && !access.CompanyIds.Contains(companyId.Value))
            return AdministrationResult<IReadOnlyList<AssetSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return AdministrationResult<IReadOnlyList<AssetSummary>>.Success(await repository.ListAssetsAsync(access.Global, access.CompanyIds, companyId, string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant(), cancellationToken));
    }

    public async Task<AdministrationResult<AssetSummary>> CreateAssetAsync(Guid userId, CreateAssetRequest request, CancellationToken cancellationToken)
    {
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, request.CompanyId, cancellationToken))
            return AdministrationResult<AssetSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (request.LocationId is not null)
        {
            var location = await repository.FindLocationAsync(request.LocationId.Value, cancellationToken);
            if (location is null) return AdministrationResult<AssetSummary>.Failure("STOCK_LOCATION_NOT_FOUND", "Stok lokasyonu bulunamadı.");
            if (location.CompanyId != request.CompanyId) return AdministrationResult<AssetSummary>.Failure("ASSET_LOCATION_COMPANY_MISMATCH", "Demirbaş ve lokasyon aynı şirkete bağlı olmalıdır.");
        }
        try
        {
            var row = AssetItem.Create(request.CompanyId, request.LocationId, request.AssetTag, request.Name, request.Category, request.SerialNumber, request.PurchaseDate, request.PurchaseCost, request.Currency, request.Note, timeProvider.GetUtcNow(), userId);
            repository.AddAsset(row); await repository.SaveChangesAsync(cancellationToken);
            return AdministrationResult<AssetSummary>.Success(new(row.Id, row.CompanyId, row.LocationId, row.AssetTag, row.Name, row.Category, row.SerialNumber, row.Status, row.PurchaseDate, row.PurchaseCost, row.Currency, null, null, null, null, row.Version, null));
        }
        catch (ArgumentException) { return AdministrationResult<AssetSummary>.Failure("ASSET_INVALID", "Demirbaş bilgileri geçersiz."); }
    }

    public async Task<AdministrationResult<AssetAssignmentSummary>> AssignAssetAsync(Guid userId, AssignAssetRequest request, CancellationToken cancellationToken)
    {
        var asset = await repository.FindAssetAsync(request.AssetId, cancellationToken);
        if (asset is null) return AdministrationResult<AssetAssignmentSummary>.Failure("ASSET_NOT_FOUND", "Demirbaş bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, asset.CompanyId, cancellationToken))
            return AdministrationResult<AssetAssignmentSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (asset.Status != AssetStatuses.Available) return AdministrationResult<AssetAssignmentSummary>.Failure("ASSET_STATE_INVALID", "Yalnız AVAILABLE durumundaki demirbaş zimmetlenebilir.");
        if (await repository.FindActiveAssignmentByAssetAsync(asset.Id, cancellationToken) is not null)
            return AdministrationResult<AssetAssignmentSummary>.Failure("ASSET_ALREADY_ASSIGNED", "Demirbaşın aktif zimmeti zaten bulunuyor.");
        var employee = await repository.FindEmployeeAsync(request.EmployeeId, cancellationToken);
        if (employee is null) return AdministrationResult<AssetAssignmentSummary>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (employee.Status != EmployeeStatuses.Active) return AdministrationResult<AssetAssignmentSummary>.Failure("EMPLOYEE_INACTIVE", "Yalnız aktif personele zimmet yapılabilir.");
        if (employee.CompanyId != asset.CompanyId) return AdministrationResult<AssetAssignmentSummary>.Failure("ASSET_EMPLOYEE_COMPANY_MISMATCH", "Personel ve demirbaş aynı şirkete bağlı olmalıdır.");
        try
        {
            var project = await repository.FindProjectSnapshotAsync(employee.Id, request.AssignedDate, cancellationToken);
            var now = timeProvider.GetUtcNow(); asset.Assign(now, userId);
            var assignment = AssetAssignment.Create(asset.CompanyId, asset.Id, employee.Id, project?.ProjectId, project?.CostCenterId, request.AssignedDate, request.DueDate, request.Note, now, userId);
            repository.AddAssignment(assignment); await repository.SaveChangesAsync(cancellationToken);
            return AdministrationResult<AssetAssignmentSummary>.Success(new(assignment.Id, assignment.CompanyId, asset.Id, asset.AssetTag, employee.Id, employee.EmployeeNo, $"{employee.FirstName} {employee.LastName}", assignment.AssignedDate, assignment.DueDate, assignment.ReturnedDate, assignment.Status, assignment.ProjectIdSnapshot, assignment.CostCenterIdSnapshot, assignment.Note, assignment.Version));
        }
        catch (InvalidOperationException) { return AdministrationResult<AssetAssignmentSummary>.Failure("ASSET_STATE_INVALID", "Demirbaş mevcut durumda zimmetlenemez."); }
        catch (ArgumentException) { return AdministrationResult<AssetAssignmentSummary>.Failure("ASSET_ASSIGNMENT_INVALID", "Zimmet bilgileri geçersiz."); }
    }

    public async Task<AdministrationResult<AssetAssignmentSummary>> ReturnAssetAsync(Guid userId, Guid assetId, ReturnAssetRequest request, CancellationToken cancellationToken)
    {
        var asset = await repository.FindAssetAsync(assetId, cancellationToken);
        if (asset is null) return AdministrationResult<AssetAssignmentSummary>.Failure("ASSET_NOT_FOUND", "Demirbaş bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, asset.CompanyId, cancellationToken))
            return AdministrationResult<AssetAssignmentSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var assignment = await repository.FindActiveAssignmentByAssetAsync(asset.Id, cancellationToken);
        if (assignment is null) return AdministrationResult<AssetAssignmentSummary>.Failure("ASSET_ASSIGNMENT_NOT_FOUND", "Aktif zimmet kaydı bulunamadı.");
        if (asset.Version != request.AssetVersion || assignment.Version != request.AssignmentVersion)
            return AdministrationResult<AssetAssignmentSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Demirbaş veya zimmet kaydı başka bir işlem tarafından değiştirildi. Veriyi yenileyin.");
        StockLocation? location = null;
        if (request.LocationId is not null)
        {
            location = await repository.FindLocationAsync(request.LocationId.Value, cancellationToken);
            if (location is null) return AdministrationResult<AssetAssignmentSummary>.Failure("STOCK_LOCATION_NOT_FOUND", "İade lokasyonu bulunamadı.");
            if (location.CompanyId != asset.CompanyId) return AdministrationResult<AssetAssignmentSummary>.Failure("ASSET_LOCATION_COMPANY_MISMATCH", "İade lokasyonu demirbaş şirketiyle aynı olmalıdır.");
        }
        var employee = await repository.FindEmployeeAsync(assignment.EmployeeId, cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow(); assignment.Return(request.ReturnedDate, request.Damaged, now, userId); asset.Return(request.Damaged, location?.Id ?? asset.LocationId, now, userId); await repository.SaveChangesAsync(cancellationToken);
            return AdministrationResult<AssetAssignmentSummary>.Success(new(assignment.Id, assignment.CompanyId, asset.Id, asset.AssetTag, assignment.EmployeeId, employee?.EmployeeNo ?? "—", employee is null ? "—" : $"{employee.FirstName} {employee.LastName}", assignment.AssignedDate, assignment.DueDate, assignment.ReturnedDate, assignment.Status, assignment.ProjectIdSnapshot, assignment.CostCenterIdSnapshot, assignment.Note, assignment.Version));
        }
        catch (InvalidOperationException) { return AdministrationResult<AssetAssignmentSummary>.Failure("ASSET_STATE_INVALID", "Demirbaş mevcut durumda iade edilemez."); }
        catch (ArgumentException) { return AdministrationResult<AssetAssignmentSummary>.Failure("ASSET_RETURN_INVALID", "İade bilgileri geçersiz."); }
    }

    public async Task<AdministrationResult<AssetAssignmentSummary>> MarkAssetLostAsync(Guid userId, Guid assetId, MarkAssetLostRequest request, CancellationToken cancellationToken)
    {
        var asset = await repository.FindAssetAsync(assetId, cancellationToken);
        if (asset is null) return AdministrationResult<AssetAssignmentSummary>.Failure("ASSET_NOT_FOUND", "Demirbaş bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, asset.CompanyId, cancellationToken))
            return AdministrationResult<AssetAssignmentSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var assignment = await repository.FindActiveAssignmentByAssetAsync(asset.Id, cancellationToken);
        if (assignment is null) return AdministrationResult<AssetAssignmentSummary>.Failure("ASSET_ASSIGNMENT_NOT_FOUND", "Aktif zimmet kaydı bulunamadı.");
        if (asset.Version != request.AssetVersion || assignment.Version != request.AssignmentVersion)
            return AdministrationResult<AssetAssignmentSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Demirbaş veya zimmet kaydı başka bir işlem tarafından değiştirildi. Veriyi yenileyin.");
        var employee = await repository.FindEmployeeAsync(assignment.EmployeeId, cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow(); assignment.MarkLost(request.LostDate, now, userId); asset.MarkLost(now, userId); await repository.SaveChangesAsync(cancellationToken);
            return AdministrationResult<AssetAssignmentSummary>.Success(new(assignment.Id, assignment.CompanyId, asset.Id, asset.AssetTag, assignment.EmployeeId, employee?.EmployeeNo ?? "—", employee is null ? "—" : $"{employee.FirstName} {employee.LastName}", assignment.AssignedDate, assignment.DueDate, assignment.ReturnedDate, assignment.Status, assignment.ProjectIdSnapshot, assignment.CostCenterIdSnapshot, assignment.Note, assignment.Version));
        }
        catch (InvalidOperationException) { return AdministrationResult<AssetAssignmentSummary>.Failure("ASSET_STATE_INVALID", "Demirbaş mevcut durumda kayıp olarak işaretlenemez."); }
        catch (ArgumentException) { return AdministrationResult<AssetAssignmentSummary>.Failure("ASSET_LOST_INVALID", "Kayıp bilgileri geçersiz."); }
    }

    public async Task<AdministrationResult<IReadOnlyList<AssetAssignmentSummary>>> ListAssignmentsAsync(Guid userId, Guid? companyId, Guid? employeeId, string? status, CancellationToken cancellationToken)
    {
        var access = await ResolveAccessAsync(userId, cancellationToken);
        if (companyId is not null && !access.Global && !access.CompanyIds.Contains(companyId.Value))
            return AdministrationResult<IReadOnlyList<AssetAssignmentSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return AdministrationResult<IReadOnlyList<AssetAssignmentSummary>>.Success(await repository.ListAssetAssignmentsAsync(access.Global, access.CompanyIds, companyId, employeeId, string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant(), cancellationToken));
    }

    private async Task<(bool Global, Guid[] CompanyIds)> ResolveAccessAsync(Guid userId, CancellationToken cancellationToken)
    {
        var snapshot = await accessControlService.GetSnapshotAsync(userId, cancellationToken);
        return (snapshot.Scopes.Any(x => x.ScopeType == ScopeTypes.Global), snapshot.Scopes.Where(x => x.ScopeType == ScopeTypes.Company && x.ScopeId is not null).Select(x => x.ScopeId!.Value).Distinct().ToArray());
    }
}
