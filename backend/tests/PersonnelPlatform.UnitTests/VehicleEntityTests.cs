using PersonnelPlatform.Domain.Administration;

namespace PersonnelPlatform.UnitTests;

public sealed class VehicleEntityTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 8, 23, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateVehicle_NormalizesPlateAndStartsActive()
    {
        var row = Vehicle.Create(CompanyId, "34 abc 123", "vin123", "Ford", "Transit", 2024, null, null, null, Now, ActorId);
        Assert.Equal("34ABC123", row.Plate);
        Assert.Equal("VIN123", row.Vin);
        Assert.Equal(VehicleStatuses.Active, row.Status);
    }

    [Fact]
    public void Assignment_Close_UsesExclusiveEndAndIncrementsVersion()
    {
        var row = VehicleAssignment.Create(CompanyId, Guid.NewGuid(), Guid.NewGuid(), null, null, new DateOnly(2026, 8, 1), null, null, Now, ActorId);
        row.Close(new DateOnly(2026, 8, 10), Now.AddHours(1), ActorId);
        Assert.Equal(VehicleAssignmentStatuses.Closed, row.Status);
        Assert.Equal(new DateOnly(2026, 8, 10), row.ValidUntilExclusive);
        Assert.Equal(2, row.Version);
    }

    [Fact]
    public void Assignment_RejectsInvalidDateRange()
    {
        Assert.Throws<ArgumentException>(() => VehicleAssignment.Create(CompanyId, Guid.NewGuid(), Guid.NewGuid(), null, null, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 10), null, Now, ActorId));
    }

    [Fact]
    public void Odometer_RequiresExternalIdForIntegration()
    {
        Assert.Throws<ArgumentException>(() => VehicleOdometerEvent.Create(CompanyId, Guid.NewGuid(), 1000, Now, VehicleEventSources.Integration, null, null, Now, ActorId));
    }

    [Fact]
    public void Maintenance_RejectsNextDueDateBeforeServiceDate()
    {
        Assert.Throws<ArgumentException>(() => VehicleMaintenanceRecord.Create(CompanyId, Guid.NewGuid(), Guid.NewGuid(), "SERVICE", "Periodic", 100m, "TRY", new DateOnly(2026, 8, 20), new DateOnly(2026, 8, 19), null, null, Now, ActorId));
    }

    [Fact]
    public void Fuel_RoundsLitersAndCost()
    {
        var row = VehicleFuelRecord.Create(CompanyId, Guid.NewGuid(), Guid.NewGuid(), 45.1236m, 1234.567m, "try", Now, "Station", VehicleEventSources.Manual, null, Now, ActorId);
        Assert.Equal(45.124m, row.Liters);
        Assert.Equal(1234.57m, row.TotalCost);
        Assert.Equal("TRY", row.Currency);
    }
}
