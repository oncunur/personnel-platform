using PersonnelPlatform.Domain.Leave;

namespace PersonnelPlatform.UnitTests;

public sealed class LeaveEntityTests
{
    [Fact]
    public void Draft_can_be_submitted_and_withdrawn()
    {
        var actor = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var leave = LeaveRequest.CreateDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 3),
            LeaveDayParts.FullDay,
            LeaveDayParts.FullDay,
            3m,
            "Planlı izin",
            now,
            actor);

        leave.Submit(now.AddMinutes(1), actor);
        Assert.Equal(LeaveRequestStatuses.Submitted, leave.Status);
        Assert.Equal(2, leave.Version);

        leave.Withdraw(now.AddMinutes(2), actor);
        Assert.Equal(LeaveRequestStatuses.Withdrawn, leave.Status);
        Assert.Equal(3, leave.Version);
    }

    [Fact]
    public void Balance_reserve_and_release_preserves_available_days()
    {
        var actor = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var entitlement = LeaveEntitlement.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            14m,
            2m,
            1m,
            null,
            now,
            actor);
        var balance = LeaveBalance.CreateFromEntitlement(entitlement, now, actor);

        Assert.Equal(17m, balance.AvailableDays);
        balance.Reserve(2.5m, now.AddMinutes(1), actor);
        Assert.Equal(14.5m, balance.AvailableDays);
        Assert.Equal(2.5m, balance.ReservedDays);

        balance.Release(2.5m, now.AddMinutes(2), actor);
        Assert.Equal(17m, balance.AvailableDays);
        Assert.Equal(0m, balance.ReservedDays);
    }

    [Fact]
    public void Insufficient_balance_cannot_be_reserved()
    {
        var actor = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var entitlement = LeaveEntitlement.Create(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 1m, 0m, 0m, null, now, actor);
        var balance = LeaveBalance.CreateFromEntitlement(entitlement, now, actor);

        Assert.Throws<InvalidOperationException>(() => balance.Reserve(1.5m, now, actor));
    }

    [Fact]
    public void Invalid_state_transition_is_rejected()
    {
        var actor = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var leave = LeaveRequest.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), LeaveDayParts.FirstHalf, LeaveDayParts.FirstHalf, 0.5m, null, now, actor);

        Assert.Throws<InvalidOperationException>(() => leave.Approve(now, actor));
    }
}
