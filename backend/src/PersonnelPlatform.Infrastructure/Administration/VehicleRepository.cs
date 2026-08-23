using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Administration;
using PersonnelPlatform.Domain.Administration;
using PersonnelPlatform.Domain.Personnel;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Administration;

public sealed class VehicleRepository(ApplicationDbContext db) : IVehicleRepository
{
    public Task<Employee?> FindEmployeeAsync(Guid employeeId, CancellationToken ct) => db.Employees.FirstOrDefaultAsync(x => x.Id == employeeId && x.DeletedAt == null, ct);

    public async Task<ProjectCostSnapshot?> FindProjectSnapshotAsync(Guid employeeId, DateOnly date, CancellationToken ct) =>
        await db.EmployeeProjectAssignments.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.DeletedAt == null && x.Status == "ACTIVE" && x.ValidFrom <= date && (x.ValidUntil == null || x.ValidUntil >= date))
            .OrderByDescending(x => x.AllocationPercent).ThenByDescending(x => x.ValidFrom)
            .Select(x => new ProjectCostSnapshot(x.ProjectId, x.CostCenterId))
            .FirstOrDefaultAsync(ct);

    public Task<Vehicle?> FindVehicleAsync(Guid vehicleId, CancellationToken ct) => db.Vehicles.FirstOrDefaultAsync(x => x.Id == vehicleId && x.DeletedAt == null, ct);

    public async Task<IReadOnlyList<VehicleSummary>> ListVehiclesAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, string? status, CancellationToken ct)
    {
        var query = db.Vehicles.AsNoTracking().Where(x => x.DeletedAt == null);
        if (!globalAccess) query = query.Where(x => companyIds.Contains(x.CompanyId));
        if (companyId is not null) query = query.Where(x => x.CompanyId == companyId.Value);
        if (status is not null) query = query.Where(x => x.Status == status);
        var rows = await query.OrderBy(x => x.Plate).ToListAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var kms = await db.VehicleOdometerEvents.AsNoTracking().Where(x => ids.Contains(x.VehicleId) && x.DeletedAt == null)
            .GroupBy(x => x.VehicleId).Select(g => new { VehicleId = g.Key, Km = g.Max(x => x.OdometerKm) }).ToDictionaryAsync(x => x.VehicleId, x => x.Km, ct);
        var assignmentRows = await (from a in db.VehicleAssignments.AsNoTracking()
                                    join e in db.Employees.AsNoTracking() on a.EmployeeId equals e.Id
                                    where ids.Contains(a.VehicleId) && a.DeletedAt == null && a.Status == VehicleAssignmentStatuses.Active && e.DeletedAt == null
                                    orderby a.ValidFrom descending
                                    select new { a.VehicleId, a.EmployeeId, e.EmployeeNo, EmployeeName = e.FirstName + " " + e.LastName }).ToListAsync(ct);
        var assignments = assignmentRows.GroupBy(x => x.VehicleId).ToDictionary(g => g.Key, g => g.First());
        return rows.Select(x =>
        {
            assignments.TryGetValue(x.Id, out var a); kms.TryGetValue(x.Id, out var km);
            return new VehicleSummary(x.Id, x.CompanyId, x.Plate, x.Vin, x.Brand, x.Model, x.ModelYear, x.Status, x.InsuranceValidUntil, x.InspectionValidUntil, kms.ContainsKey(x.Id) ? km : null, a?.EmployeeId, a?.EmployeeNo, a?.EmployeeName, x.Version);
        }).ToArray();
    }

    public void AddVehicle(Vehicle vehicle) => db.Vehicles.Add(vehicle);

    public Task<VehicleAssignment?> FindAssignmentAsync(Guid assignmentId, CancellationToken ct) => db.VehicleAssignments.FirstOrDefaultAsync(x => x.Id == assignmentId && x.DeletedAt == null, ct);

    public Task<VehicleAssignment?> FindActiveAssignmentAsync(Guid vehicleId, DateOnly date, CancellationToken ct) =>
        db.VehicleAssignments.FirstOrDefaultAsync(x => x.VehicleId == vehicleId && x.DeletedAt == null && x.Status == VehicleAssignmentStatuses.Active && x.ValidFrom <= date && (x.ValidUntilExclusive == null || x.ValidUntilExclusive > date), ct);

    public async Task<IReadOnlyList<VehicleAssignmentSummary>> ListAssignmentsAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, Guid? vehicleId, Guid? employeeId, string? status, CancellationToken ct)
    {
        var query = from a in db.VehicleAssignments.AsNoTracking()
                    join v in db.Vehicles.AsNoTracking() on a.VehicleId equals v.Id
                    join e in db.Employees.AsNoTracking() on a.EmployeeId equals e.Id
                    where a.DeletedAt == null && v.DeletedAt == null && e.DeletedAt == null
                    select new { a, v, e };
        if (!globalAccess) query = query.Where(x => companyIds.Contains(x.a.CompanyId));
        if (companyId is not null) query = query.Where(x => x.a.CompanyId == companyId.Value);
        if (vehicleId is not null) query = query.Where(x => x.a.VehicleId == vehicleId.Value);
        if (employeeId is not null) query = query.Where(x => x.a.EmployeeId == employeeId.Value);
        if (status is not null) query = query.Where(x => x.a.Status == status);
        return await query.OrderByDescending(x => x.a.ValidFrom).Select(x => new VehicleAssignmentSummary(x.a.Id, x.a.CompanyId, x.a.VehicleId, x.v.Plate, x.a.EmployeeId, x.e.EmployeeNo, x.e.FirstName + " " + x.e.LastName, x.a.ValidFrom, x.a.ValidUntilExclusive, x.a.Status, x.a.ProjectIdSnapshot, x.a.CostCenterIdSnapshot, x.a.Note, x.a.Version)).ToListAsync(ct);
    }

    public void AddAssignment(VehicleAssignment assignment) => db.VehicleAssignments.Add(assignment);

    public Task<VehicleOdometerEvent?> FindOdometerByExternalEventAsync(Guid companyId, string source, string externalEventId, CancellationToken ct) =>
        db.VehicleOdometerEvents.AsNoTracking().FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Source == source && x.ExternalEventId == externalEventId && x.DeletedAt == null, ct);

    public Task<int?> GetLatestOdometerKmAsync(Guid vehicleId, CancellationToken ct) =>
        db.VehicleOdometerEvents.AsNoTracking().Where(x => x.VehicleId == vehicleId && x.DeletedAt == null).Select(x => (int?)x.OdometerKm).MaxAsync(ct);

    public async Task<IReadOnlyList<VehicleOdometerSummary>> ListOdometerAsync(Guid vehicleId, int take, CancellationToken ct) =>
        await db.VehicleOdometerEvents.AsNoTracking().Where(x => x.VehicleId == vehicleId && x.DeletedAt == null).OrderByDescending(x => x.OccurredAt).Take(take).Select(x => new VehicleOdometerSummary(x.Id, x.VehicleId, x.OdometerKm, x.OccurredAt, x.Source, x.ExternalEventId, x.Note)).ToListAsync(ct);

    public void AddOdometer(VehicleOdometerEvent odometerEvent) => db.VehicleOdometerEvents.Add(odometerEvent);

    public async Task<IReadOnlyList<VehicleMaintenanceSummary>> ListMaintenanceAsync(Guid vehicleId, int take, CancellationToken ct) =>
        await (from m in db.VehicleMaintenanceRecords.AsNoTracking()
               join v in db.Vehicles.AsNoTracking() on m.VehicleId equals v.Id
               join o in db.VehicleOdometerEvents.AsNoTracking() on m.OdometerEventId equals o.Id
               where m.VehicleId == vehicleId && m.DeletedAt == null
               orderby m.ServiceDate descending
               select new VehicleMaintenanceSummary(m.Id, m.VehicleId, v.Plate, m.OdometerEventId, o.OdometerKm, m.MaintenanceType, m.Description, m.Cost, m.Currency, m.ServiceDate, m.NextDueDate, m.NextDueOdometerKm, m.Vendor)).Take(take).ToListAsync(ct);

    public void AddMaintenance(VehicleMaintenanceRecord maintenance) => db.VehicleMaintenanceRecords.Add(maintenance);

    public Task<VehicleFuelRecord?> FindFuelByExternalEventAsync(Guid companyId, string source, string externalEventId, CancellationToken ct) =>
        db.VehicleFuelRecords.AsNoTracking().FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Source == source && x.ExternalEventId == externalEventId && x.DeletedAt == null, ct);

    public async Task<IReadOnlyList<VehicleFuelSummary>> ListFuelAsync(Guid vehicleId, int take, CancellationToken ct) =>
        await (from f in db.VehicleFuelRecords.AsNoTracking()
               join v in db.Vehicles.AsNoTracking() on f.VehicleId equals v.Id
               join o in db.VehicleOdometerEvents.AsNoTracking() on f.OdometerEventId equals o.Id
               where f.VehicleId == vehicleId && f.DeletedAt == null
               orderby f.FueledAt descending
               select new VehicleFuelSummary(f.Id, f.VehicleId, v.Plate, f.OdometerEventId, o.OdometerKm, f.Liters, f.TotalCost, f.Currency, f.FueledAt, f.Station, f.Source, f.ExternalEventId)).Take(take).ToListAsync(ct);

    public void AddFuel(VehicleFuelRecord fuel) => db.VehicleFuelRecords.Add(fuel);

    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
