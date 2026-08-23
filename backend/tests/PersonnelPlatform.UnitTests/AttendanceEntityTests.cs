using PersonnelPlatform.Domain.Attendance;
using Xunit;

namespace PersonnelPlatform.UnitTests;

public sealed class AttendanceEntityTests
{
    [Fact]
    public void Overnight_shift_calculates_minutes_across_midnight()
    {
        var shift = ShiftDefinition.Create(
            Guid.NewGuid(),
            "NIGHT",
            "Gece",
            new TimeOnly(20, 0),
            new TimeOnly(8, 0),
            60,
            10,
            10,
            DateTimeOffset.UtcNow,
            Guid.NewGuid());

        Assert.True(shift.CrossesMidnight);
        Assert.Equal(660, shift.PlannedMinutes);
        Assert.Equal(720, ShiftDefinition.CalculateGrossMinutes(new TimeOnly(20, 0), new TimeOnly(8, 0)));
    }

    [Fact]
    public void Day_shift_deducts_break_minutes()
    {
        var shift = ShiftDefinition.Create(
            Guid.NewGuid(),
            "DAY",
            "Gündüz",
            new TimeOnly(8, 0),
            new TimeOnly(17, 0),
            60,
            0,
            0,
            DateTimeOffset.UtcNow,
            Guid.NewGuid());

        Assert.False(shift.CrossesMidnight);
        Assert.Equal(480, shift.PlannedMinutes);
    }

    [Fact]
    public void Non_work_calendar_day_cannot_have_planned_minutes()
    {
        Assert.Throws<ArgumentException>(() => WorkCalendarDay.Create(
            Guid.NewGuid(),
            new DateOnly(2026, 9, 6),
            WorkCalendarDayTypes.Holiday,
            480,
            true,
            null,
            DateTimeOffset.UtcNow,
            Guid.NewGuid()));
    }

    [Fact]
    public void Assignment_rejects_end_before_start()
    {
        Assert.Throws<ArgumentException>(() => EmployeeShiftAssignment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 9, 10),
            new DateOnly(2026, 9, 9),
            null,
            DateTimeOffset.UtcNow,
            Guid.NewGuid()));
    }
}
