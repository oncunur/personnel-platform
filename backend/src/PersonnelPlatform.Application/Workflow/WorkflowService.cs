using System.Text.Json;
using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Domain.Workflow;

namespace PersonnelPlatform.Application.Workflow;

public sealed class WorkflowService(
    IWorkflowRepository repository,
    AccessControlService accessControlService,
    WorkflowSlaProcessor slaProcessor,
    TimeProvider timeProvider)
{
    public async Task<WorkflowResult<IReadOnlyList<WorkflowRequestTypeSummary>>> ListRequestTypesAsync(Guid userId, Guid? companyId, bool? active, CancellationToken ct)
    {
        var access = await ResolveAccessAsync(userId, ct);
        if (companyId is not null && !access.Global && !access.CompanyIds.Contains(companyId.Value)) return WorkflowResult<IReadOnlyList<WorkflowRequestTypeSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return WorkflowResult<IReadOnlyList<WorkflowRequestTypeSummary>>.Success(await repository.ListRequestTypesAsync(access.Global, access.CompanyIds, companyId, active, ct));
    }

    public async Task<WorkflowResult<WorkflowRequestTypeSummary>> CreateRequestTypeAsync(Guid userId, CreateWorkflowRequestTypeRequest request, CancellationToken ct)
    {
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, request.CompanyId, ct)) return WorkflowResult<WorkflowRequestTypeSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (!TryValidateRequiredFields(request.RequiredFieldsJson, out var normalizedFields)) return WorkflowResult<WorkflowRequestTypeSummary>.Failure("WORKFLOW_REQUIRED_FIELDS_INVALID", "Zorunlu alan tanımı JSON string dizisi olmalıdır.");
        try
        {
            var row = WorkflowRequestType.Create(request.CompanyId, request.Code, request.Name, request.Description, request.SlaMinutes, normalizedFields, timeProvider.GetUtcNow(), userId);
            repository.AddRequestType(row); await repository.SaveChangesAsync(ct);
            return WorkflowResult<WorkflowRequestTypeSummary>.Success(Map(row));
        }
        catch (ArgumentException) { return WorkflowResult<WorkflowRequestTypeSummary>.Failure("WORKFLOW_REQUEST_TYPE_INVALID", "Talep türü bilgileri geçersiz."); }
    }

    public async Task<WorkflowResult<WorkflowRequestTypeSummary>> UpdateRequestTypeAsync(Guid userId, Guid requestTypeId, UpdateWorkflowRequestTypeRequest request, CancellationToken ct)
    {
        var row = await repository.FindRequestTypeAsync(requestTypeId, ct); if (row is null) return WorkflowResult<WorkflowRequestTypeSummary>.Failure("WORKFLOW_REQUEST_TYPE_NOT_FOUND", "Talep türü bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, row.CompanyId, ct)) return WorkflowResult<WorkflowRequestTypeSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (row.Version != request.Version) return WorkflowResult<WorkflowRequestTypeSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Talep türü başka bir işlem tarafından değiştirildi. Veriyi yenileyin.");
        if (!TryValidateRequiredFields(request.RequiredFieldsJson, out var normalizedFields)) return WorkflowResult<WorkflowRequestTypeSummary>.Failure("WORKFLOW_REQUIRED_FIELDS_INVALID", "Zorunlu alan tanımı JSON string dizisi olmalıdır.");
        try
        {
            var now = timeProvider.GetUtcNow(); row.Update(request.Name, request.Description, request.SlaMinutes, normalizedFields, now, userId); row.SetActive(request.IsActive, now, userId); await repository.SaveChangesAsync(ct);
            return WorkflowResult<WorkflowRequestTypeSummary>.Success(Map(row));
        }
        catch (ArgumentException) { return WorkflowResult<WorkflowRequestTypeSummary>.Failure("WORKFLOW_REQUEST_TYPE_INVALID", "Talep türü bilgileri geçersiz."); }
    }

    public async Task<WorkflowResult<IReadOnlyList<WorkflowApprovalStepSummary>>> ListStepsAsync(Guid userId, Guid requestTypeId, CancellationToken ct)
    {
        var type = await repository.FindRequestTypeAsync(requestTypeId, ct); if (type is null) return WorkflowResult<IReadOnlyList<WorkflowApprovalStepSummary>>.Failure("WORKFLOW_REQUEST_TYPE_NOT_FOUND", "Talep türü bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, type.CompanyId, ct)) return WorkflowResult<IReadOnlyList<WorkflowApprovalStepSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return WorkflowResult<IReadOnlyList<WorkflowApprovalStepSummary>>.Success(await repository.ListStepSummariesAsync(requestTypeId, ct));
    }

    public async Task<WorkflowResult<IReadOnlyList<WorkflowApprovalStepSummary>>> ReplaceStepsAsync(Guid userId, Guid requestTypeId, ReplaceWorkflowApprovalStepsRequest request, CancellationToken ct)
    {
        var type = await repository.FindRequestTypeAsync(requestTypeId, ct); if (type is null) return WorkflowResult<IReadOnlyList<WorkflowApprovalStepSummary>>.Failure("WORKFLOW_REQUEST_TYPE_NOT_FOUND", "Talep türü bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, type.CompanyId, ct)) return WorkflowResult<IReadOnlyList<WorkflowApprovalStepSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (type.Version != request.RequestTypeVersion) return WorkflowResult<IReadOnlyList<WorkflowApprovalStepSummary>>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Talep türü workflow tanımı başka bir işlem tarafından değiştirildi. Veriyi yenileyin.");
        var ordered = request.Steps.OrderBy(x => x.StepOrder).ToArray();
        if (ordered.Select(x => x.StepOrder).Distinct().Count() != ordered.Length || ordered.Where((x, i) => x.StepOrder != i + 1).Any()) return WorkflowResult<IReadOnlyList<WorkflowApprovalStepSummary>>.Failure("WORKFLOW_STEP_ORDER_INVALID", "Onay adımları 1'den başlayan kesintisiz sıra olmalıdır.");

        foreach (var step in ordered)
        {
            var kind = step.TargetKind.Trim().ToUpperInvariant();
            if (kind == ApprovalTargetKinds.User)
            {
                if (step.ApproverUserId is null || step.ApproverRoleId is not null) return WorkflowResult<IReadOnlyList<WorkflowApprovalStepSummary>>.Failure("WORKFLOW_APPROVER_INVALID", "USER adımı yalnız kullanıcı hedefi içermelidir.");
                var user = await repository.FindUserAsync(step.ApproverUserId.Value, ct); if (user is null || !user.IsActive) return WorkflowResult<IReadOnlyList<WorkflowApprovalStepSummary>>.Failure("WORKFLOW_APPROVER_USER_NOT_FOUND", "Aktif onay kullanıcısı bulunamadı.");
            }
            else if (kind == ApprovalTargetKinds.Role)
            {
                if (step.ApproverRoleId is null || step.ApproverUserId is not null) return WorkflowResult<IReadOnlyList<WorkflowApprovalStepSummary>>.Failure("WORKFLOW_APPROVER_INVALID", "ROLE adımı yalnız rol hedefi içermelidir.");
                var role = await repository.FindRoleAsync(step.ApproverRoleId.Value, ct); if (role is null || !role.IsActive) return WorkflowResult<IReadOnlyList<WorkflowApprovalStepSummary>>.Failure("WORKFLOW_APPROVER_ROLE_NOT_FOUND", "Aktif onay rolü bulunamadı.");
            }
            else return WorkflowResult<IReadOnlyList<WorkflowApprovalStepSummary>>.Failure("WORKFLOW_APPROVER_INVALID", "Onay hedef türü USER veya ROLE olmalıdır.");
        }

        try
        {
            var old = await repository.ListStepDefinitionsAsync(requestTypeId, ct); repository.RemoveStepDefinitions(old);
            var now = timeProvider.GetUtcNow();
            foreach (var step in ordered) repository.AddStepDefinition(WorkflowApprovalStepDefinition.Create(type.CompanyId, type.Id, step.StepOrder, step.Name, step.TargetKind, step.ApproverUserId, step.ApproverRoleId, now, userId));
            type.Update(type.Name, type.Description, type.SlaMinutes, type.RequiredFieldsJson, now, userId);
            await repository.SaveChangesAsync(ct);
            return WorkflowResult<IReadOnlyList<WorkflowApprovalStepSummary>>.Success(await repository.ListStepSummariesAsync(requestTypeId, ct));
        }
        catch (ArgumentException) { return WorkflowResult<IReadOnlyList<WorkflowApprovalStepSummary>>.Failure("WORKFLOW_STEP_INVALID", "Onay adımı bilgileri geçersiz."); }
    }

    public async Task<WorkflowResult<WorkflowRequestSummary>> CreateRequestAsync(Guid userId, CreateWorkflowRequestRequest request, CancellationToken ct)
    {
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, request.CompanyId, ct)) return WorkflowResult<WorkflowRequestSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var type = await repository.FindRequestTypeAsync(request.RequestTypeId, ct); if (type is null || type.CompanyId != request.CompanyId) return WorkflowResult<WorkflowRequestSummary>.Failure("WORKFLOW_REQUEST_TYPE_NOT_FOUND", "Talep türü bulunamadı.");
        if (!type.IsActive) return WorkflowResult<WorkflowRequestSummary>.Failure("WORKFLOW_REQUEST_TYPE_INACTIVE", "Pasif talep türüyle talep oluşturulamaz.");
        if (!TryValidatePayload(request.RequestDataJson, type.RequiredFieldsJson, out var normalizedPayload, out var missing)) return WorkflowResult<WorkflowRequestSummary>.Failure("WORKFLOW_PAYLOAD_INVALID", missing is null ? "Talep verisi geçerli bir JSON nesnesi olmalıdır." : $"Zorunlu alan eksik: {missing}.");
        if (request.EmployeeId is not null)
        {
            var employee = await repository.FindEmployeeAsync(request.EmployeeId.Value, ct); if (employee is null || employee.CompanyId != request.CompanyId) return WorkflowResult<WorkflowRequestSummary>.Failure("EMPLOYEE_NOT_FOUND", "Şirket kapsamındaki personel bulunamadı.");
        }
        try
        {
            var now = timeProvider.GetUtcNow(); var requestNo = await repository.NextRequestNoAsync(request.CompanyId, now.Year, ct);
            var row = WorkflowRequest.Create(request.CompanyId, requestNo, type.Id, userId, request.EmployeeId, request.Priority, normalizedPayload, now);
            repository.AddRequest(row); repository.AddHistory(WorkflowRequestHistory.Create(row.CompanyId, row.Id, "REQUEST_CREATED", null, row.Status, userId, now, "{}")); await repository.SaveChangesAsync(ct);
            return WorkflowResult<WorkflowRequestSummary>.Success((await repository.ListRequestsAsync(true, [], row.CompanyId, null, null, null, 500, ct)).Single(x => x.Id == row.Id));
        }
        catch (ArgumentException) { return WorkflowResult<WorkflowRequestSummary>.Failure("WORKFLOW_REQUEST_INVALID", "Talep bilgileri geçersiz."); }
    }

    public async Task<WorkflowResult<WorkflowRequestDetail>> GetRequestAsync(Guid userId, Guid requestId, CancellationToken ct)
    {
        var row = await repository.FindRequestAsync(requestId, ct); if (row is null) return WorkflowResult<WorkflowRequestDetail>.Failure("WORKFLOW_REQUEST_NOT_FOUND", "Talep bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, row.CompanyId, ct)) return WorkflowResult<WorkflowRequestDetail>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var summary = (await repository.ListRequestsAsync(true, [], row.CompanyId, null, null, null, 500, ct)).Single(x => x.Id == row.Id);
        return WorkflowResult<WorkflowRequestDetail>.Success(new(summary, await repository.ListApprovalsAsync(row.Id, ct), await repository.ListTimelineAsync(row.Id, 500, ct)));
    }

    public async Task<WorkflowResult<IReadOnlyList<WorkflowRequestSummary>>> ListRequestsAsync(Guid userId, Guid? companyId, Guid? employeeId, Guid? requesterUserId, string? status, int take, CancellationToken ct)
    {
        var access = await ResolveAccessAsync(userId, ct); if (companyId is not null && !access.Global && !access.CompanyIds.Contains(companyId.Value)) return WorkflowResult<IReadOnlyList<WorkflowRequestSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return WorkflowResult<IReadOnlyList<WorkflowRequestSummary>>.Success(await repository.ListRequestsAsync(access.Global, access.CompanyIds, companyId, employeeId, requesterUserId, Normalize(status), Math.Clamp(take, 1, 500), ct));
    }

    public async Task<WorkflowResult<WorkflowRequestDetail>> SubmitAsync(Guid userId, Guid requestId, WorkflowRequestActionRequest request, CancellationToken ct)
    {
        var row = await GetRequestForMutationAsync(userId, requestId, request.Version, ct); if (!row.Succeeded || row.Value is null) return WorkflowResult<WorkflowRequestDetail>.Failure(row.ErrorCode!, row.ErrorMessage!);
        if (row.Value.RequesterUserId != userId && !await accessControlService.HasPermissionAsync(userId, WorkflowPermissions.RequestManage, ct)) return WorkflowResult<WorkflowRequestDetail>.Failure("WORKFLOW_REQUEST_OWNER_DENIED", "Yalnız talep sahibi veya yönetim yetkili kullanıcı talebi gönderebilir.");
        var type = await repository.FindRequestTypeAsync(row.Value.RequestTypeId, ct); if (type is null || !type.IsActive) return WorkflowResult<WorkflowRequestDetail>.Failure("WORKFLOW_REQUEST_TYPE_INACTIVE", "Talep türü aktif değil.");
        if (!TryValidatePayload(row.Value.RequestDataJson, type.RequiredFieldsJson, out _, out var missing)) return WorkflowResult<WorkflowRequestDetail>.Failure("WORKFLOW_PAYLOAD_INVALID", missing is null ? "Talep verisi geçersiz." : $"Zorunlu alan eksik: {missing}.");
        try
        {
            var definitions = await repository.ListStepDefinitionsAsync(type.Id, ct); var now = timeProvider.GetUtcNow(); var from = row.Value.Status;
            row.Value.Submit(type.SlaMinutes, definitions.Count, now, userId);
            foreach (var step in definitions.OrderBy(x => x.StepOrder)) repository.AddApproval(WorkflowRequestApproval.Create(row.Value.CompanyId, row.Value.Id, step.StepOrder, step.Name, step.TargetKind, step.ApproverUserId, step.ApproverRoleId, step.StepOrder == 1));
            repository.AddHistory(WorkflowRequestHistory.Create(row.Value.CompanyId, row.Value.Id, "REQUEST_SUBMITTED", from, row.Value.Status, userId, now, JsonSerializer.Serialize(new { type.SlaMinutes, ApprovalStepCount = definitions.Count })));
            await repository.SaveChangesAsync(ct); return await GetRequestAsync(userId, row.Value.Id, ct);
        }
        catch (InvalidOperationException) { return WorkflowResult<WorkflowRequestDetail>.Failure("WORKFLOW_REQUEST_STATE_INVALID", "Talep mevcut durumda gönderilemez."); }
    }

    public Task<WorkflowResult<WorkflowRequestDetail>> ApproveAsync(Guid userId, Guid requestId, WorkflowRequestActionRequest request, CancellationToken ct) => DecideAsync(userId, requestId, request, true, ct);
    public Task<WorkflowResult<WorkflowRequestDetail>> RejectAsync(Guid userId, Guid requestId, WorkflowRequestActionRequest request, CancellationToken ct) => DecideAsync(userId, requestId, request, false, ct);

    public async Task<WorkflowResult<WorkflowRequestDetail>> CancelAsync(Guid userId, Guid requestId, WorkflowRequestActionRequest request, CancellationToken ct)
    {
        var found = await GetRequestForMutationAsync(userId, requestId, request.Version, ct); if (!found.Succeeded || found.Value is null) return WorkflowResult<WorkflowRequestDetail>.Failure(found.ErrorCode!, found.ErrorMessage!);
        if (found.Value.RequesterUserId != userId && !await accessControlService.HasPermissionAsync(userId, WorkflowPermissions.RequestManage, ct)) return WorkflowResult<WorkflowRequestDetail>.Failure("WORKFLOW_REQUEST_OWNER_DENIED", "Yalnız talep sahibi veya yönetim yetkili kullanıcı talebi iptal edebilir.");
        try
        {
            var from = found.Value.Status; var now = timeProvider.GetUtcNow(); found.Value.Cancel(now, userId); repository.AddHistory(WorkflowRequestHistory.Create(found.Value.CompanyId, found.Value.Id, "REQUEST_CANCELLED", from, found.Value.Status, userId, now, JsonSerializer.Serialize(new { request.Comment }))); await repository.SaveChangesAsync(ct); return await GetRequestAsync(userId, found.Value.Id, ct);
        }
        catch (InvalidOperationException) { return WorkflowResult<WorkflowRequestDetail>.Failure("WORKFLOW_REQUEST_STATE_INVALID", "Talep mevcut durumda iptal edilemez."); }
    }

    public async Task<WorkflowResult<IReadOnlyList<WorkflowSlaEventSummary>>> ListSlaEventsAsync(Guid userId, Guid? companyId, Guid? requestId, string? eventType, int take, CancellationToken ct)
    {
        var access = await ResolveAccessAsync(userId, ct); if (companyId is not null && !access.Global && !access.CompanyIds.Contains(companyId.Value)) return WorkflowResult<IReadOnlyList<WorkflowSlaEventSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return WorkflowResult<IReadOnlyList<WorkflowSlaEventSummary>>.Success(await repository.ListSlaEventsAsync(access.Global, access.CompanyIds, companyId, requestId, Normalize(eventType), Math.Clamp(take, 1, 500), ct));
    }

    public async Task<WorkflowResult<WorkflowSlaRunResult>> ProcessSlaAsync(Guid userId, CancellationToken ct)
    {
        var access = await ResolveAccessAsync(userId, ct); if (!access.Global && access.CompanyIds.Count == 0) return WorkflowResult<WorkflowSlaRunResult>.Failure("SCOPE_DENIED", "SLA işlemek için şirket kapsamınız bulunmuyor.");
        return WorkflowResult<WorkflowSlaRunResult>.Success(await slaProcessor.RunAsync(access.Global ? null : access.CompanyIds, ct));
    }

    private async Task<WorkflowResult<WorkflowRequestDetail>> DecideAsync(Guid userId, Guid requestId, WorkflowRequestActionRequest request, bool approve, CancellationToken ct)
    {
        var found = await GetRequestForMutationAsync(userId, requestId, request.Version, ct); if (!found.Succeeded || found.Value is null) return WorkflowResult<WorkflowRequestDetail>.Failure(found.ErrorCode!, found.ErrorMessage!);
        var row = found.Value; if (row.Status != WorkflowRequestStatuses.InApproval) return WorkflowResult<WorkflowRequestDetail>.Failure("WORKFLOW_REQUEST_STATE_INVALID", "Talep onay aşamasında değil.");
        var approval = await repository.FindCurrentApprovalAsync(row.Id, row.CurrentStepOrder, ct); if (approval is null) return WorkflowResult<WorkflowRequestDetail>.Failure("WORKFLOW_APPROVAL_NOT_FOUND", "Bekleyen onay adımı bulunamadı.");
        var allowed = approval.TargetKindSnapshot == ApprovalTargetKinds.User ? approval.ApproverUserIdSnapshot == userId : approval.ApproverRoleIdSnapshot is not null && await repository.UserHasRoleAsync(userId, approval.ApproverRoleIdSnapshot.Value, ct);
        if (!allowed) return WorkflowResult<WorkflowRequestDetail>.Failure("WORKFLOW_APPROVER_DENIED", "Bu onay adımı için yetkili değilsiniz.");
        try
        {
            var now = timeProvider.GetUtcNow(); var from = row.Status;
            if (approve)
            {
                approval.Approve(userId, request.Comment, now); var next = await repository.FindApprovalAsync(row.Id, row.CurrentStepOrder + 1, ct); if (next is not null) next.Activate(); row.AdvanceApproval(approval.StepOrder, next is not null, now, userId);
                repository.AddHistory(WorkflowRequestHistory.Create(row.CompanyId, row.Id, "REQUEST_APPROVED_STEP", from, row.Status, userId, now, JsonSerializer.Serialize(new { approval.StepOrder, request.Comment })));
            }
            else
            {
                approval.Reject(userId, request.Comment, now); row.Reject(approval.StepOrder, now, userId); repository.AddHistory(WorkflowRequestHistory.Create(row.CompanyId, row.Id, "REQUEST_REJECTED", from, row.Status, userId, now, JsonSerializer.Serialize(new { approval.StepOrder, request.Comment })));
            }
            await repository.SaveChangesAsync(ct); return await GetRequestAsync(userId, row.Id, ct);
        }
        catch (InvalidOperationException) { return WorkflowResult<WorkflowRequestDetail>.Failure("WORKFLOW_APPROVAL_STATE_INVALID", "Onay adımı başka bir işlem tarafından sonuçlandırılmış olabilir. Veriyi yenileyin."); }
    }

    private async Task<WorkflowResult<WorkflowRequest>> GetRequestForMutationAsync(Guid userId, Guid requestId, int version, CancellationToken ct)
    {
        var row = await repository.FindRequestAsync(requestId, ct); if (row is null) return WorkflowResult<WorkflowRequest>.Failure("WORKFLOW_REQUEST_NOT_FOUND", "Talep bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, row.CompanyId, ct)) return WorkflowResult<WorkflowRequest>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (row.Version != version) return WorkflowResult<WorkflowRequest>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Talep başka bir işlem tarafından değiştirildi. Veriyi yenileyin.");
        return WorkflowResult<WorkflowRequest>.Success(row);
    }

    private async Task<(bool Global, HashSet<Guid> CompanyIds)> ResolveAccessAsync(Guid userId, CancellationToken ct)
    {
        var snapshot = await accessControlService.GetSnapshotAsync(userId, ct); return (snapshot.Scopes.Any(x => x.ScopeType == ScopeTypes.Global), snapshot.Scopes.Where(x => x.ScopeType == ScopeTypes.Company && x.ScopeId is not null).Select(x => x.ScopeId!.Value).ToHashSet());
    }

    private static WorkflowRequestTypeSummary Map(WorkflowRequestType x) => new(x.Id, x.CompanyId, x.Code, x.Name, x.Description, x.SlaMinutes, x.RequiredFieldsJson, x.IsActive, x.Version);
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static bool TryValidateRequiredFields(string json, out string normalized)
    {
        normalized = "[]";
        try { using var doc = JsonDocument.Parse(json); if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(x.GetString()))) return false; normalized = JsonSerializer.Serialize(doc.RootElement.EnumerateArray().Select(x => x.GetString()!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()); return true; } catch { return false; }
    }

    private static bool TryValidatePayload(string payloadJson, string requiredFieldsJson, out string normalized, out string? missing)
    {
        normalized = "{}"; missing = null;
        try
        {
            using var payload = JsonDocument.Parse(payloadJson); if (payload.RootElement.ValueKind != JsonValueKind.Object) return false;
            using var required = JsonDocument.Parse(requiredFieldsJson);
            foreach (var field in required.RootElement.EnumerateArray().Select(x => x.GetString()!)) if (!payload.RootElement.TryGetProperty(field, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) { missing = field; return false; }
            normalized = JsonSerializer.Serialize(payload.RootElement); return true;
        }
        catch { return false; }
    }
}

public sealed class WorkflowSlaProcessor(IWorkflowRepository repository, TimeProvider timeProvider)
{
    public Task<WorkflowSlaRunResult> RunAsync(CancellationToken ct) => RunAsync(null, ct);

    public async Task<WorkflowSlaRunResult> RunAsync(IReadOnlyCollection<Guid>? companyIds, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow(); var candidates = await repository.BuildSlaCandidatesAsync(now, companyIds, ct); var created = 0;
        foreach (var candidate in candidates) if (await repository.TryInsertSlaEventAsync(candidate, now, ct)) created++;
        return new WorkflowSlaRunResult(candidates.Count, created, candidates.Count - created);
    }
}
