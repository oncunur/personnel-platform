using PersonnelPlatform.Domain.Attendance;
using Xunit;

namespace PersonnelPlatform.UnitTests;

public sealed class DailyAttendanceCalculatorTests
{
    [Fact]
    public void Complete_day_shift_is_calculated_without_review()
    {
        var date = new DateOnly(2026, 8, 24);
        var input = new DailyAttendanceCalculationInput(
            date,
            new TimeOnly(8, 0),
            new TimeOnly(17, 0),
            60,
            480,
            10,
            10,
            WorkCalendarDayTypes.Workday,
            0m,
            false,
            [
                Punch(date, new TimeOnly(8, 5), RawAttendanceDirections.In),
                Punch(date, new TimeOnly(17, 5), RawAttendanceDirections.Out)
            ]);

        var result = DailyAttendanceCalculator.Calculate(input);

        Assert.Equal(DailyAttendanceStatuses.Worked, result.Status);
        Assert.Equal(DailyAttendanceProcessingStatuses.Calculated, result.ProcessingStatus);
        Assert.Equal(480, result.WorkedMinutes);
        Assert.Equal(0, result.LateMinutes);
        Assert.Equal(0, result.EarlyLeaveMinutes);
        Assert.Null(result.Message);
    }

    [Fact]
    public void Multiple_punches_require_review_even_when_worked_minutes_are_complete()
    {
        var date = new DateOnly(2026, 8, 24);
        var input = new DailyAttendanceCalculationInput(
            date,
            new TimeOnly(8, 0),
            new TimeOnly(17, 0),
            60,
            480,
            0,
            0,
            WorkCalendarDayTypes.Workday,
            0m,
            false,
            [
                Punch(date, new TimeOnly(8, 0), RawAttendanceDirections.In),
                Punch(date, new TimeOnly(12, 0), RawAttendanceDirections.Out),
                Punch(date, new TimeOnly(13, 0), RawAttendanceDirections.In),
                Punch(date, new TimeOnly(17, 0), RawAttendanceDirections.Out)
            ]);

        var result = DailyAttendanceCalculator.Calculate(input);

        Assert.Equal(DailyAttendanceStatuses.Worked, result.Status);
        Assert.Equal(DailyAttendanceProcessingStatuses.ReviewRequired, result.ProcessingStatus);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public void Overnight_shift_uses_next_day_out_event()
    {
        var date = new DateOnly(2026, 8, 24);
        var input = new DailyAttendanceCalculationInput(
            date,
            new TimeOnly(20, 0),
            new TimeOnly(8, 0),
            60,
            660,
            0,
            0,
            WorkCalendarDayTypes.Workday,
            0m,
            false,
            [
                Punch(date, new TimeOnly(20, 0), RawAttendanceDirections.In),
                Punch(date.AddDays(1), new TimeOnly(8, 0), RawAttendanceDirections.Out)
            ]);

        var result = DailyAttendanceCalculator.Calculate(input);

        Assert.Equal(660, result.WorkedMinutes);
        Assert.Equal(DailyAttendanceProcessingStatuses.Calculated, result.ProcessingStatus);
    }

    [Fact]
    public void Missing_punch_on_planned_day_requires_review()
    {
        var date = new DateOnly(2026, 8, 24);
        var input = new DailyAttendanceCalculationInput(
            date,
            new TimeOnly(8, 0),
            new TimeOnly(17, 0),
            60,
            480,
            0,
            0,
            WorkCalendarDayTypes.Workday,
            0m,
            false,
            [Punch(date, new TimeOnly(8, 0), RawAttendanceDirections.In)]);

        var result = DailyAttendanceCalculator.Calculate(input);

        Assert.Equal(DailyAttendanceStatuses.MissingRecord, result.Status);
        Assert.Equal(DailyAttendanceProcessingStatuses.ReviewRequired, result.ProcessingStatus);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public void Full_day_leave_with_no_punch_is_calculated_as_leave()
    {
        var date = new DateOnly(2026, 8, 24);
        var input = new DailyAttendanceCalculationInput(
            date,
            new TimeOnly(8, 0),
            new TimeOnly(17, 0),
            60,
            480,
            0,
            0,
            WorkCalendarDayTypes.Workday,
            1m,
            false,
            []);

        var result = DailyAttendanceCalculator.Calculate(input);

        Assert.Equal(DailyAttendanceStatuses.Leave, result.Status);
        Assert.Equal(DailyAttendanceProcessingStatuses.Calculated, result.ProcessingStatus);
        Assert.Equal(480, result.LeaveMinutes);
    }

    [Fact]
    public void Full_day_leave_with_punch_requires_review()
    {
        var date = new DateOnly(2026, 8, 24);
        var input = new DailyAttendanceCalculationInput(
            date,
            new TimeOnly(8, 0),
            new TimeOnly(17, 0),
            60,
            480,
            0,
            0,
            WorkCalendarDayTypes.Workday,
            1m,
            false,
            [
                Punch(date, new TimeOnly(8, 0), RawAttendanceDirections.In),
                Punch(date, new TimeOnly(17, 0), RawAttendanceDirections.Out)
            ]);

        var result = DailyAttendanceCalculator.Calculate(input);

        Assert.Equal(DailyAttendanceStatuses.Leave, result.Status);
        Assert.Equal(DailyAttendanceProcessingStatuses.ReviewRequired, result.ProcessingStatus);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public void Holiday_work_becomes_overtime_candidate_and_requires_review()
    {
        var date = new DateOnly(2026, 8, 30);
        var input = new DailyAttendanceCalculationInput(
            date,
            new TimeOnly(8, 0),
            new TimeOnly(17, 0),
            60,
            0,
            0,
            0,
            WorkCalendarDayTypes.Holiday,
            0m,
            false,
            [
                Punch(date, new TimeOnly(8, 0), RawAttendanceDirections.In),
                Punch(date, new TimeOnly(17, 0), RawAttendanceDirections.Out)
            ]);

        var result = DailyAttendanceCalculator.Calculate(input);

        Assert.Equal(DailyAttendanceStatuses.Worked, result.Status);
        Assert.Equal(DailyAttendanceProcessingStatuses.ReviewRequired, result.ProcessingStatus);
        Assert.Equal(480, result.OvertimeCandidateMinutes);
    }

    [Fact]
    public void Raw_event_preserves_source_local_snapshot_but_stores_event_in_utc()
    {
        var sourceTime = new DateTimeOffset(2026, 8, 24, 8, 15, 0, TimeSpan.FromHours(3));
        var row = RawAttendanceEvent.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            RawAttendanceSources.Pdks,
            RawAttendanceDirections.In,
            sourceTime,
            "TURNSTILE-1",
            "evt-1",
            null,
            DateTimeOffset.UtcNow,
            Guid.NewGuid());

        Assert.Equal(new DateOnly(2026, 8, 24), row.LocalDate);
        Assert.Equal(new TimeOnly(8, 15), row.LocalTime);
        Assert.Equal(180, row.UtcOffsetMinutes);
        Assert.Equal(TimeSpan.Zero, row.EventAt.Offset);
        Assert.Equal(sourceTime.UtcDateTime, row.EventAt.UtcDateTime);
    }

    private static AttendancePunchPoint Punch(DateOnly date, TimeOnly time, string direction)
    {
        var local = new DateTimeOffset(date.Year, date.Month, date.Day, time.Hour, time.Minute, 0, TimeSpan.FromHours(3));
        return new AttendancePunchPoint(Guid.NewGuid(), date, time, local.ToUniversalTime(), direction);
    }
}
