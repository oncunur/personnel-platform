namespace PersonnelPlatform.Domain.Attendance;

public sealed record AttendancePunchPoint(Guid EventId, DateOnly LocalDate, TimeOnly LocalTime, DateTimeOffset EventAt, string Direction);

public sealed record DailyAttendanceCalculationInput(
    DateOnly AttendanceDate,
    TimeOnly ShiftStartTime,
    TimeOnly ShiftEndTime,
    int ShiftBreakMinutes,
    int PlannedMinutes,
    int GraceInMinutes,
    int GraceOutMinutes,
    string CalendarDayType,
    decimal LeaveDayFraction,
    bool IsSickLeave,
    IReadOnlyList<AttendancePunchPoint> Punches);

public sealed record DailyAttendanceCalculationResult(
    string Status,
    string ProcessingStatus,
    int PlannedMinutes,
    int LeaveMinutes,
    int WorkedMinutes,
    int LateMinutes,
    int EarlyLeaveMinutes,
    int OvertimeCandidateMinutes,
    DateTimeOffset? FirstInAt,
    DateTimeOffset? LastOutAt,
    IReadOnlyList<Guid> UsedEventIds,
    string? Message);

public static class DailyAttendanceCalculator
{
    private const int EventWindowMarginMinutes = 360;

    public static DailyAttendanceCalculationResult Calculate(DailyAttendanceCalculationInput input)
    {
        if (!WorkCalendarDayTypes.IsKnown(input.CalendarDayType)) throw new ArgumentException("Calendar day type is invalid.", nameof(input));
        if (input.PlannedMinutes < 0 || input.ShiftBreakMinutes < 0 || input.GraceInMinutes < 0 || input.GraceOutMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(input));
        if (input.LeaveDayFraction < 0 || input.LeaveDayFraction > 1) throw new ArgumentOutOfRangeException(nameof(input));

        var shiftStart = ToMinute(input.AttendanceDate, input.AttendanceDate, input.ShiftStartTime);
        var shiftEndDate = input.ShiftEndTime <= input.ShiftStartTime ? input.AttendanceDate.AddDays(1) : input.AttendanceDate;
        var shiftEnd = ToMinute(input.AttendanceDate, shiftEndDate, input.ShiftEndTime);
        var relevant = input.Punches
            .Select(x => new PunchWithMinute(x, ToMinute(input.AttendanceDate, x.LocalDate, x.LocalTime)))
            .Where(x => x.Minute >= shiftStart - EventWindowMarginMinutes && x.Minute <= shiftEnd + EventWindowMarginMinutes)
            .OrderBy(x => x.Minute)
            .ToArray();

        var firstIn = relevant.FirstOrDefault(x => x.Punch.Direction == RawAttendanceDirections.In);
        var lastOut = firstIn is null
            ? null
            : relevant.LastOrDefault(x => x.Punch.Direction == RawAttendanceDirections.Out && x.Minute >= firstIn.Minute);
        var usedIds = relevant.Select(x => x.Punch.EventId).ToArray();
        var hasCompletePair = firstIn is not null && lastOut is not null;
        var worked = hasCompletePair ? Math.Max(0, lastOut!.Minute - firstIn!.Minute - input.ShiftBreakMinutes) : 0;
        var leaveMinutes = (int)Math.Round(input.PlannedMinutes * input.LeaveDayFraction, MidpointRounding.AwayFromZero);

        if (input.LeaveDayFraction >= 1m)
        {
            var leaveStatus = input.IsSickLeave ? DailyAttendanceStatuses.Sick : DailyAttendanceStatuses.Leave;
            return new DailyAttendanceCalculationResult(
                leaveStatus,
                relevant.Length == 0 ? DailyAttendanceProcessingStatuses.Calculated : DailyAttendanceProcessingStatuses.ReviewRequired,
                input.PlannedMinutes,
                leaveMinutes,
                worked,
                0,
                0,
                0,
                firstIn?.Punch.EventAt,
                lastOut?.Punch.EventAt,
                usedIds,
                relevant.Length == 0 ? null : "Onaylı tam gün izin ile PDKS hareketi çakışıyor; insan kontrolü gerekli.");
        }

        var nonWorkDay = input.CalendarDayType is WorkCalendarDayTypes.Holiday or WorkCalendarDayTypes.Weekend or WorkCalendarDayTypes.OffDay;
        if (nonWorkDay && relevant.Length == 0)
        {
            return new DailyAttendanceCalculationResult(
                input.CalendarDayType == WorkCalendarDayTypes.Holiday ? DailyAttendanceStatuses.Holiday : DailyAttendanceStatuses.OffDay,
                DailyAttendanceProcessingStatuses.Calculated,
                0,
                0,
                0,
                0,
                0,
                0,
                null,
                null,
                usedIds,
                null);
        }

        if (!hasCompletePair)
        {
            var message = input.LeaveDayFraction > 0m
                ? "Kısmi izin gününde gerekli giriş/çıkış çifti bulunamadı."
                : nonWorkDay
                    ? "Çalışılmayan günde tek taraflı veya belirsiz PDKS hareketi var."
                    : relevant.Length == 0
                        ? "Planlı çalışma gününde PDKS hareketi bulunamadı."
                        : "Giriş/çıkış çifti tamamlanmamış.";
            var status = relevant.Length == 0 && input.LeaveDayFraction == 0m && !nonWorkDay
                ? DailyAttendanceStatuses.Absent
                : DailyAttendanceStatuses.MissingRecord;
            return new DailyAttendanceCalculationResult(
                status,
                DailyAttendanceProcessingStatuses.ReviewRequired,
                nonWorkDay ? 0 : input.PlannedMinutes,
                leaveMinutes,
                0,
                0,
                0,
                0,
                firstIn?.Punch.EventAt,
                null,
                usedIds,
                message);
        }

        if (nonWorkDay)
        {
            return new DailyAttendanceCalculationResult(
                DailyAttendanceStatuses.Worked,
                DailyAttendanceProcessingStatuses.ReviewRequired,
                0,
                0,
                worked,
                0,
                0,
                worked,
                firstIn!.Punch.EventAt,
                lastOut!.Punch.EventAt,
                usedIds,
                "Tatil/hafta sonu/off-day çalışması tespit edildi; fazla mesai onayı için kontrol gerekli.");
        }

        if (input.LeaveDayFraction > 0m)
        {
            return new DailyAttendanceCalculationResult(
                DailyAttendanceStatuses.Partial,
                DailyAttendanceProcessingStatuses.ReviewRequired,
                input.PlannedMinutes,
                leaveMinutes,
                worked,
                0,
                0,
                Math.Max(0, worked - Math.Max(0, input.PlannedMinutes - leaveMinutes)),
                firstIn!.Punch.EventAt,
                lastOut!.Punch.EventAt,
                usedIds,
                "Kısmi izin + çalışma günü otomatik hesaplandı; saat sınırları insan kontrolü gerektirir.");
        }

        var late = Math.Max(0, firstIn!.Minute - (shiftStart + input.GraceInMinutes));
        var early = Math.Max(0, (shiftEnd - input.GraceOutMinutes) - lastOut!.Minute);
        var overtimeCandidate = Math.Max(0, worked - input.PlannedMinutes);
        var partial = worked < input.PlannedMinutes;

        return new DailyAttendanceCalculationResult(
            partial ? DailyAttendanceStatuses.Partial : DailyAttendanceStatuses.Worked,
            partial ? DailyAttendanceProcessingStatuses.ReviewRequired : DailyAttendanceProcessingStatuses.Calculated,
            input.PlannedMinutes,
            0,
            worked,
            late,
            early,
            overtimeCandidate,
            firstIn.Punch.EventAt,
            lastOut.Punch.EventAt,
            usedIds,
            partial ? "Çalışılan süre planlanan süreden düşük; kontrol gerekli." : null);
    }

    private static int ToMinute(DateOnly attendanceDate, DateOnly eventDate, TimeOnly time) =>
        checked((eventDate.DayNumber - attendanceDate.DayNumber) * 1440 + time.Hour * 60 + time.Minute);

    private sealed record PunchWithMinute(AttendancePunchPoint Punch, int Minute);
}
