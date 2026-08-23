using PersonnelPlatform.Domain.Camp;
using Xunit;

namespace PersonnelPlatform.UnitTests;

public sealed class CampEntityTests
{
    [Fact]
    public void Stay_uses_half_open_date_range_for_night_count()
    {
        var stay = CreateStay(new DateOnly(2026, 8, 24), new DateOnly(2026, 8, 27), 100m);

        Assert.Equal(3, stay.NightsAsOf(new DateOnly(2026, 8, 30)));
        Assert.Equal(300m, stay.CostAsOf(new DateOnly(2026, 8, 30)));
    }

    [Fact]
    public void Open_stay_counts_until_as_of_exclusive()
    {
        var stay = CreateStay(new DateOnly(2026, 8, 24), null, 125.50m);

        Assert.Equal(2, stay.NightsAsOf(new DateOnly(2026, 8, 26)));
        Assert.Equal(251m, stay.CostAsOf(new DateOnly(2026, 8, 26)));
    }

    [Fact]
    public void Close_freezes_total_cost_snapshot()
    {
        var stay = CreateStay(new DateOnly(2026, 8, 24), null, 150m);

        stay.Close(new DateOnly(2026, 8, 27), DateTimeOffset.UtcNow, Guid.NewGuid());

        Assert.Equal(AccommodationStayStatuses.Closed, stay.Status);
        Assert.Equal(3, stay.NightsAsOf(new DateOnly(2026, 9, 1)));
        Assert.Equal(450m, stay.TotalCostSnapshot);
    }

    [Fact]
    public void Check_out_must_be_after_check_in()
    {
        Assert.Throws<ArgumentException>(() => CreateStay(new DateOnly(2026, 8, 24), new DateOnly(2026, 8, 24), 100m));
    }

    [Fact]
    public void Rate_period_is_half_open_and_requires_positive_price()
    {
        var rate = AccommodationRate.Create(
            Guid.NewGuid(),
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 9, 1),
            250.126m,
            "try",
            DateTimeOffset.UtcNow,
            Guid.NewGuid());

        Assert.Equal(250.13m, rate.NightlyRate);
        Assert.Equal("TRY", rate.Currency);
        Assert.Equal(new DateOnly(2026, 9, 1), rate.ValidUntilExclusive);
    }

    [Fact]
    public void Cancelled_stay_cannot_be_closed()
    {
        var stay = CreateStay(new DateOnly(2026, 8, 24), null, 100m);
        stay.Cancel(DateTimeOffset.UtcNow, Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => stay.Close(new DateOnly(2026, 8, 25), DateTimeOffset.UtcNow, Guid.NewGuid()));
    }

    private static AccommodationStay CreateStay(DateOnly checkIn, DateOnly? checkOutExclusive, decimal rate) =>
        AccommodationStay.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            checkIn,
            checkOutExclusive,
            rate,
            "TRY",
            "test",
            DateTimeOffset.UtcNow,
            Guid.NewGuid());
}
