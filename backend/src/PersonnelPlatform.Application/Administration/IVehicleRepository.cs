using PersonnelPlatform.Domain.Administration;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Application.Administration;

public interface IVehicleRepository
{
    Task<Employee?> FindEmployeeAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<ProjectCostSnapshot?> FindProjectSnapshotAsync(Guid employeeId, DateOnly date, CancellationToken cancellationToken);

    Task<Vehicle?> FindVehicleAsync(Guid vehicleId, CancellationToken cancellationToken);
    Task<IReadOnlyList<VehicleSummary>> ListVehiclesAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, string? status, CancellationToken cancellationToken);
    void AddVehicle(Vehicle vehicle);

    Task<VehicleAssignment?> FindAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken);
    Task<VehicleAssignment?> FindActiveAssignmentAsync(Guid vehicleId, DateOnly date, CancellationToken cancellationToken);
    Task<IReadOnlyList<VehicleAssignmentSummary>> ListAssignmentsAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, Guid? vehicleId, Guid? employeeId, string? status, CancellationToken cancellationToken);
    void AddAssignment(VehicleAssignment assignment);

    Task<VehicleOdometerEvent?> FindOdometerByExternalEventAsync(Guid companyId, string source, string externalEventId, CancellationToken cancellationToken);
    Task<int?> GetLatestOdometerKmAsync(Guid vehicleId, CancellationToken cancellationToken);
    Task<IReadOnlyList<VehicleOdometerSummary>> ListOdometerAsync(Guid vehicleId, int take, CancellationToken cancellationToken);
    void AddOdometer(VehicleOdometerEvent odometerEvent);

    Task<IReadOnlyList<VehicleMaintenanceSummary>> ListMaintenanceAsync(Guid vehicleId, int take, CancellationToken cancellationToken);
    void AddMaintenance(VehicleMaintenanceRecord maintenance);

    Task<VehicleFuelRecord?> FindFuelByExternalEventAsync(Guid companyId, string source, string externalEventId, CancellationToken cancellationToken);
    Task<IReadOnlyList<VehicleFuelSummary>> ListFuelAsync(Guid vehicleId, int take, CancellationToken cancellationToken);
    void AddFuel(VehicleFuelRecord fuel);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
