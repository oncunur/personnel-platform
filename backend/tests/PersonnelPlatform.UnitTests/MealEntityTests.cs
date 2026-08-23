using PersonnelPlatform.Domain.Meal;
using Xunit;

namespace PersonnelPlatform.UnitTests;

public sealed class MealEntityTests
{
    [Fact]
    public void Rate_normalizes_price_and_currency()
    {
        var row = MealRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 1),
            125.126m, "try", DateTimeOffset.UtcNow, Guid.NewGuid());

        Assert.Equal(125.13m, row.UnitPrice);
        Assert.Equal("TRY", row.Currency);
    }

    [Fact]
    public void Rate_end_must_be_after_start()
    {
        Assert.Throws<ArgumentException>(() => MealRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 1),
            100m, "TRY", DateTimeOffset.UtcNow, Guid.NewGuid()));
    }

    [Fact]
    public void Consumption_snapshots_unit_and_total_cost()
    {
        var row = CreateConsumption(1.5m, 80m, MealConsumptionSources.Manual, null);

        Assert.Equal(1.5m, row.Quantity);
        Assert.Equal(80m, row.UnitPriceSnapshot);
        Assert.Equal(120m, row.TotalCostSnapshot);
        Assert.Equal("TRY", row.CurrencySnapshot);
    }

    [Fact]
    public void Quantity_must_be_positive_and_reasonable()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateConsumption(0m, 80m, MealConsumptionSources.Manual, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateConsumption(11m, 80m, MealConsumptionSources.Manual, null));
    }

    [Fact]
    public void Import_requires_external_event_id()
    {
        Assert.Throws<ArgumentException>(() => CreateConsumption(1m, 80m, MealConsumptionSources.Import, null));
    }

    [Fact]
    public void Manual_source_may_omit_external_event_id()
    {
        var row = CreateConsumption(1m, 80m, "manual", null);

        Assert.Equal(MealConsumptionSources.Manual, row.Source);
        Assert.Null(row.ExternalEventId);
    }

    [Fact]
    public void Integration_normalizes_source_and_keeps_external_event_id()
    {
        var row = CreateConsumption(1m, 80m, "integration", "meal-evt-001");

        Assert.Equal(MealConsumptionSources.Integration, row.Source);
        Assert.Equal("meal-evt-001", row.ExternalEventId);
    }

    private static MealConsumption CreateConsumption(decimal quantity, decimal unitPrice, string source, string? externalEventId) =>
        MealConsumption.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 23),
            quantity,
            unitPrice,
            "try",
            Guid.NewGuid(),
            Guid.NewGuid(),
            source,
            externalEventId,
            "test",
            DateTimeOffset.UtcNow,
            Guid.NewGuid());
}
