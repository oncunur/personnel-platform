using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Application.Personnel;
using PersonnelPlatform.Domain.Attendance;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Application.Attendance;

public sealed class AttendanceSetupService(
    IAttendanceSetupRepository repository,
    IPersonnelRepository personnelRepository,
    AccessControlService accessControlService,
    TimeProvider timeProvider)
{
    public async Task<AttendanceResult<IReadOnlyList<WorkCalendarSummary>>> ListCalendarsAsync(Guid userId, Guid? companyId, CancellationToken cancellationToken)
    {
        var access = await ResolveAccessAsync(userId, cancellationToken);
        if (companyId is not null && !access.Global && !access.CompanyIds.Contains(companyId.Value))
            return AttendanceResult<IReadOnlyList<WorkCalendarSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var rows = await repository.ListCalendarsAsync(companyId, access.Global, access.CompanyIds, cancellationToken);
        return AttendanceResult<IReadOnlyList<WorkCalendarSummary>>.Success(rows.Select(ToCalendar).ToArray());
    }

    public async Task<AttendanceResult<WorkCalendarSummary>> CreateCalendarAsync(Guid userId, CreateWorkCalendarRequest request, CancellationToken cancellationToken)
    {
        if (!await CanAccessCompanyAsync(userId, request.CompanyId, cancellationToken))
            return AttendanceResult<WorkCalendarSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var code = request.Code?.Trim().ToUpperInvariant() ?? string.Empty;
        if (await repository.CalendarCodeExistsAsync(request.CompanyId, code, cancellationToken))
            return AttendanceResult<WorkCalendarSummary>.Failure("WORK_CALENDAR_CODE_EXISTS", "Bu çalışma takvimi kodu zaten kullanılıyor.");
        if (request.IsDefault && await repository.HasDefaultCalendarAsync(request.CompanyId, cancellationToken))
            return AttendanceResult<WorkCalendarSummary>.Failure("DEFAULT_WORK_CALENDAR_EXISTS", "Bu şirket için zaten varsayılan çalışma takvimi bulunuyor.");
        try
        {
            var row = WorkCalendar.Create(request.CompanyId, request.Code, request.Name, request.IsDefault, timeProvider.GetUtcNow(), userId);
            repository.AddCalendar(row);
            await repository.SaveChangesAsync(cancellationToken);
            return AttendanceResult<WorkCalendarSummary>.Success(ToCalendar(row));
        }
        catch (ArgumentException)
        {
            return AttendanceResult<WorkCalendarSummary>.Failure("WORK_CALENDAR_INVALID", "Çalışma takvimi bilgileri geçersiz.");
        }
    }

    public async Task<AttendanceResult<IReadOnlyList<WorkCalendarDaySummary>>> ListCalendarDaysAsync(Guid userId, Guid calendarId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var calendar = await repository.FindCalendarAsync(calendarId, cancellationToken);
        if (calendar is null) return AttendanceResult<IReadOnlyList<WorkCalendarDaySummary>>.Failure("WORK_CALENDAR_NOT_FOUND", "Çalışma takvimi bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, calendar.CompanyId, cancellationToken))
            return AttendanceResult<IReadOnlyList<WorkCalendarDaySummary>>.Failure("SCOPE_DENIED", "Çalışma takviminin şirket kapsamına erişiminiz yok.");
        if (from is not null && to is not null && to < from)
            return AttendanceResult<IReadOnlyList<WorkCalendarDaySummary>>.Failure("WORK_CALENDAR_DATE_RANGE_INVALID", "Bitiş tarihi başlangıç tarihinden önce olamaz.");
        var rows = await repository.ListCalendarDaysAsync(calendarId, from, to, cancellationToken);
        return AttendanceResult<IReadOnlyList<WorkCalendarDaySummary>>.Success(rows.Select(ToCalendarDay).ToArray());
    }

    public async Task<AttendanceResult<WorkCalendarDaySummary>> UpsertCalendarDayAsync(Guid userId, Guid calendarId, UpsertWorkCalendarDayRequest request, CancellationToken cancellationToken)
    {
        var calendar = await repository.FindCalendarAsync(calendarId, cancellationToken);
        if (calendar is null) return AttendanceResult<WorkCalendarDaySummary>.Failure("WORK_CALENDAR_NOT_FOUND", "Çalışma takvimi bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, calendar.CompanyId, cancellationToken))
            return AttendanceResult<WorkCalendarDaySummary>.Failure("SCOPE_DENIED", "Çalışma takviminin şirket kapsamına erişiminiz yok.");
        try
        {
            var now = timeProvider.GetUtcNow();
            var day = await repository.FindCalendarDayAsync(calendarId, request.Date, cancellationToken);
            if (day is null)
            {
                day = WorkCalendarDay.Create(calendarId, request.Date, request.DayType, request.PlannedMinutes, request.IsPaid, request.Description, now, userId);
                repository.AddCalendarDay(day);
            }
            else
            {
                day.Update(request.DayType, request.PlannedMinutes, request.IsPaid, request.Description, now, userId);
            }
            await repository.SaveChangesAsync(cancellationToken);
            return AttendanceResult<WorkCalendarDaySummary>.Success(ToCalendarDay(day));
        }
        catch (ArgumentException)
        {
            return AttendanceResult<WorkCalendarDaySummary>.Failure("WORK_CALENDAR_DAY_INVALID", "Takvim günü bilgileri geçersiz.");
        }
    }

    public async Task<AttendanceResult<IReadOnlyList<ShiftSummary>>> ListShiftsAsync(Guid userId, Guid? companyId, CancellationToken cancellationToken)
    {
        var access = await ResolveAccessAsync(userId, cancellationToken);
        if (companyId is not null && !access.Global && !access.CompanyIds.Contains(companyId.Value))
            return AttendanceResult<IReadOnlyList<ShiftSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var rows = await repository.ListShiftsAsync(companyId, access.Global, access.CompanyIds, cancellationToken);
        return AttendanceResult<IReadOnlyList<ShiftSummary>>.Success(rows.Select(ToShift).ToArray());
    }

    public async Task<AttendanceResult<ShiftSummary>> CreateShiftAsync(Guid userId, CreateShiftRequest request, CancellationToken cancellationToken)
    {
        if (!await CanAccessCompanyAsync(userId, request.CompanyId, cancellationToken))
            return AttendanceResult<ShiftSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var code = request.Code?.Trim().ToUpperInvariant() ?? string.Empty;
        if (await repository.ShiftCodeExistsAsync(request.CompanyId, code, cancellationToken))
            return AttendanceResult<ShiftSummary>.Failure("SHIFT_CODE_EXISTS", "Bu vardiya kodu zaten kullanılıyor.");
        try
        {
            var row = ShiftDefinition.Create(request.CompanyId, request.Code, request.Name, request.StartTime, request.EndTime, request.BreakMinutes, request.GraceInMinutes, request.GraceOutMinutes, timeProvider.GetUtcNow(), userId);
            repository.AddShift(row);
            await repository.SaveChangesAsync(cancellationToken);
            return AttendanceResult<ShiftSummary>.Success(ToShift(row));
        }
        catch (ArgumentException)
        {
            return AttendanceResult<ShiftSummary>.Failure("SHIFT_INVALID", "Vardiya bilgileri geçersiz.");
        }
    }

    public async Task<AttendanceResult<IReadOnlyList<EmployeeShiftAssignmentSummary>>> ListAssignmentsAsync(Guid userId, Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await personnelRepository.FindEmployeeAsync(employeeId, cancellationToken);
        if (employee is null) return AttendanceResult<IReadOnlyList<EmployeeShiftAssignmentSummary>>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, employee.CompanyId, cancellationToken))
            return AttendanceResult<IReadOnlyList<EmployeeShiftAssignmentSummary>>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");
        return AttendanceResult<IReadOnlyList<EmployeeShiftAssignmentSummary>>.Success(await repository.ListEmployeeAssignmentsAsync(employeeId, cancellationToken));
    }

    public async Task<AttendanceResult<EmployeeShiftAssignmentSummary>> AssignShiftAsync(Guid userId, Guid employeeId, CreateEmployeeShiftAssignmentRequest request, CancellationToken cancellationToken)
    {
        var employee = await personnelRepository.FindEmployeeAsync(employeeId, cancellationToken);
        if (employee is null) return AttendanceResult<EmployeeShiftAssignmentSummary>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, employee.CompanyId, cancellationToken))
            return AttendanceResult<EmployeeShiftAssignmentSummary>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");
        if (employee.Status != EmployeeStatuses.Active)
            return AttendanceResult<EmployeeShiftAssignmentSummary>.Failure("EMPLOYEE_INACTIVE", "Yalnız aktif personele vardiya atanabilir.");

        var shift = await repository.FindShiftAsync(request.ShiftId, cancellationToken);
        if (shift is null || !shift.IsActive) return AttendanceResult<EmployeeShiftAssignmentSummary>.Failure("SHIFT_NOT_FOUND", "Aktif vardiya bulunamadı.");
        var calendar = await repository.FindCalendarAsync(request.WorkCalendarId, cancellationToken);
        if (calendar is null || !calendar.IsActive) return AttendanceResult<EmployeeShiftAssignmentSummary>.Failure("WORK_CALENDAR_NOT_FOUND", "Aktif çalışma takvimi bulunamadı.");
        if (shift.CompanyId != employee.CompanyId || calendar.CompanyId != employee.CompanyId)
            return AttendanceResult<EmployeeShiftAssignmentSummary>.Failure("ATTENDANCE_COMPANY_MISMATCH", "Vardiya, takvim ve personel aynı şirkete ait olmalıdır.");
        if (await repository.HasAssignmentOverlapAsync(employeeId, request.ValidFrom, request.ValidUntil, cancellationToken))
            return AttendanceResult<EmployeeShiftAssignmentSummary>.Failure("SHIFT_ASSIGNMENT_DATE_CONFLICT", "Personelin bu tarih aralığında başka bir vardiya ataması bulunuyor.");

        try
        {
            var assignment = EmployeeShiftAssignment.Create(employeeId, request.ShiftId, request.WorkCalendarId, request.ValidFrom, request.ValidUntil, request.Note, timeProvider.GetUtcNow(), userId);
            repository.AddAssignment(assignment);
            await repository.SaveChangesAsync(cancellationToken);
            var rows = await repository.ListEmployeeAssignmentsAsync(employeeId, cancellationToken);
            return AttendanceResult<EmployeeShiftAssignmentSummary>.Success(rows.First(x => x.Id == assignment.Id));
        }
        catch (ArgumentException)
        {
            return AttendanceResult<EmployeeShiftAssignmentSummary>.Failure("SHIFT_ASSIGNMENT_INVALID", "Vardiya atama bilgileri geçersiz.");
        }
    }

    private async Task<bool> CanAccessCompanyAsync(Guid userId, Guid companyId, CancellationToken cancellationToken) =>
        await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, companyId, cancellationToken);

    private async Task<CompanyAccess> ResolveAccessAsync(Guid userId, CancellationToken cancellationToken)
    {
        var snapshot = await accessControlService.GetSnapshotAsync(userId, cancellationToken);
        return new CompanyAccess(
            snapshot.Scopes.Any(x => x.ScopeType == ScopeTypes.Global),
            snapshot.Scopes.Where(x => x.ScopeType == ScopeTypes.Company && x.ScopeId is not null).Select(x => x.ScopeId!.Value).Distinct().ToArray());
    }

    private static WorkCalendarSummary ToCalendar(WorkCalendar x) => new(x.Id, x.CompanyId, x.Code, x.Name, x.IsDefault, x.IsActive, x.Version);
    private static WorkCalendarDaySummary ToCalendarDay(WorkCalendarDay x) => new(x.Id, x.WorkCalendarId, x.Date, x.DayType, x.PlannedMinutes, x.IsPaid, x.Description, x.Version);
    private static ShiftSummary ToShift(ShiftDefinition x) => new(x.Id, x.CompanyId, x.Code, x.Name, x.StartTime, x.EndTime, x.BreakMinutes, x.PlannedMinutes, x.GraceInMinutes, x.GraceOutMinutes, x.CrossesMidnight, x.IsActive, x.Version);
    private sealed record CompanyAccess(bool Global, IReadOnlyCollection<Guid> CompanyIds);
}
