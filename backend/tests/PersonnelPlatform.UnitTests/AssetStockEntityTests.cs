using PersonnelPlatform.Domain.Administration;
using Xunit;

namespace PersonnelPlatform.UnitTests;

public sealed class AssetStockEntityTests
{
    [Fact]
    public void Issue_movement_has_negative_signed_quantity()
    {
        var row = StockMovement.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null, null, StockMovementTypes.Issue, 4.5m, StockMovementSources.Manual, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Guid.NewGuid());
        Assert.Equal(-4.5m, row.SignedQuantity);
    }

    [Fact]
    public void Receipt_movement_has_positive_signed_quantity()
    {
        var row = StockMovement.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null, null, StockMovementTypes.Receipt, 3m, StockMovementSources.Manual, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Guid.NewGuid());
        Assert.Equal(3m, row.SignedQuantity);
    }

    [Fact]
    public void Integration_movement_requires_external_event_id()
    {
        Assert.Throws<ArgumentException>(() => StockMovement.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null, null, StockMovementTypes.Receipt, 1m, StockMovementSources.Integration, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Guid.NewGuid()));
    }

    [Fact]
    public void Asset_assignment_lifecycle_returns_asset_to_available()
    {
        var actor = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var asset = AssetItem.Create(Guid.NewGuid(), null, "LPT-001", "Laptop", "IT", "SN-1", null, 1000m, "USD", null, now, actor);
        var assignment = AssetAssignment.Create(asset.CompanyId, asset.Id, Guid.NewGuid(), null, null, new DateOnly(2026, 8, 23), null, null, now, actor);
        asset.Assign(now, actor);
        assignment.Return(new DateOnly(2026, 8, 25), false, now.AddMinutes(1), actor);
        asset.Return(false, null, now.AddMinutes(1), actor);
        Assert.Equal(AssetAssignmentStatuses.Returned, assignment.Status);
        Assert.Equal(AssetStatuses.Available, asset.Status);
    }

    [Fact]
    public void Damaged_return_moves_asset_to_maintenance()
    {
        var actor = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var asset = AssetItem.Create(Guid.NewGuid(), null, "TOOL-1", "Tool", "TOOLS", null, null, null, null, null, now, actor);
        var assignment = AssetAssignment.Create(asset.CompanyId, asset.Id, Guid.NewGuid(), null, null, new DateOnly(2026, 8, 23), null, null, now, actor);
        asset.Assign(now, actor);
        assignment.Return(new DateOnly(2026, 8, 24), true, now.AddMinutes(1), actor);
        asset.Return(true, null, now.AddMinutes(1), actor);
        Assert.Equal(AssetAssignmentStatuses.Damaged, assignment.Status);
        Assert.Equal(AssetStatuses.Maintenance, asset.Status);
    }

    [Fact]
    public void Lost_assignment_marks_asset_lost()
    {
        var actor = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var asset = AssetItem.Create(Guid.NewGuid(), null, "PH-1", "Phone", "IT", null, null, null, null, null, now, actor);
        var assignment = AssetAssignment.Create(asset.CompanyId, asset.Id, Guid.NewGuid(), null, null, new DateOnly(2026, 8, 23), null, null, now, actor);
        asset.Assign(now, actor);
        assignment.MarkLost(new DateOnly(2026, 8, 24), now.AddMinutes(1), actor);
        asset.MarkLost(now.AddMinutes(1), actor);
        Assert.Equal(AssetAssignmentStatuses.Lost, assignment.Status);
        Assert.Equal(AssetStatuses.Lost, asset.Status);
    }

    [Fact]
    public void Assigned_asset_cannot_be_assigned_twice()
    {
        var asset = AssetItem.Create(Guid.NewGuid(), null, "A-1", "Asset", "GEN", null, null, null, null, null, DateTimeOffset.UtcNow, Guid.NewGuid());
        asset.Assign(DateTimeOffset.UtcNow, Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => asset.Assign(DateTimeOffset.UtcNow, Guid.NewGuid()));
    }

    [Fact]
    public void Due_date_cannot_be_before_assigned_date()
    {
        Assert.Throws<ArgumentException>(() => AssetAssignment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null, new DateOnly(2026, 8, 23), new DateOnly(2026, 8, 22), null, DateTimeOffset.UtcNow, Guid.NewGuid()));
    }
}
