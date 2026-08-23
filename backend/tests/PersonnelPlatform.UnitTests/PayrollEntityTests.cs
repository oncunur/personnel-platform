using PersonnelPlatform.Domain.Payroll;
using Xunit;

namespace PersonnelPlatform.UnitTests;

public sealed class PayrollEntityTests
{
    [Fact]
    public void Payroll_period_follows_required_state_machine()
    {
        var actor = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var period = PayrollPeriod.Create(Guid.NewGuid(), 2026, 8, 1, null, now, actor);

        period.Open(now.AddMinutes(1), actor);
        period.BeginCalculation(now.AddMinutes(2), actor);
        period.CompleteCalculation(now.AddMinutes(3), actor);
        period.StartReview(now.AddMinutes(4), actor);
        period.Approve(now.AddMinutes(5), actor);
        period.Close(now.AddMinutes(6), actor);

        Assert.Equal(PayrollPeriodStatuses.Closed, period.Status);
        Assert.NotNull(period.CalculatedAt);
        Assert.NotNull(period.ApprovedAt);
        Assert.NotNull(period.ClosedAt);
        Assert.Equal(7, period.Version);
    }

    [Fact]
    public void Payroll_period_cannot_skip_approval_states()
    {
        var period = PayrollPeriod.Create(Guid.NewGuid(), 2026, 8, 1, null, DateTimeOffset.UtcNow, Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => period.Close(DateTimeOffset.UtcNow, Guid.NewGuid()));
    }

    [Fact]
    public void Revision_requires_previous_period_reference()
    {
        Assert.Throws<ArgumentException>(() => PayrollPeriod.Create(Guid.NewGuid(), 2026, 8, 2, null, DateTimeOffset.UtcNow, Guid.NewGuid()));
        var revision = PayrollPeriod.Create(Guid.NewGuid(), 2026, 8, 2, Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid());
        Assert.Equal(2, revision.Revision);
    }

    [Fact]
    public void Compensation_requires_month_boundary()
    {
        Assert.Throws<ArgumentException>(() => EmployeeCompensation.Create(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 8, 2), null,
            60_000m, "TRY", 1.5m, DateTimeOffset.UtcNow, Guid.NewGuid()));
    }

    [Fact]
    public void Employee_result_calculates_absence_overtime_and_employer_cost()
    {
        var result = PayrollEmployeeResult.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            60_000m, "TRY", 1.5m,
            plannedMinutes: 10_000,
            workedMinutes: 8_000,
            paidLeaveMinutes: 1_000,
            approvedOvertimeMinutes: 120,
            mealEmployerCost: 1_000m,
            accommodationEmployerCost: 2_000m,
            sourceSnapshotJson: "{}",
            calculatedAt: DateTimeOffset.UtcNow);

        Assert.Equal(60_000m, result.BaseSalaryAmount);
        Assert.Equal(6_000m, result.AbsenceDeductionAmount);
        Assert.Equal(1_080m, result.OvertimeEarningAmount);
        Assert.Equal(55_080m, result.PayBeforeStatutory);
        Assert.Equal(58_080m, result.EmployerCostBeforeStatutory);
    }

    [Fact]
    public void Paid_leave_and_worked_minutes_are_capped_at_planned_minutes_for_absence()
    {
        var result = PayrollEmployeeResult.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            30_000m, "TRY", 1.5m,
            plannedMinutes: 5_000,
            workedMinutes: 4_500,
            paidLeaveMinutes: 1_000,
            approvedOvertimeMinutes: 0,
            mealEmployerCost: 0m,
            accommodationEmployerCost: 0m,
            sourceSnapshotJson: "{}",
            calculatedAt: DateTimeOffset.UtcNow);

        Assert.Equal(0m, result.AbsenceDeductionAmount);
        Assert.Equal(30_000m, result.PayBeforeStatutory);
    }
}
