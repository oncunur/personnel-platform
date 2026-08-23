using System.Text.Json;
using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Application.Personnel;
using PersonnelPlatform.Domain.Attendance;
using PersonnelPlatform.Domain.Leave;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Application.Attendance;

public sealed class AttendanceProcessingService(
    IAttendanceProcessingRepository repository,
    IPersonnelRepository personnelRepository,
    AccessControlService accessControlService,
    TimeProvider timeProvider)
{
    public async Task<AttendanceResult<RawAttendanceEventSummary>> IngestRawAsync(Guid userId, CreateRawAttendanceEventRequest request, CancellationToken cancellationToken)
    {
        var employee = await personnelRepository.FindEmployeeAsync(request.EmployeeId, cancellationToken);
        if (employee is null) return AttendanceResult<RawAttendanceEventSummary>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (employee.CompanyId != request.CompanyId)
            return AttendanceResult<RawAttendanceEventSummary>.Failure("ATTENDANCE_COMPANY_MISMATCH", "PDKS olayı ile personel aynı şirkete ait olmalıdır.");
        if (!await CanAccessCompanyAsync(userId, employee.CompanyId, cancellationToken))
            return AttendanceResult<RawAttendanceEventSummary>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");

        RawAttendanceEvent rawEvent;
        try
        {
            rawEvent = RawAttendanceEvent.Create(
                request.CompanyId,
                request.EmployeeId,
                request.Source,
                request.Direction,
                request.EventAt,
                request.DeviceCode,
                request.ExternalEventId,
                request.RawPayloadJson,
                timeProvider.GetUtcNow(),
                userId);
        }
        catch (ArgumentException)
        {
            return AttendanceResult<RawAttendanceEventSummary>.Failure("RAW_ATTENDANCE_EVENT_INVALID", "PDKS olay bilgileri geçersiz.");
        }

        if (rawEvent.ExternalEventId is not null)
        {
            var existing = await repository.FindRawByExternalIdAsync(rawEvent.CompanyId, rawEvent.Source, rawEvent.ExternalEventId, cancellationToken);
            if (existing is not null) return AttendanceResult<RawAttendanceEventSummary>.Success(ToRaw(existing));
        }

        repository.AddRawEvent(rawEvent);
        await repository.SaveChangesAsync(cancellationToken);
        return AttendanceResult<RawAttendanceEventSummary>.Success(ToRaw(rawEvent));
    }

    public async Task<AttendanceResult<IReadOnlyList<RawAttendanceEventSummary>>> ListRawAsync(Guid userId, Guid employeeId, DateOnly fromLocalDate, DateOnly toLocalDate, CancellationToken cancellationToken)
    {
        if (toLocalDate < fromLocalDate)
            return AttendanceResult<IReadOnlyList<RawAttendanceEventSummary>>.Failure("ATTENDANCE_DATE_RANGE_INVALID", "Bitiş tarihi başlangıç tarihinden önce olamaz.");
        var employee = await personnelRepository.FindEmployeeAsync(employeeId, cancellationToken);
        if (employee is null) return AttendanceResult<IReadOnlyList<RawAttendanceEventSummary>>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, employee.CompanyId, cancellationToken))
            return AttendanceResult<IReadOnlyList<RawAttendanceEventSummary>>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");
        var rows = await repository.ListRawEventsAsync(employeeId, fromLocalDate, toLocalDate, cancellationToken);
        return AttendanceResult<IReadOnlyList<RawAttendanceEventSummary>>.Success(rows.Select(ToRaw).ToArray());
    }

    public async Task<AttendanceResult<DailyAttendanceSummary>> CalculateDailyAsync(Guid userId, CalculateDailyAttendanceRequest request, CancellationToken cancellationToken)
    {
        var employee = await personnelRepository.FindEmployeeAsync(request.EmployeeId, cancellationToken);
        if (employee is null) return AttendanceResult<DailyAttendanceSummary>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, employee.CompanyId, cancellationToken))
            return AttendanceResult<DailyAttendanceSummary>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");
        if (employee.Status == EmployeeStatuses.Terminated && employee.TerminationDate is not null && request.AttendanceDate > employee.TerminationDate)
            return AttendanceResult<DailyAttendanceSummary>.Failure("ATTENDANCE_DATE_AFTER_TERMINATION", "İşten çıkış tarihinden sonraki gün için puantaj hesaplanamaz.");

        var existing = await repository.FindDailyAsync(request.EmployeeId, request.AttendanceDate, cancellationToken);
        if (existing?.ProcessingStatus is DailyAttendanceProcessingStatuses.Approved or DailyAttendanceProcessingStatuses.Locked)
            return AttendanceResult<DailyAttendanceSummary>.Failure("DAILY_ATTENDANCE_LOCKED", "Onaylanmış veya kilitli günlük puantaj yeniden hesaplanamaz.");

        var schedule = await repository.FindScheduleAsync(request.EmployeeId, request.AttendanceDate, cancellationToken);
        var leave = await repository.FindApprovedLeaveAsync(request.EmployeeId, request.AttendanceDate, cancellationToken);
        var rawEvents = await repository.ListRawEventsAsync(request.EmployeeId, request.AttendanceDate.AddDays(-1), request.AttendanceDate.AddDays(2), cancellationToken);
        var leaveFraction = CalculateLeaveFraction(leave, request.AttendanceDate);
        var isSick = leave?.LeaveTypeCode == "SICK_LEAVE";

        DailyAttendanceCalculationResult calculation;
        WorkCalendarDay? calendarDay = null;
        if (schedule is null)
        {
            var fullLeave = leaveFraction >= 1m;
            calculation = new DailyAttendanceCalculationResult(
                fullLeave ? (isSick ? DailyAttendanceStatuses.Sick : DailyAttendanceStatuses.Leave) : DailyAttendanceStatuses.MissingRecord,
                DailyAttendanceProcessingStatuses.ReviewRequired,
                0,
                0,
                0,
                0,
                0,
                0,
                null,
                null,
                rawEvents.Select(x => x.Id).ToArray(),
                fullLeave
                    ? "Onaylı izin var ancak tarih için vardiya ataması bulunamadı; izin dakika hesabı için kontrol gerekli."
                    : "Tarih için vardiya ataması bulunamadı.");
        }
        else
        {
            calendarDay = await repository.FindCalendarDayAsync(schedule.WorkCalendarId, request.AttendanceDate, cancellationToken);
            var dayType = calendarDay?.DayType ?? DefaultDayType(request.AttendanceDate);
            var plannedMinutes = dayType == WorkCalendarDayTypes.Workday
                ? calendarDay is { PlannedMinutes: > 0 } ? calendarDay.PlannedMinutes : schedule.ShiftPlannedMinutes
                : 0;
            calculation = DailyAttendanceCalculator.Calculate(new DailyAttendanceCalculationInput(
                request.AttendanceDate,
                schedule.ShiftStartTime,
                schedule.ShiftEndTime,
                schedule.BreakMinutes,
                plannedMinutes,
                schedule.GraceInMinutes,
                schedule.GraceOutMinutes,
                dayType,
                leaveFraction,
                isSick,
                rawEvents.Select(x => new AttendancePunchPoint(x.Id, x.LocalDate, x.LocalTime, x.EventAt, x.Direction)).ToArray()));
        }

        var snapshot = JsonSerializer.Serialize(new
        {
            calculatedFor = request.AttendanceDate,
            schedule = schedule is null ? null : new
            {
                schedule.AssignmentId,
                schedule.ShiftId,
                schedule.WorkCalendarId,
                schedule.ShiftStartTime,
                schedule.ShiftEndTime,
                schedule.BreakMinutes,
                schedule.ShiftPlannedMinutes,
                schedule.GraceInMinutes,
                schedule.GraceOutMinutes,
                schedule.CrossesMidnight
            },
            calendarDay = calendarDay is null ? null : new { calendarDay.Id, calendarDay.DayType, calendarDay.PlannedMinutes, calendarDay.IsPaid },
            defaultCalendarRule = calendarDay is null && schedule is not null ? "MONDAY_FRIDAY_WORKDAY" : null,
            leave = leave is null ? null : new { leave.LeaveId, leave.LeaveTypeCode, leave.StartDate, leave.EndDate, leave.StartDayPart, leave.EndDayPart, fraction = leaveFraction },
            rawEventIds = calculation.UsedEventIds
        });

        var now = timeProvider.GetUtcNow();
        if (existing is null)
        {
            existing = DailyAttendance.Create(
                employee.CompanyId,
                employee.Id,
                request.AttendanceDate,
                schedule?.AssignmentId,
                schedule?.ShiftId,
                schedule?.WorkCalendarId,
                leave?.LeaveId,
                calculation.Status,
                calculation.ProcessingStatus,
                calculation.PlannedMinutes,
                calculation.LeaveMinutes,
                calculation.WorkedMinutes,
                calculation.LateMinutes,
                calculation.EarlyLeaveMinutes,
                calculation.OvertimeCandidateMinutes,
                calculation.FirstInAt,
                calculation.LastOutAt,
                snapshot,
                calculation.Message,
                now,
                userId);
            repository.AddDaily(existing);
        }
        else
        {
            existing.Recalculate(
                schedule?.AssignmentId,
                schedule?.ShiftId,
                schedule?.WorkCalendarId,
                leave?.LeaveId,
                calculation.Status,
                calculation.ProcessingStatus,
                calculation.PlannedMinutes,
                calculation.LeaveMinutes,
                calculation.WorkedMinutes,
                calculation.LateMinutes,
                calculation.EarlyLeaveMinutes,
                calculation.OvertimeCandidateMinutes,
                calculation.FirstInAt,
                calculation.LastOutAt,
                snapshot,
                calculation.Message,
                now,
                userId);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return AttendanceResult<DailyAttendanceSummary>.Success(ToDaily(existing));
    }

    public async Task<AttendanceResult<IReadOnlyList<DailyAttendanceSummary>>> ListDailyAsync(Guid userId, Guid employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (to < from)
            return AttendanceResult<IReadOnlyList<DailyAttendanceSummary>>.Failure("ATTENDANCE_DATE_RANGE_INVALID", "Bitiş tarihi başlangıç tarihinden önce olamaz.");
        var employee = await personnelRepository.FindEmployeeAsync(employeeId, cancellationToken);
        if (employee is null) return AttendanceResult<IReadOnlyList<DailyAttendanceSummary>>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, employee.CompanyId, cancellationToken))
            return AttendanceResult<IReadOnlyList<DailyAttendanceSummary>>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");
        var rows = await repository.ListDailyAsync(employeeId, from, to, cancellationToken);
        return AttendanceResult<IReadOnlyList<DailyAttendanceSummary>>.Success(rows.Select(ToDaily).ToArray());
    }

    private async Task<bool> CanAccessCompanyAsync(Guid userId, Guid companyId, CancellationToken cancellationToken) =>
        await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, companyId, cancellationToken);

    private static string DefaultDayType(DateOnly date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? WorkCalendarDayTypes.Weekend : WorkCalendarDayTypes.Workday;

    private static decimal CalculateLeaveFraction(ApprovedLeaveSnapshot? leave, DateOnly attendanceDate)
    {
        if (leave is null || attendanceDate < leave.StartDate || attendanceDate > leave.EndDate) return 0m;
        if (leave.StartDate == leave.EndDate)
            return leave.StartDayPart == LeaveDayParts.FullDay && leave.EndDayPart == LeaveDayParts.FullDay ? 1m : 0.5m;
        if (attendanceDate == leave.StartDate && leave.StartDayPart != LeaveDayParts.FullDay) return 0.5m;
        if (attendanceDate == leave.EndDate && leave.EndDayPart != LeaveDayParts.FullDay) return 0.5m;
        return 1m;
    }

    private static RawAttendanceEventSummary ToRaw(RawAttendanceEvent x) => new(
        x.Id, x.CompanyId, x.EmployeeId, x.Source, x.Direction, x.EventAt, x.LocalDate, x.LocalTime, x.UtcOffsetMinutes, x.DeviceCode, x.ExternalEventId, x.ReceivedAt);

    private static DailyAttendanceSummary ToDaily(DailyAttendance x) => new(
        x.Id,
        x.CompanyId,
        x.EmployeeId,
        x.AttendanceDate,
        x.ShiftAssignmentId,
        x.ShiftId,
        x.WorkCalendarId,
        x.LeaveId,
        x.Status,
        x.ProcessingStatus,
        x.PlannedMinutes,
        x.LeaveMinutes,
        x.WorkedMinutes,
        x.LateMinutes,
        x.EarlyLeaveMinutes,
        x.OvertimeCandidateMinutes,
        x.FirstInAt,
        x.LastOutAt,
        x.CalculationMessage,
        x.CalculatedAt,
        x.Version);
}
