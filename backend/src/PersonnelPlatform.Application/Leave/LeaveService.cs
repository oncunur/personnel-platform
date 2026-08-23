using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Application.Personnel;
using PersonnelPlatform.Domain.Leave;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Application.Leave;

public sealed class LeaveService(
    ILeaveRepository leaveRepository,
    IPersonnelRepository personnelRepository,
    AccessControlService accessControlService,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<LeaveTypeSummary>> ListTypesAsync(CancellationToken cancellationToken)
    {
        var rows = await leaveRepository.ListLeaveTypesAsync(cancellationToken);
        return rows.Select(ToTypeSummary).ToArray();
    }

    public async Task<LeaveResult<LeaveTypeSummary>> CreateTypeAsync(Guid userId, CreateLeaveTypeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
            return LeaveResult<LeaveTypeSummary>.Failure("LEAVE_TYPE_DATA_INVALID", "İzin türü kodu ve adı zorunludur.");
        var code = request.Code.Trim().ToUpperInvariant();
        if (await leaveRepository.LeaveTypeCodeExistsAsync(code, cancellationToken))
            return LeaveResult<LeaveTypeSummary>.Failure("LEAVE_TYPE_ALREADY_EXISTS", "Bu izin türü kodu zaten kullanılıyor.");

        try
        {
            var type = LeaveType.Create(code, request.Name, request.Description, request.IsPaid, request.BalanceRequired, request.AllowHalfDay, request.AttachmentRequired, request.DisplayOrder, timeProvider.GetUtcNow(), userId);
            leaveRepository.AddLeaveType(type);
            await leaveRepository.SaveChangesAsync(cancellationToken);
            return LeaveResult<LeaveTypeSummary>.Success(ToTypeSummary(type));
        }
        catch (ArgumentException)
        {
            return LeaveResult<LeaveTypeSummary>.Failure("LEAVE_TYPE_DATA_INVALID", "İzin türü bilgileri geçersiz.");
        }
    }

    public async Task<LeaveResult<LeavePagedResult<LeaveRequestSummary>>> SearchAsync(Guid userId, LeaveQuery query, CancellationToken cancellationToken)
    {
        var access = await ResolveAccessAsync(userId, cancellationToken);
        if (query.CompanyId is not null && !access.Global && !access.CompanyIds.Contains(query.CompanyId.Value))
            return LeaveResult<LeavePagedResult<LeaveRequestSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");

        if (query.EmployeeId is not null)
        {
            var employee = await personnelRepository.FindEmployeeAsync(query.EmployeeId.Value, cancellationToken);
            if (employee is null) return LeaveResult<LeavePagedResult<LeaveRequestSummary>>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
            if (!await CanAccessCompanyAsync(userId, employee.CompanyId, cancellationToken)) return LeaveResult<LeavePagedResult<LeaveRequestSummary>>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");
        }

        var normalized = query with
        {
            Status = string.IsNullOrWhiteSpace(query.Status) ? null : query.Status.Trim().ToUpperInvariant(),
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize, 1, 100)
        };
        return LeaveResult<LeavePagedResult<LeaveRequestSummary>>.Success(await leaveRepository.SearchLeaveRequestsAsync(normalized, access.Global, access.CompanyIds, cancellationToken));
    }

    public async Task<LeaveResult<LeaveRequestSummary>> GetAsync(Guid userId, Guid leaveId, CancellationToken cancellationToken)
    {
        var summary = await leaveRepository.GetLeaveRequestSummaryAsync(leaveId, cancellationToken);
        if (summary is null) return LeaveResult<LeaveRequestSummary>.Failure("LEAVE_NOT_FOUND", "İzin talebi bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, summary.CompanyId, cancellationToken)) return LeaveResult<LeaveRequestSummary>.Failure("SCOPE_DENIED", "İzin kaydının şirket kapsamına erişiminiz yok.");
        return LeaveResult<LeaveRequestSummary>.Success(summary);
    }

    public async Task<LeaveResult<LeaveRequestSummary>> CreateDraftAsync(Guid userId, CreateLeaveRequest request, CancellationToken cancellationToken)
    {
        var employee = await personnelRepository.FindEmployeeAsync(request.EmployeeId, cancellationToken);
        if (employee is null) return LeaveResult<LeaveRequestSummary>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, employee.CompanyId, cancellationToken)) return LeaveResult<LeaveRequestSummary>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");
        if (employee.Status != EmployeeStatuses.Active) return LeaveResult<LeaveRequestSummary>.Failure("EMPLOYEE_INACTIVE", "Yalnız aktif personel için izin talebi oluşturulabilir.");

        var type = await leaveRepository.FindLeaveTypeAsync(request.LeaveTypeId, cancellationToken);
        if (type is null) return LeaveResult<LeaveRequestSummary>.Failure("LEAVE_TYPE_NOT_FOUND", "İzin türü bulunamadı.");
        if (!type.IsActive) return LeaveResult<LeaveRequestSummary>.Failure("LEAVE_TYPE_INACTIVE", "İzin türü pasif.");
        var validation = ValidateRequestDates(type, request.StartDate, request.EndDate, request.StartDayPart, request.EndDayPart);
        if (validation is not null) return LeaveResult<LeaveRequestSummary>.Failure(validation.Value.Code, validation.Value.Message);

        try
        {
            var requestedDays = CalculateRequestedDays(request.StartDate, request.EndDate, request.StartDayPart, request.EndDayPart);
            var leave = LeaveRequest.CreateDraft(request.EmployeeId, request.LeaveTypeId, request.StartDate, request.EndDate, request.StartDayPart, request.EndDayPart, requestedDays, request.Reason, timeProvider.GetUtcNow(), userId);
            leaveRepository.AddLeaveRequest(leave);
            await leaveRepository.SaveChangesAsync(cancellationToken);
            return await GetAsync(userId, leave.Id, cancellationToken);
        }
        catch (ArgumentException)
        {
            return LeaveResult<LeaveRequestSummary>.Failure("LEAVE_DATA_INVALID", "İzin talebi bilgileri geçersiz.");
        }
    }

    public async Task<LeaveResult<LeaveRequestSummary>> SubmitAsync(Guid userId, Guid leaveId, int version, CancellationToken cancellationToken)
    {
        var leave = await leaveRepository.FindLeaveRequestAsync(leaveId, cancellationToken);
        if (leave is null) return LeaveResult<LeaveRequestSummary>.Failure("LEAVE_NOT_FOUND", "İzin talebi bulunamadı.");
        var employee = await personnelRepository.FindEmployeeAsync(leave.EmployeeId, cancellationToken);
        if (employee is null) return LeaveResult<LeaveRequestSummary>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, employee.CompanyId, cancellationToken)) return LeaveResult<LeaveRequestSummary>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");
        if (leave.Version != version) return LeaveResult<LeaveRequestSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Kayıt başka bir kullanıcı tarafından değiştirildi.");
        if (leave.Status != LeaveRequestStatuses.Draft) return LeaveResult<LeaveRequestSummary>.Failure("LEAVE_SUBMIT_NOT_ALLOWED", "Yalnız taslak izin talebi gönderilebilir.");
        if (await leaveRepository.HasBlockingOverlapAsync(leave.EmployeeId, leave.StartDate, leave.EndDate, leave.Id, cancellationToken))
            return LeaveResult<LeaveRequestSummary>.Failure("LEAVE_DATE_CONFLICT", "Aynı tarihlerde çakışan gönderilmiş veya onaylanmış izin bulunuyor.");

        var type = await leaveRepository.FindLeaveTypeAsync(leave.LeaveTypeId, cancellationToken);
        if (type is null || !type.IsActive) return LeaveResult<LeaveRequestSummary>.Failure("LEAVE_TYPE_INACTIVE", "İzin türü bulunamadı veya pasif.");

        LeaveBalance? balance = null;
        if (type.BalanceRequired)
        {
            balance = await leaveRepository.FindBalanceForRangeAsync(leave.EmployeeId, leave.LeaveTypeId, leave.StartDate, leave.EndDate, cancellationToken);
            if (balance is null) return LeaveResult<LeaveRequestSummary>.Failure("LEAVE_BALANCE_NOT_FOUND", "Bu izin dönemi için tanımlı bakiye bulunmuyor.");
            if (balance.AvailableDays < leave.RequestedDays) return LeaveResult<LeaveRequestSummary>.Failure("LEAVE_BALANCE_INSUFFICIENT", $"Yetersiz izin bakiyesi. Kullanılabilir: {balance.AvailableDays:0.##} gün.");
        }

        try
        {
            var now = timeProvider.GetUtcNow();
            balance?.Reserve(leave.RequestedDays, now, userId);
            leave.Submit(now, userId);
            await leaveRepository.SaveChangesAsync(cancellationToken);
            return await GetAsync(userId, leave.Id, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return LeaveResult<LeaveRequestSummary>.Failure("LEAVE_SUBMIT_NOT_ALLOWED", "İzin talebi gönderilemedi.");
        }
    }

    public async Task<LeaveResult<LeaveRequestSummary>> WithdrawAsync(Guid userId, Guid leaveId, int version, CancellationToken cancellationToken)
    {
        var leave = await leaveRepository.FindLeaveRequestAsync(leaveId, cancellationToken);
        if (leave is null) return LeaveResult<LeaveRequestSummary>.Failure("LEAVE_NOT_FOUND", "İzin talebi bulunamadı.");
        var employee = await personnelRepository.FindEmployeeAsync(leave.EmployeeId, cancellationToken);
        if (employee is null) return LeaveResult<LeaveRequestSummary>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, employee.CompanyId, cancellationToken)) return LeaveResult<LeaveRequestSummary>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");
        if (leave.Version != version) return LeaveResult<LeaveRequestSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Kayıt başka bir kullanıcı tarafından değiştirildi.");

        var wasReserved = leave.Status is LeaveRequestStatuses.Submitted or LeaveRequestStatuses.PendingApproval;
        try
        {
            var now = timeProvider.GetUtcNow();
            if (wasReserved)
            {
                var type = await leaveRepository.FindLeaveTypeAsync(leave.LeaveTypeId, cancellationToken);
                if (type?.BalanceRequired == true)
                {
                    var balance = await leaveRepository.FindBalanceForRangeAsync(leave.EmployeeId, leave.LeaveTypeId, leave.StartDate, leave.EndDate, cancellationToken);
                    balance?.Release(leave.RequestedDays, now, userId);
                }
            }
            leave.Withdraw(now, userId);
            await leaveRepository.SaveChangesAsync(cancellationToken);
            return await GetAsync(userId, leave.Id, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return LeaveResult<LeaveRequestSummary>.Failure("LEAVE_WITHDRAW_NOT_ALLOWED", "İzin talebi mevcut durumunda geri çekilemez.");
        }
    }

    public async Task<LeaveResult<IReadOnlyList<LeaveBalanceSummary>>> ListBalancesAsync(Guid userId, Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await personnelRepository.FindEmployeeAsync(employeeId, cancellationToken);
        if (employee is null) return LeaveResult<IReadOnlyList<LeaveBalanceSummary>>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, employee.CompanyId, cancellationToken)) return LeaveResult<IReadOnlyList<LeaveBalanceSummary>>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");

        var types = (await leaveRepository.ListLeaveTypesAsync(cancellationToken)).ToDictionary(x => x.Id);
        var balances = await leaveRepository.ListBalancesAsync(employeeId, cancellationToken);
        return LeaveResult<IReadOnlyList<LeaveBalanceSummary>>.Success(balances
            .Where(x => types.ContainsKey(x.LeaveTypeId))
            .Select(x => ToBalanceSummary(x, types[x.LeaveTypeId]))
            .OrderByDescending(x => x.PeriodStart)
            .ThenBy(x => x.LeaveTypeName)
            .ToArray());
    }

    public async Task<LeaveResult<LeaveBalanceSummary>> UpsertEntitlementAsync(Guid userId, Guid employeeId, UpsertLeaveEntitlementRequest request, CancellationToken cancellationToken)
    {
        var employee = await personnelRepository.FindEmployeeAsync(employeeId, cancellationToken);
        if (employee is null) return LeaveResult<LeaveBalanceSummary>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, employee.CompanyId, cancellationToken)) return LeaveResult<LeaveBalanceSummary>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");
        var type = await leaveRepository.FindLeaveTypeAsync(request.LeaveTypeId, cancellationToken);
        if (type is null) return LeaveResult<LeaveBalanceSummary>.Failure("LEAVE_TYPE_NOT_FOUND", "İzin türü bulunamadı.");
        if (!type.BalanceRequired) return LeaveResult<LeaveBalanceSummary>.Failure("LEAVE_BALANCE_NOT_REQUIRED", "Bu izin türü bakiye takibi gerektirmiyor.");
        if (request.PeriodEnd < request.PeriodStart || request.EntitledDays < 0 || request.CarryOverDays < 0)
            return LeaveResult<LeaveBalanceSummary>.Failure("LEAVE_ENTITLEMENT_INVALID", "İzin hakediş bilgileri geçersiz.");

        var existing = await leaveRepository.FindEntitlementExactAsync(employeeId, request.LeaveTypeId, request.PeriodStart, request.PeriodEnd, cancellationToken);
        if (await leaveRepository.HasEntitlementPeriodOverlapAsync(employeeId, request.LeaveTypeId, request.PeriodStart, request.PeriodEnd, existing?.Id, cancellationToken))
            return LeaveResult<LeaveBalanceSummary>.Failure("LEAVE_ENTITLEMENT_PERIOD_CONFLICT", "Bu izin türü için çakışan bir hakediş dönemi bulunuyor.");

        try
        {
            var now = timeProvider.GetUtcNow();
            LeaveEntitlement entitlement;
            if (existing is null)
            {
                entitlement = LeaveEntitlement.Create(employeeId, request.LeaveTypeId, request.PeriodStart, request.PeriodEnd, request.EntitledDays, request.CarryOverDays, request.AdjustmentDays, request.Note, now, userId);
                leaveRepository.AddLeaveEntitlement(entitlement);
            }
            else
            {
                entitlement = existing;
                entitlement.Update(request.EntitledDays, request.CarryOverDays, request.AdjustmentDays, request.Note, now, userId);
            }

            var balance = await leaveRepository.FindBalanceExactAsync(employeeId, request.LeaveTypeId, request.PeriodStart, request.PeriodEnd, cancellationToken);
            if (balance is null)
            {
                balance = LeaveBalance.CreateFromEntitlement(entitlement, now, userId);
                leaveRepository.AddLeaveBalance(balance);
            }
            else
            {
                balance.SyncEntitlement(entitlement, now, userId);
            }

            await leaveRepository.SaveChangesAsync(cancellationToken);
            return LeaveResult<LeaveBalanceSummary>.Success(ToBalanceSummary(balance, type));
        }
        catch (ArgumentException)
        {
            return LeaveResult<LeaveBalanceSummary>.Failure("LEAVE_ENTITLEMENT_INVALID", "İzin hakediş bilgileri geçersiz.");
        }
    }

    public static decimal CalculateRequestedDays(DateOnly startDate, DateOnly endDate, string startDayPart, string endDayPart)
    {
        if (endDate < startDate) throw new ArgumentException("Leave date range is invalid.");
        if (!LeaveDayParts.IsKnown(startDayPart) || !LeaveDayParts.IsKnown(endDayPart)) throw new ArgumentException("Leave day part is invalid.");
        var days = endDate.DayNumber - startDate.DayNumber + 1m;
        if (startDayPart != LeaveDayParts.FullDay) days -= 0.5m;
        if (endDate != startDate && endDayPart != LeaveDayParts.FullDay) days -= 0.5m;
        if (endDate == startDate && startDayPart != LeaveDayParts.FullDay) days = 0.5m;
        return days;
    }

    private static (string Code, string Message)? ValidateRequestDates(LeaveType type, DateOnly startDate, DateOnly endDate, string startDayPart, string endDayPart)
    {
        if (endDate < startDate) return ("LEAVE_DATE_INVALID", "İzin bitiş tarihi başlangıç tarihinden önce olamaz.");
        if (!LeaveDayParts.IsKnown(startDayPart) || !LeaveDayParts.IsKnown(endDayPart)) return ("LEAVE_DAY_PART_INVALID", "İzin gün bölümü geçersiz.");
        if (!type.AllowHalfDay && (startDayPart != LeaveDayParts.FullDay || endDayPart != LeaveDayParts.FullDay)) return ("LEAVE_HALF_DAY_NOT_ALLOWED", "Bu izin türünde yarım gün kullanılamaz.");
        if (startDate == endDate && startDayPart != endDayPart) return ("LEAVE_DAY_PART_INVALID", "Tek günlük izinlerde başlangıç ve bitiş gün bölümü aynı olmalıdır.");
        return null;
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

    private static LeaveTypeSummary ToTypeSummary(LeaveType x) => new(x.Id, x.Code, x.Name, x.Description, x.IsPaid, x.BalanceRequired, x.AllowHalfDay, x.AttachmentRequired, x.IsActive, x.DisplayOrder);
    private static LeaveBalanceSummary ToBalanceSummary(LeaveBalance x, LeaveType type) => new(x.Id, x.EmployeeId, x.LeaveTypeId, type.Code, type.Name, x.PeriodStart, x.PeriodEnd, x.EntitledDays, x.CarryOverDays, x.AdjustmentDays, x.ReservedDays, x.UsedDays, x.AvailableDays, x.Version);
    private sealed record CompanyAccess(bool Global, IReadOnlyCollection<Guid> CompanyIds);
}
