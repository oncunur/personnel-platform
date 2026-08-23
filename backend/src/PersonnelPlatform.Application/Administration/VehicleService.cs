using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Domain.Administration;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Application.Administration;

public sealed class VehicleService(
    IVehicleRepository repository,
    AccessControlService accessControlService,
    TimeProvider timeProvider)
{
    public async Task<VehicleResult<IReadOnlyList<VehicleSummary>>> ListVehiclesAsync(Guid userId, Guid? companyId, string? status, CancellationToken ct)
    {
        var access = await ResolveAccessAsync(userId, ct);
        if (companyId is not null && !access.Global && !access.CompanyIds.Contains(companyId.Value))
            return VehicleResult<IReadOnlyList<VehicleSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return VehicleResult<IReadOnlyList<VehicleSummary>>.Success(await repository.ListVehiclesAsync(access.Global, access.CompanyIds, companyId, Normalize(status), ct));
    }

    public async Task<VehicleResult<VehicleSummary>> CreateVehicleAsync(Guid userId, CreateVehicleRequest request, CancellationToken ct)
    {
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, request.CompanyId, ct))
            return VehicleResult<VehicleSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        try
        {
            var row = Vehicle.Create(request.CompanyId, request.Plate, request.Vin, request.Brand, request.Model, request.ModelYear, request.InsuranceValidUntil, request.InspectionValidUntil, request.Note, timeProvider.GetUtcNow(), userId);
            repository.AddVehicle(row); await repository.SaveChangesAsync(ct);
            return VehicleResult<VehicleSummary>.Success(new(row.Id, row.CompanyId, row.Plate, row.Vin, row.Brand, row.Model, row.ModelYear, row.Status, row.InsuranceValidUntil, row.InspectionValidUntil, null, null, null, null, row.Version));
        }
        catch (ArgumentException) { return VehicleResult<VehicleSummary>.Failure("VEHICLE_INVALID", "Araç bilgileri geçersiz."); }
    }

    public async Task<VehicleResult<VehicleSummary>> SetStatusAsync(Guid userId, Guid vehicleId, SetVehicleStatusRequest request, CancellationToken ct)
    {
        var vehicle = await repository.FindVehicleAsync(vehicleId, ct);
        if (vehicle is null) return VehicleResult<VehicleSummary>.Failure("VEHICLE_NOT_FOUND", "Araç bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, vehicle.CompanyId, ct)) return VehicleResult<VehicleSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (vehicle.Version != request.Version) return VehicleResult<VehicleSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Araç başka bir işlem tarafından değiştirildi. Veriyi yenileyin.");
        try
        {
            vehicle.SetStatus(request.Status, timeProvider.GetUtcNow(), userId); await repository.SaveChangesAsync(ct);
            var currentKm = await repository.GetLatestOdometerKmAsync(vehicle.Id, ct);
            var assignment = await repository.FindActiveAssignmentAsync(vehicle.Id, DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime), ct);
            Employee? employee = assignment is null ? null : await repository.FindEmployeeAsync(assignment.EmployeeId, ct);
            return VehicleResult<VehicleSummary>.Success(new(vehicle.Id, vehicle.CompanyId, vehicle.Plate, vehicle.Vin, vehicle.Brand, vehicle.Model, vehicle.ModelYear, vehicle.Status, vehicle.InsuranceValidUntil, vehicle.InspectionValidUntil, currentKm, employee?.Id, employee?.EmployeeNo, employee is null ? null : $"{employee.FirstName} {employee.LastName}", vehicle.Version));
        }
        catch (ArgumentException) { return VehicleResult<VehicleSummary>.Failure("VEHICLE_STATUS_INVALID", "Araç durumu geçersiz."); }
    }

    public async Task<VehicleResult<VehicleAssignmentSummary>> AssignAsync(Guid userId, AssignVehicleRequest request, CancellationToken ct)
    {
        var vehicle = await repository.FindVehicleAsync(request.VehicleId, ct);
        if (vehicle is null) return VehicleResult<VehicleAssignmentSummary>.Failure("VEHICLE_NOT_FOUND", "Araç bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, vehicle.CompanyId, ct)) return VehicleResult<VehicleAssignmentSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (vehicle.Status is VehicleStatuses.Retired or VehicleStatuses.OutOfService) return VehicleResult<VehicleAssignmentSummary>.Failure("VEHICLE_STATE_INVALID", "Hizmet dışı veya emekli araç personele atanamaz.");
        var employee = await repository.FindEmployeeAsync(request.EmployeeId, ct);
        if (employee is null) return VehicleResult<VehicleAssignmentSummary>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (employee.Status != EmployeeStatuses.Active) return VehicleResult<VehicleAssignmentSummary>.Failure("EMPLOYEE_INACTIVE", "Yalnız aktif personele araç atanabilir.");
        if (employee.CompanyId != vehicle.CompanyId) return VehicleResult<VehicleAssignmentSummary>.Failure("VEHICLE_EMPLOYEE_COMPANY_MISMATCH", "Araç ve personel aynı şirkete bağlı olmalıdır.");
        if (await repository.FindActiveAssignmentAsync(vehicle.Id, request.ValidFrom, ct) is not null) return VehicleResult<VehicleAssignmentSummary>.Failure("VEHICLE_ASSIGNMENT_DATE_CONFLICT", "Araç için bu tarihte aktif atama bulunuyor.");
        try
        {
            var project = await repository.FindProjectSnapshotAsync(employee.Id, request.ValidFrom, ct);
            var row = VehicleAssignment.Create(vehicle.CompanyId, vehicle.Id, employee.Id, project?.ProjectId, project?.CostCenterId, request.ValidFrom, request.ValidUntilExclusive, request.Note, timeProvider.GetUtcNow(), userId);
            repository.AddAssignment(row); await repository.SaveChangesAsync(ct);
            return VehicleResult<VehicleAssignmentSummary>.Success(new(row.Id, row.CompanyId, row.VehicleId, vehicle.Plate, row.EmployeeId, employee.EmployeeNo, $"{employee.FirstName} {employee.LastName}", row.ValidFrom, row.ValidUntilExclusive, row.Status, row.ProjectIdSnapshot, row.CostCenterIdSnapshot, row.Note, row.Version));
        }
        catch (ArgumentException) { return VehicleResult<VehicleAssignmentSummary>.Failure("VEHICLE_ASSIGNMENT_INVALID", "Araç atama bilgileri geçersiz."); }
    }

    public async Task<VehicleResult<VehicleAssignmentSummary>> CloseAssignmentAsync(Guid userId, Guid assignmentId, CloseVehicleAssignmentRequest request, CancellationToken ct)
    {
        var row = await repository.FindAssignmentAsync(assignmentId, ct);
        if (row is null) return VehicleResult<VehicleAssignmentSummary>.Failure("VEHICLE_ASSIGNMENT_NOT_FOUND", "Araç ataması bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, row.CompanyId, ct)) return VehicleResult<VehicleAssignmentSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (row.Version != request.Version) return VehicleResult<VehicleAssignmentSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Araç ataması başka bir işlem tarafından değiştirildi. Veriyi yenileyin.");
        var vehicle = await repository.FindVehicleAsync(row.VehicleId, ct);
        var employee = await repository.FindEmployeeAsync(row.EmployeeId, ct);
        try
        {
            row.Close(request.ValidUntilExclusive, timeProvider.GetUtcNow(), userId); await repository.SaveChangesAsync(ct);
            return VehicleResult<VehicleAssignmentSummary>.Success(new(row.Id, row.CompanyId, row.VehicleId, vehicle?.Plate ?? "—", row.EmployeeId, employee?.EmployeeNo ?? "—", employee is null ? "—" : $"{employee.FirstName} {employee.LastName}", row.ValidFrom, row.ValidUntilExclusive, row.Status, row.ProjectIdSnapshot, row.CostCenterIdSnapshot, row.Note, row.Version));
        }
        catch (InvalidOperationException) { return VehicleResult<VehicleAssignmentSummary>.Failure("VEHICLE_ASSIGNMENT_STATE_INVALID", "Araç ataması mevcut durumda kapatılamaz."); }
        catch (ArgumentException) { return VehicleResult<VehicleAssignmentSummary>.Failure("VEHICLE_ASSIGNMENT_INVALID", "Araç atama bitiş tarihi geçersiz."); }
    }

    public async Task<VehicleResult<IReadOnlyList<VehicleAssignmentSummary>>> ListAssignmentsAsync(Guid userId, Guid? companyId, Guid? vehicleId, Guid? employeeId, string? status, CancellationToken ct)
    {
        var access = await ResolveAccessAsync(userId, ct);
        if (companyId is not null && !access.Global && !access.CompanyIds.Contains(companyId.Value)) return VehicleResult<IReadOnlyList<VehicleAssignmentSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return VehicleResult<IReadOnlyList<VehicleAssignmentSummary>>.Success(await repository.ListAssignmentsAsync(access.Global, access.CompanyIds, companyId, vehicleId, employeeId, Normalize(status), ct));
    }

    public async Task<VehicleResult<VehicleOdometerSummary>> RecordOdometerAsync(Guid userId, Guid vehicleId, RecordOdometerRequest request, CancellationToken ct)
    {
        var vehicle = await repository.FindVehicleAsync(vehicleId, ct);
        if (vehicle is null) return VehicleResult<VehicleOdometerSummary>.Failure("VEHICLE_NOT_FOUND", "Araç bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, vehicle.CompanyId, ct)) return VehicleResult<VehicleOdometerSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var source = string.IsNullOrWhiteSpace(request.Source) ? VehicleEventSources.Manual : request.Source.Trim().ToUpperInvariant();
        if (source != VehicleEventSources.Manual && !string.IsNullOrWhiteSpace(request.ExternalEventId) && await repository.FindOdometerByExternalEventAsync(vehicle.CompanyId, source, request.ExternalEventId.Trim(), ct) is not null)
            return VehicleResult<VehicleOdometerSummary>.Failure("VEHICLE_EXTERNAL_EVENT_DUPLICATE", "Aynı harici kilometre olayı daha önce kaydedilmiş.");
        var latest = await repository.GetLatestOdometerKmAsync(vehicle.Id, ct);
        if (latest is not null && request.OdometerKm < latest.Value) return VehicleResult<VehicleOdometerSummary>.Failure("VEHICLE_ODOMETER_REGRESSION", $"Kilometre son değerden küçük olamaz. Son değer: {latest.Value} km.");
        try
        {
            var row = VehicleOdometerEvent.Create(vehicle.CompanyId, vehicle.Id, request.OdometerKm, request.OccurredAt, source, request.ExternalEventId, request.Note, timeProvider.GetUtcNow(), userId);
            repository.AddOdometer(row); await repository.SaveChangesAsync(ct);
            return VehicleResult<VehicleOdometerSummary>.Success(new(row.Id, row.VehicleId, row.OdometerKm, row.OccurredAt, row.Source, row.ExternalEventId, row.Note));
        }
        catch (ArgumentException) { return VehicleResult<VehicleOdometerSummary>.Failure("VEHICLE_ODOMETER_INVALID", "Kilometre kaydı geçersiz."); }
    }

    public async Task<VehicleResult<IReadOnlyList<VehicleOdometerSummary>>> ListOdometerAsync(Guid userId, Guid vehicleId, int take, CancellationToken ct)
    {
        var access = await CheckVehicleAccessAsync(userId, vehicleId, ct); if (!access.Succeeded || access.Value is null) return VehicleResult<IReadOnlyList<VehicleOdometerSummary>>.Failure(access.ErrorCode!, access.ErrorMessage!);
        return VehicleResult<IReadOnlyList<VehicleOdometerSummary>>.Success(await repository.ListOdometerAsync(vehicleId, Math.Clamp(take, 1, 500), ct));
    }

    public async Task<VehicleResult<VehicleMaintenanceSummary>> CreateMaintenanceAsync(Guid userId, Guid vehicleId, CreateMaintenanceRequest request, CancellationToken ct)
    {
        var access = await CheckVehicleAccessAsync(userId, vehicleId, ct); if (!access.Succeeded || access.Value is null) return VehicleResult<VehicleMaintenanceSummary>.Failure(access.ErrorCode!, access.ErrorMessage!);
        var vehicle = access.Value;
        var latest = await repository.GetLatestOdometerKmAsync(vehicle.Id, ct);
        if (latest is not null && request.OdometerKm < latest.Value) return VehicleResult<VehicleMaintenanceSummary>.Failure("VEHICLE_ODOMETER_REGRESSION", $"Bakım kilometresi son değerden küçük olamaz. Son değer: {latest.Value} km.");
        try
        {
            var now = timeProvider.GetUtcNow();
            var odometer = VehicleOdometerEvent.Create(vehicle.CompanyId, vehicle.Id, request.OdometerKm, request.OccurredAt, VehicleEventSources.Manual, null, $"Maintenance: {request.MaintenanceType}", now, userId);
            var row = VehicleMaintenanceRecord.Create(vehicle.CompanyId, vehicle.Id, odometer.Id, request.MaintenanceType, request.Description, request.Cost, request.Currency, request.ServiceDate, request.NextDueDate, request.NextDueOdometerKm, request.Vendor, now, userId);
            repository.AddOdometer(odometer); repository.AddMaintenance(row); await repository.SaveChangesAsync(ct);
            return VehicleResult<VehicleMaintenanceSummary>.Success(new(row.Id, row.VehicleId, vehicle.Plate, row.OdometerEventId, odometer.OdometerKm, row.MaintenanceType, row.Description, row.Cost, row.Currency, row.ServiceDate, row.NextDueDate, row.NextDueOdometerKm, row.Vendor));
        }
        catch (ArgumentException) { return VehicleResult<VehicleMaintenanceSummary>.Failure("VEHICLE_MAINTENANCE_INVALID", "Bakım/servis bilgileri geçersiz."); }
    }

    public async Task<VehicleResult<IReadOnlyList<VehicleMaintenanceSummary>>> ListMaintenanceAsync(Guid userId, Guid vehicleId, int take, CancellationToken ct)
    {
        var access = await CheckVehicleAccessAsync(userId, vehicleId, ct); if (!access.Succeeded) return VehicleResult<IReadOnlyList<VehicleMaintenanceSummary>>.Failure(access.ErrorCode!, access.ErrorMessage!);
        return VehicleResult<IReadOnlyList<VehicleMaintenanceSummary>>.Success(await repository.ListMaintenanceAsync(vehicleId, Math.Clamp(take, 1, 500), ct));
    }

    public async Task<VehicleResult<VehicleFuelSummary>> CreateFuelAsync(Guid userId, Guid vehicleId, CreateFuelRecordRequest request, CancellationToken ct)
    {
        var access = await CheckVehicleAccessAsync(userId, vehicleId, ct); if (!access.Succeeded || access.Value is null) return VehicleResult<VehicleFuelSummary>.Failure(access.ErrorCode!, access.ErrorMessage!);
        var vehicle = access.Value;
        var source = string.IsNullOrWhiteSpace(request.Source) ? VehicleEventSources.Manual : request.Source.Trim().ToUpperInvariant();
        if (source != VehicleEventSources.Manual && !string.IsNullOrWhiteSpace(request.ExternalEventId) && await repository.FindFuelByExternalEventAsync(vehicle.CompanyId, source, request.ExternalEventId.Trim(), ct) is not null)
            return VehicleResult<VehicleFuelSummary>.Failure("VEHICLE_FUEL_EXTERNAL_EVENT_DUPLICATE", "Aynı harici yakıt kaydı daha önce işlendi.");
        var latest = await repository.GetLatestOdometerKmAsync(vehicle.Id, ct);
        if (latest is not null && request.OdometerKm < latest.Value) return VehicleResult<VehicleFuelSummary>.Failure("VEHICLE_ODOMETER_REGRESSION", $"Yakıt kilometresi son değerden küçük olamaz. Son değer: {latest.Value} km.");
        try
        {
            var now = timeProvider.GetUtcNow();
            var odometer = VehicleOdometerEvent.Create(vehicle.CompanyId, vehicle.Id, request.OdometerKm, request.FueledAt, source, request.ExternalEventId is null ? null : $"FUEL:{request.ExternalEventId}", "Fuel", now, userId);
            var row = VehicleFuelRecord.Create(vehicle.CompanyId, vehicle.Id, odometer.Id, request.Liters, request.TotalCost, request.Currency, request.FueledAt, request.Station, source, request.ExternalEventId, now, userId);
            repository.AddOdometer(odometer); repository.AddFuel(row); await repository.SaveChangesAsync(ct);
            return VehicleResult<VehicleFuelSummary>.Success(new(row.Id, row.VehicleId, vehicle.Plate, row.OdometerEventId, odometer.OdometerKm, row.Liters, row.TotalCost, row.Currency, row.FueledAt, row.Station, row.Source, row.ExternalEventId));
        }
        catch (ArgumentException) { return VehicleResult<VehicleFuelSummary>.Failure("VEHICLE_FUEL_INVALID", "Yakıt kaydı bilgileri geçersiz."); }
    }

    public async Task<VehicleResult<IReadOnlyList<VehicleFuelSummary>>> ListFuelAsync(Guid userId, Guid vehicleId, int take, CancellationToken ct)
    {
        var access = await CheckVehicleAccessAsync(userId, vehicleId, ct); if (!access.Succeeded) return VehicleResult<IReadOnlyList<VehicleFuelSummary>>.Failure(access.ErrorCode!, access.ErrorMessage!);
        return VehicleResult<IReadOnlyList<VehicleFuelSummary>>.Success(await repository.ListFuelAsync(vehicleId, Math.Clamp(take, 1, 500), ct));
    }

    private async Task<VehicleResult<Vehicle>> CheckVehicleAccessAsync(Guid userId, Guid vehicleId, CancellationToken ct)
    {
        var vehicle = await repository.FindVehicleAsync(vehicleId, ct);
        if (vehicle is null) return VehicleResult<Vehicle>.Failure("VEHICLE_NOT_FOUND", "Araç bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, vehicle.CompanyId, ct)) return VehicleResult<Vehicle>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return VehicleResult<Vehicle>.Success(vehicle);
    }

    private async Task<(bool Global, HashSet<Guid> CompanyIds)> ResolveAccessAsync(Guid userId, CancellationToken ct)
    {
        var snapshot = await accessControlService.GetSnapshotAsync(userId, ct);
        return (snapshot.Scopes.Any(x => x.ScopeType == ScopeTypes.Global), snapshot.Scopes.Where(x => x.ScopeType == ScopeTypes.Company && x.ScopeId is not null).Select(x => x.ScopeId!.Value).ToHashSet());
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
