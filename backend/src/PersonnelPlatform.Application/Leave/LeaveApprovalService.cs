using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Application.Personnel;
using PersonnelPlatform.Domain.Leave;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Application.Leave;

public sealed class LeaveApprovalService(
    ILeaveApprovalRepository approvalRepository,
    ILeaveRepository leaveRepository,
    IPersonnelRepository personnelRepository,
    AccessControlService accessControlService,
    TimeProvider timeProvider)
{
    public async Task<LeaveResult<IReadOnlyList<EmployeeUserLinkSummary>>> ListApproverLinksAsync(Guid userId, CancellationToken cancellationToken)
    {
        var access = await ResolveAccessAsync(userId, cancellationToken);
        return LeaveResult<IReadOnlyList<EmployeeUserLinkSummary>>.Success(
            await approvalRepository.ListLinksAsync(access.Global, access.CompanyIds, cancellationToken));
    }

    public async Task<LeaveResult<EmployeeUserLinkSummary>> SetApproverLinkAsync(Guid actorUserId, Guid userId, Guid employeeId, CancellationToken cancellationToken)
    {
        if (!await approvalRepository.ActiveUserExistsAsync(userId, cancellationToken))
            return LeaveResult<EmployeeUserLinkSummary>.Failure("USER_NOT_FOUND", "Aktif sistem kullanıcısı bulunamadı.");

        var employee = await personnelRepository.FindEmployeeAsync(employeeId, cancellationToken);
        if (employee is null) return LeaveResult<EmployeeUserLinkSummary>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (employee.Status != EmployeeStatuses.Active) return LeaveResult<EmployeeUserLinkSummary>.Failure("EMPLOYEE_INACTIVE", "Yalnız aktif personel sistem kullanıcısına bağlanabilir.");
        if (!await accessControlService.HasScopeAsync(actorUserId, ScopeTypes.Company, employee.CompanyId, cancellationToken))
            return LeaveResult<EmployeeUserLinkSummary>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");

        var employeeLink = await approvalRepository.FindLinkByEmployeeIdAsync(employeeId, cancellationToken);
        if (employeeLink is not null && employeeLink.UserId != userId)
            return LeaveResult<EmployeeUserLinkSummary>.Failure("EMPLOYEE_ALREADY_LINKED", "Bu personel başka bir aktif sistem kullanıcısına bağlı.");

        var userLink = await approvalRepository.FindLinkByUserIdAsync(userId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (userLink is null)
        {
            userLink = EmployeeUserLink.Create(userId, employeeId, now, actorUserId);
            approvalRepository.AddEmployeeUserLink(userLink);
        }
        else if (userLink.EmployeeId != employeeId)
        {
            userLink.Relink(employeeId, now, actorUserId);
        }

        await approvalRepository.SaveChangesAsync(cancellationToken);
        var access = await ResolveAccessAsync(actorUserId, cancellationToken);
        var summary = (await approvalRepository.ListLinksAsync(access.Global, access.CompanyIds, cancellationToken)).FirstOrDefault(x => x.UserId == userId);
        return summary is null
            ? LeaveResult<EmployeeUserLinkSummary>.Failure("APPROVER_LINK_SAVE_FAILED", "Kullanıcı-personel eşlemesi kaydedilemedi.")
            : LeaveResult<EmployeeUserLinkSummary>.Success(summary);
    }

    public async Task<LeaveResult<IReadOnlyList<LeaveApprovalInboxItem>>> ListInboxAsync(Guid userId, CancellationToken cancellationToken)
    {
        var canManagerApprove = await accessControlService.HasPermissionAsync(userId, LeavePermissions.ManagerApprove, cancellationToken);
        var canHrApprove = await accessControlService.HasPermissionAsync(userId, LeavePermissions.Approve, cancellationToken);
        if (!canManagerApprove && !canHrApprove)
            return LeaveResult<IReadOnlyList<LeaveApprovalInboxItem>>.Failure("LEAVE_APPROVAL_PERMISSION_DENIED", "İzin onay yetkiniz yok.");

        var link = canManagerApprove ? await approvalRepository.FindLinkByUserIdAsync(userId, cancellationToken) : null;
        var access = await ResolveAccessAsync(userId, cancellationToken);
        var rows = await approvalRepository.ListPendingInboxAsync(access.Global, access.CompanyIds, link?.EmployeeId, cancellationToken);
        var filtered = rows
            .Where(x => x.StepCode == LeaveApprovalStepCodes.Manager
                ? canManagerApprove && x.CanDecide
                : x.StepCode == LeaveApprovalStepCodes.Hr && canHrApprove)
            .Select(x => x with { CanDecide = true })
            .ToArray();
        return LeaveResult<IReadOnlyList<LeaveApprovalInboxItem>>.Success(filtered);
    }

    public async Task<LeaveResult<LeaveApprovalWorkflowDetail>> GetWorkflowAsync(Guid userId, Guid leaveId, CancellationToken cancellationToken)
    {
        var leave = await leaveRepository.GetLeaveRequestSummaryAsync(leaveId, cancellationToken);
        if (leave is null) return LeaveResult<LeaveApprovalWorkflowDetail>.Failure("LEAVE_NOT_FOUND", "İzin talebi bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, leave.CompanyId, cancellationToken))
            return LeaveResult<LeaveApprovalWorkflowDetail>.Failure("SCOPE_DENIED", "İzin kaydının şirket kapsamına erişiminiz yok.");

        var steps = await approvalRepository.ListApprovalSummariesAsync(leaveId, cancellationToken);
        var history = await approvalRepository.ListHistoryAsync(leaveId, cancellationToken);
        return LeaveResult<LeaveApprovalWorkflowDetail>.Success(new LeaveApprovalWorkflowDetail(leave, steps, history));
    }

    public async Task<LeaveResult<LeaveApprovalWorkflowDetail>> DecideAsync(Guid userId, Guid leaveId, Guid approvalId, LeaveApprovalDecisionRequest request, CancellationToken cancellationToken)
    {
        var leave = await leaveRepository.FindLeaveRequestAsync(leaveId, cancellationToken);
        if (leave is null) return LeaveResult<LeaveApprovalWorkflowDetail>.Failure("LEAVE_NOT_FOUND", "İzin talebi bulunamadı.");
        var summary = await leaveRepository.GetLeaveRequestSummaryAsync(leaveId, cancellationToken);
        if (summary is null) return LeaveResult<LeaveApprovalWorkflowDetail>.Failure("LEAVE_NOT_FOUND", "İzin talebi bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, summary.CompanyId, cancellationToken))
            return LeaveResult<LeaveApprovalWorkflowDetail>.Failure("SCOPE_DENIED", "İzin kaydının şirket kapsamına erişiminiz yok.");
        if (leave.Version != request.LeaveVersion)
            return LeaveResult<LeaveApprovalWorkflowDetail>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "İzin kaydı başka bir kullanıcı tarafından değiştirildi.");
        if (leave.Status != LeaveRequestStatuses.PendingApproval)
            return LeaveResult<LeaveApprovalWorkflowDetail>.Failure("LEAVE_NOT_PENDING_APPROVAL", "İzin talebi onay beklemiyor.");

        var approval = await approvalRepository.FindApprovalAsync(approvalId, cancellationToken);
        if (approval is null || approval.LeaveId != leaveId)
            return LeaveResult<LeaveApprovalWorkflowDetail>.Failure("LEAVE_APPROVAL_NOT_FOUND", "Onay adımı bulunamadı.");
        if (approval.Version != request.ApprovalVersion)
            return LeaveResult<LeaveApprovalWorkflowDetail>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Onay adımı başka bir kullanıcı tarafından değiştirildi.");
        if (approval.Status != LeaveApprovalStatuses.Pending)
            return LeaveResult<LeaveApprovalWorkflowDetail>.Failure("LEAVE_APPROVAL_NOT_PENDING", "Bu onay adımı karar beklemiyor.");

        var permission = await ValidateDecisionPermissionAsync(userId, approval, cancellationToken);
        if (permission is not null)
            return LeaveResult<LeaveApprovalWorkflowDetail>.Failure(permission.Value.Code, permission.Value.Message);

        var now = timeProvider.GetUtcNow();
        var fromStatus = approval.Status;
        try
        {
            if (request.Approve)
            {
                approval.Approve(userId, request.Note, now);
                approvalRepository.AddHistory(LeaveApprovalHistory.Create(
                    leaveId, approval.Id, LeaveApprovalHistoryActions.StepApproved, approval.StepCode, fromStatus, LeaveApprovalStatuses.Approved, userId, now, request.Note));

                if (approval.StepCode == LeaveApprovalStepCodes.Manager)
                {
                    var hr = await approvalRepository.FindApprovalByStepAsync(leaveId, LeaveApprovalStepCodes.Hr, cancellationToken);
                    if (hr is null) return LeaveResult<LeaveApprovalWorkflowDetail>.Failure("LEAVE_APPROVAL_WORKFLOW_INVALID", "HR onay adımı bulunamadı.");
                    if (hr.Status == LeaveApprovalStatuses.Waiting)
                    {
                        hr.Activate(now, userId);
                        approvalRepository.AddHistory(LeaveApprovalHistory.Create(
                            leaveId, hr.Id, LeaveApprovalHistoryActions.StepActivated, hr.StepCode, LeaveApprovalStatuses.Waiting, LeaveApprovalStatuses.Pending, userId, now));
                    }
                }
                else if (approval.StepCode == LeaveApprovalStepCodes.Hr)
                {
                    var balanceResult = await ConsumeApprovedBalanceAsync(leave, userId, now, cancellationToken);
                    if (balanceResult is not null)
                        return LeaveResult<LeaveApprovalWorkflowDetail>.Failure(balanceResult.Value.Code, balanceResult.Value.Message);
                    leave.Approve(now, userId);
                }
            }
            else
            {
                approval.Reject(userId, request.Note, now);
                approvalRepository.AddHistory(LeaveApprovalHistory.Create(
                    leaveId, approval.Id, LeaveApprovalHistoryActions.StepRejected, approval.StepCode, fromStatus, LeaveApprovalStatuses.Rejected, userId, now, request.Note));

                var releaseResult = await ReleaseReservedBalanceAsync(leave, userId, now, cancellationToken);
                if (releaseResult is not null)
                    return LeaveResult<LeaveApprovalWorkflowDetail>.Failure(releaseResult.Value.Code, releaseResult.Value.Message);

                leave.Reject(now, userId);
                var laterSteps = (await approvalRepository.ListApprovalsAsync(leaveId, cancellationToken))
                    .Where(x => x.StepOrder > approval.StepOrder && !LeaveApprovalStatuses.IsTerminal(x.Status))
                    .ToArray();
                foreach (var later in laterSteps)
                {
                    var previous = later.Status;
                    later.Skip(userId, "Önceki onay adımı reddedildi.", now);
                    approvalRepository.AddHistory(LeaveApprovalHistory.Create(
                        leaveId, later.Id, LeaveApprovalHistoryActions.StepSkipped, later.StepCode, previous, LeaveApprovalStatuses.Skipped, userId, now, "Önceki onay adımı reddedildi."));
                }
            }

            await approvalRepository.SaveChangesAsync(cancellationToken);
            return await GetWorkflowAsync(userId, leaveId, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return LeaveResult<LeaveApprovalWorkflowDetail>.Failure("LEAVE_APPROVAL_STATE_INVALID", "Onay adımı mevcut durumunda işlenemedi.");
        }
    }

    private async Task<(string Code, string Message)?> ValidateDecisionPermissionAsync(Guid userId, LeaveApproval approval, CancellationToken cancellationToken)
    {
        if (approval.StepCode == LeaveApprovalStepCodes.Manager)
        {
            if (!await accessControlService.HasPermissionAsync(userId, LeavePermissions.ManagerApprove, cancellationToken))
                return ("LEAVE_MANAGER_APPROVAL_DENIED", "Yönetici izin onay yetkiniz yok.");
            var link = await approvalRepository.FindLinkByUserIdAsync(userId, cancellationToken);
            if (link is null || approval.ApproverEmployeeId != link.EmployeeId)
                return ("LEAVE_MANAGER_IDENTITY_MISMATCH", "Bu izin talebinin tanımlı yöneticisi değilsiniz.");
            if (approval.AssignedUserId is not null && approval.AssignedUserId != userId)
                return ("LEAVE_APPROVAL_ASSIGNED_TO_ANOTHER_USER", "Bu onay adımı başka bir kullanıcıya atanmış.");
            if (approval.AssignedUserId is null) approval.AssignUser(userId, timeProvider.GetUtcNow(), userId);
            return null;
        }

        if (approval.StepCode == LeaveApprovalStepCodes.Hr)
        {
            if (!await accessControlService.HasPermissionAsync(userId, LeavePermissions.Approve, cancellationToken))
                return ("LEAVE_HR_APPROVAL_DENIED", "HR nihai izin onay yetkiniz yok.");
            return null;
        }

        return ("LEAVE_APPROVAL_STEP_INVALID", "Onay adımı geçersiz.");
    }

    private async Task<(string Code, string Message)?> ConsumeApprovedBalanceAsync(LeaveRequest leave, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var type = await leaveRepository.FindLeaveTypeAsync(leave.LeaveTypeId, cancellationToken);
        if (type is null) return ("LEAVE_TYPE_NOT_FOUND", "İzin türü bulunamadı.");
        if (!type.BalanceRequired) return null;
        var balance = await leaveRepository.FindBalanceForRangeAsync(leave.EmployeeId, leave.LeaveTypeId, leave.StartDate, leave.EndDate, cancellationToken);
        if (balance is null) return ("LEAVE_BALANCE_NOT_FOUND", "Onay için rezerve edilmiş izin bakiyesi bulunamadı.");
        if (balance.ReservedDays < leave.RequestedDays) return ("LEAVE_BALANCE_RESERVATION_INVALID", "İzin bakiye rezervasyonu tutarsız.");
        balance.Consume(leave.RequestedDays, now, userId);
        return null;
    }

    private async Task<(string Code, string Message)?> ReleaseReservedBalanceAsync(LeaveRequest leave, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var type = await leaveRepository.FindLeaveTypeAsync(leave.LeaveTypeId, cancellationToken);
        if (type is null) return ("LEAVE_TYPE_NOT_FOUND", "İzin türü bulunamadı.");
        if (!type.BalanceRequired) return null;
        var balance = await leaveRepository.FindBalanceForRangeAsync(leave.EmployeeId, leave.LeaveTypeId, leave.StartDate, leave.EndDate, cancellationToken);
        if (balance is null) return ("LEAVE_BALANCE_NOT_FOUND", "İzin bakiyesi bulunamadı.");
        balance.Release(leave.RequestedDays, now, userId);
        return null;
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
