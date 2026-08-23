using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Application.Personnel;
using PersonnelPlatform.Domain.Attendance;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Application.Attendance;

public sealed class OvertimeService(
    IOvertimeRepository repository,
    IAttendanceProcessingRepository attendanceRepository,
    IPersonnelRepository personnelRepository,
    AccessControlService accessControlService,
    TimeProvider timeProvider)
{
    public async Task<AttendanceResult<OvertimeRequestSummary>> CreateAsync(Guid userId, CreateOvertimeRequest request, CancellationToken cancellationToken)
    {
        var daily = await attendanceRepository.FindDailyAsync(Guid.Empty, default, cancellationToken);
        daily = await FindDailyByIdAsync(request.DailyAttendanceId, cancellationToken);
        if (daily is null) return AttendanceResult<OvertimeRequestSummary>.Failure("DAILY_ATTENDANCE_NOT_FOUND", "Günlük puantaj kaydı bulunamadı.");

        var employee = await personnelRepository.FindEmployeeAsync(daily.EmployeeId, cancellationToken);
        if (employee is null) return AttendanceResult<OvertimeRequestSummary>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, employee.CompanyId, cancellationToken))
            return AttendanceResult<OvertimeRequestSummary>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");
        if (daily.OvertimeCandidateMinutes <= 0)
            return AttendanceResult<OvertimeRequestSummary>.Failure("OVERTIME_CANDIDATE_NOT_FOUND", "Bu günlük puantajda fazla mesai adayı dakika bulunmuyor.");
        if (employee.ManagerEmployeeId is null)
            return AttendanceResult<OvertimeRequestSummary>.Failure("OVERTIME_MANAGER_NOT_DEFINED", "Personelin onay yöneticisi tanımlı değil.");
        var manager = await personnelRepository.FindEmployeeAsync(employee.ManagerEmployeeId.Value, cancellationToken);
        if (manager is null || manager.Status != EmployeeStatuses.Active)
            return AttendanceResult<OvertimeRequestSummary>.Failure("OVERTIME_MANAGER_NOT_AVAILABLE", "Personelin aktif onay yöneticisi bulunamadı.");
        if (await repository.FindActiveByDailyAttendanceAsync(daily.Id, cancellationToken) is not null)
            return AttendanceResult<OvertimeRequestSummary>.Failure("OVERTIME_REQUEST_ALREADY_EXISTS", "Bu günlük puantaj için açık veya onaylanmış bir fazla mesai talebi zaten bulunuyor.");

        try
        {
            var row = OvertimeRequest.Create(
                employee.CompanyId,
                employee.Id,
                daily.Id,
                daily.AttendanceDate,
                daily.OvertimeCandidateMinutes,
                request.RequestedMinutes,
                request.Reason,
                timeProvider.GetUtcNow(),
                userId);
            repository.Add(row);
            await repository.SaveChangesAsync(cancellationToken);
            var summary = await repository.GetSummaryAsync(row.Id, cancellationToken);
            return summary is null
                ? AttendanceResult<OvertimeRequestSummary>.Failure("OVERTIME_REQUEST_SAVE_FAILED", "Fazla mesai talebi kaydedildi ancak tekrar okunamadı.")
                : AttendanceResult<OvertimeRequestSummary>.Success(summary);
        }
        catch (ArgumentException)
        {
            return AttendanceResult<OvertimeRequestSummary>.Failure("OVERTIME_REQUEST_INVALID", "Talep edilen fazla mesai dakikası aday dakikadan büyük olamaz ve sıfırdan büyük olmalıdır.");
        }
    }

    public async Task<AttendanceResult<OvertimePagedResult<OvertimeRequestSummary>>> SearchAsync(Guid userId, OvertimeQuery query, CancellationToken cancellationToken)
    {
        var access = await ResolveAccessAsync(userId, cancellationToken);
        if (query.CompanyId is not null && !access.Global && !access.CompanyIds.Contains(query.CompanyId.Value))
            return AttendanceResult<OvertimePagedResult<OvertimeRequestSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var normalized = query with { Page = Math.Max(1, query.Page), PageSize = Math.Clamp(query.PageSize, 1, 100) };
        return AttendanceResult<OvertimePagedResult<OvertimeRequestSummary>>.Success(
            await repository.SearchAsync(normalized, access.Global, access.CompanyIds, cancellationToken));
    }

    public async Task<AttendanceResult<IReadOnlyList<OvertimeInboxItem>>> ListInboxAsync(Guid userId, CancellationToken cancellationToken)
    {
        var canManager = await accessControlService.HasPermissionAsync(userId, OvertimePermissions.ManagerApprove, cancellationToken);
        var canHr = await accessControlService.HasPermissionAsync(userId, OvertimePermissions.HrApprove, cancellationToken);
        if (!canManager && !canHr)
            return AttendanceResult<IReadOnlyList<OvertimeInboxItem>>.Failure("OVERTIME_APPROVAL_PERMISSION_DENIED", "Fazla mesai onay yetkiniz yok.");

        var link = canManager ? await repository.FindUserLinkByUserIdAsync(userId, cancellationToken) : null;
        var access = await ResolveAccessAsync(userId, cancellationToken);
        return AttendanceResult<IReadOnlyList<OvertimeInboxItem>>.Success(
            await repository.ListInboxAsync(access.Global, access.CompanyIds, link?.EmployeeId, canManager, canHr, cancellationToken));
    }

    public async Task<AttendanceResult<OvertimeRequestSummary>> DecideAsync(Guid userId, Guid overtimeId, OvertimeDecisionRequest request, CancellationToken cancellationToken)
    {
        var overtime = await repository.FindAsync(overtimeId, cancellationToken);
        if (overtime is null) return AttendanceResult<OvertimeRequestSummary>.Failure("OVERTIME_REQUEST_NOT_FOUND", "Fazla mesai talebi bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, overtime.CompanyId, cancellationToken))
            return AttendanceResult<OvertimeRequestSummary>.Failure("SCOPE_DENIED", "Fazla mesai talebinin şirket kapsamına erişiminiz yok.");
        if (overtime.Version != request.Version)
            return AttendanceResult<OvertimeRequestSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Fazla mesai talebi başka bir kullanıcı tarafından değiştirildi.");

        try
        {
            var now = timeProvider.GetUtcNow();
            if (overtime.Status == OvertimeRequestStatuses.PendingManager)
            {
                if (!await accessControlService.HasPermissionAsync(userId, OvertimePermissions.ManagerApprove, cancellationToken))
                    return AttendanceResult<OvertimeRequestSummary>.Failure("OVERTIME_MANAGER_APPROVAL_DENIED", "Yönetici fazla mesai onay yetkiniz yok.");
                var link = await repository.FindUserLinkByUserIdAsync(userId, cancellationToken);
                var employee = await personnelRepository.FindEmployeeAsync(overtime.EmployeeId, cancellationToken);
                if (employee is null) return AttendanceResult<OvertimeRequestSummary>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
                if (link is null || employee.ManagerEmployeeId != link.EmployeeId)
                    return AttendanceResult<OvertimeRequestSummary>.Failure("OVERTIME_MANAGER_IDENTITY_MISMATCH", "Bu personelin tanımlı onay yöneticisi değilsiniz.");

                if (request.Approve) overtime.ApproveManager(userId, request.Note, now);
                else overtime.Reject(userId, request.Note, now);
            }
            else if (overtime.Status == OvertimeRequestStatuses.PendingHr)
            {
                if (!await accessControlService.HasPermissionAsync(userId, OvertimePermissions.HrApprove, cancellationToken))
                    return AttendanceResult<OvertimeRequestSummary>.Failure("OVERTIME_HR_APPROVAL_DENIED", "HR fazla mesai nihai onay yetkiniz yok.");
                if (request.Approve)
                {
                    var approvedMinutes = request.ApprovedMinutes ?? overtime.RequestedMinutes;
                    overtime.ApproveHr(userId, approvedMinutes, request.Note, now);
                }
                else overtime.Reject(userId, request.Note, now);
            }
            else
            {
                return AttendanceResult<OvertimeRequestSummary>.Failure("OVERTIME_REQUEST_NOT_PENDING", "Fazla mesai talebi karar beklemiyor.");
            }

            await repository.SaveChangesAsync(cancellationToken);
            var summary = await repository.GetSummaryAsync(overtime.Id, cancellationToken);
            return summary is null
                ? AttendanceResult<OvertimeRequestSummary>.Failure("OVERTIME_REQUEST_NOT_FOUND", "Fazla mesai talebi bulunamadı.")
                : AttendanceResult<OvertimeRequestSummary>.Success(summary);
        }
        catch (ArgumentOutOfRangeException)
        {
            return AttendanceResult<OvertimeRequestSummary>.Failure("OVERTIME_APPROVED_MINUTES_INVALID", "Onaylanan dakika sıfırdan büyük ve talep edilen dakikadan küçük veya eşit olmalıdır.");
        }
        catch (InvalidOperationException)
        {
            return AttendanceResult<OvertimeRequestSummary>.Failure("OVERTIME_STATE_INVALID", "Fazla mesai talebi mevcut durumunda işlenemedi.");
        }
    }

    public async Task<AttendanceResult<OvertimeRequestSummary>> CancelAsync(Guid userId, Guid overtimeId, OvertimeCancelRequest request, CancellationToken cancellationToken)
    {
        var overtime = await repository.FindAsync(overtimeId, cancellationToken);
        if (overtime is null) return AttendanceResult<OvertimeRequestSummary>.Failure("OVERTIME_REQUEST_NOT_FOUND", "Fazla mesai talebi bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, overtime.CompanyId, cancellationToken))
            return AttendanceResult<OvertimeRequestSummary>.Failure("SCOPE_DENIED", "Fazla mesai talebinin şirket kapsamına erişiminiz yok.");
        if (overtime.CreatedBy != userId)
            return AttendanceResult<OvertimeRequestSummary>.Failure("OVERTIME_CANCEL_DENIED", "Yalnız talebi oluşturan kullanıcı yönetici kararı öncesinde iptal edebilir.");
        if (overtime.Version != request.Version)
            return AttendanceResult<OvertimeRequestSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Fazla mesai talebi başka bir kullanıcı tarafından değiştirildi.");
        try
        {
            overtime.Cancel(userId, timeProvider.GetUtcNow());
            await repository.SaveChangesAsync(cancellationToken);
            var summary = await repository.GetSummaryAsync(overtime.Id, cancellationToken);
            return summary is null
                ? AttendanceResult<OvertimeRequestSummary>.Failure("OVERTIME_REQUEST_NOT_FOUND", "Fazla mesai talebi bulunamadı.")
                : AttendanceResult<OvertimeRequestSummary>.Success(summary);
        }
        catch (InvalidOperationException)
        {
            return AttendanceResult<OvertimeRequestSummary>.Failure("OVERTIME_CANCEL_NOT_ALLOWED", "Talep yalnız yönetici kararı öncesinde iptal edilebilir.");
        }
    }

    private async Task<DailyAttendance?> FindDailyByIdAsync(Guid dailyAttendanceId, CancellationToken cancellationToken)
    {
        if (dailyAttendanceId == Guid.Empty) return null;
        return await repositoryDailyBridge(dailyAttendanceId, cancellationToken);
    }

    private async Task<DailyAttendance?> repositoryDailyBridge(Guid dailyAttendanceId, CancellationToken cancellationToken)
    {
        // IAttendanceProcessingRepository intentionally exposes day-key lookup only; locate by candidate id through the overtime repository bridge.
        return await ((IOvertimeDailyAttendanceLookup)repository).FindDailyAttendanceByIdAsync(dailyAttendanceId, cancellationToken);
    }

    private async Task<CompanyAccess> ResolveAccessAsync(Guid userId, CancellationToken cancellationToken)
    {
        var snapshot = await accessControlService.GetSnapshotAsync(userId, cancellationToken);
        return new CompanyAccess(
            snapshot.Scopes.Any(x => x.ScopeType == ScopeTypes.Global),
            snapshot.Scopes.Where(x => x.ScopeType == ScopeTypes.Company && x.ScopeId is not null).Select(x => x.ScopeId!.Value).Distinct().ToArray());
    }

    private sealed record CompanyAccess(bool Global, IReadOnlyCollection<Guid> CompanyIds);
}

public interface IOvertimeDailyAttendanceLookup
{
    Task<DailyAttendance?> FindDailyAttendanceByIdAsync(Guid dailyAttendanceId, CancellationToken cancellationToken);
}
