using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Domain.Administration;

namespace PersonnelPlatform.Application.Administration;

public sealed class AdministrativeAffairsService(
    IAdministrativeAffairsRepository repository,
    AccessControlService accessControlService,
    AdministrativeReminderProcessor reminderProcessor,
    TimeProvider timeProvider)
{
    public async Task<AdministrativeAffairsResult<IReadOnlyList<AdministrativeTaskSummary>>> ListTasksAsync(Guid userId, Guid? companyId, Guid? responsibleUserId, string? status, CancellationToken ct)
    {
        var access = await ResolveAccessAsync(userId, ct);
        if (companyId is not null && !access.Global && !access.CompanyIds.Contains(companyId.Value)) return AdministrativeAffairsResult<IReadOnlyList<AdministrativeTaskSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return AdministrativeAffairsResult<IReadOnlyList<AdministrativeTaskSummary>>.Success(await repository.ListTasksAsync(access.Global, access.CompanyIds, companyId, responsibleUserId, Normalize(status), ct));
    }

    public async Task<AdministrativeAffairsResult<AdministrativeTaskSummary>> CreateTaskAsync(Guid userId, CreateAdministrativeTaskRequest request, CancellationToken ct)
    {
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, request.CompanyId, ct)) return AdministrativeAffairsResult<AdministrativeTaskSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var responsible = await repository.FindUserAsync(request.ResponsibleUserId, ct);
        if (responsible is null || !responsible.IsActive) return AdministrativeAffairsResult<AdministrativeTaskSummary>.Failure("RESPONSIBLE_USER_NOT_FOUND", "Aktif sorumlu kullanıcı bulunamadı.");
        try
        {
            var row = AdministrativeTask.Create(request.CompanyId, request.Code, request.Title, request.Description, request.ResponsibleUserId, request.DueDate, request.RecurrenceUnit, request.RecurrenceInterval, request.ReminderDaysBefore, timeProvider.GetUtcNow(), userId);
            repository.AddTask(row); await repository.SaveChangesAsync(ct);
            return AdministrativeAffairsResult<AdministrativeTaskSummary>.Success(Map(row, responsible.Username));
        }
        catch (ArgumentException) { return AdministrativeAffairsResult<AdministrativeTaskSummary>.Failure("ADMIN_TASK_INVALID", "İdari görev bilgileri geçersiz."); }
    }

    public async Task<AdministrativeAffairsResult<AdministrativeTaskSummary>> CompleteTaskAsync(Guid userId, Guid taskId, AdministrativeTaskActionRequest request, CancellationToken ct)
    {
        var found = await GetTaskForMutationAsync(userId, taskId, request.Version, ct); if (!found.Succeeded || found.Value is null) return AdministrativeAffairsResult<AdministrativeTaskSummary>.Failure(found.ErrorCode!, found.ErrorMessage!);
        var row = found.Value; var responsible = await repository.FindUserAsync(row.ResponsibleUserId, ct); var dueSnapshot = row.DueDate;
        try
        {
            var now = timeProvider.GetUtcNow(); var localDate = DateOnly.FromDateTime(now.UtcDateTime);
            repository.AddTaskCompletion(AdministrativeTaskCompletion.Create(row.CompanyId, row.Id, dueSnapshot, localDate, now, userId, request.Note));
            row.Complete(localDate, now, userId); await repository.SaveChangesAsync(ct);
            return AdministrativeAffairsResult<AdministrativeTaskSummary>.Success(Map(row, responsible?.Username ?? "—"));
        }
        catch (InvalidOperationException) { return AdministrativeAffairsResult<AdministrativeTaskSummary>.Failure("ADMIN_TASK_STATE_INVALID", "Görev mevcut durumda tamamlanamaz."); }
        catch (ArgumentException) { return AdministrativeAffairsResult<AdministrativeTaskSummary>.Failure("ADMIN_TASK_COMPLETION_INVALID", "Görev tamamlama bilgileri geçersiz."); }
    }

    public Task<AdministrativeAffairsResult<AdministrativeTaskSummary>> PauseTaskAsync(Guid userId, Guid taskId, AdministrativeTaskActionRequest request, CancellationToken ct) => MutateTaskAsync(userId, taskId, request.Version, (x, now) => x.Pause(now, userId), ct);
    public Task<AdministrativeAffairsResult<AdministrativeTaskSummary>> ResumeTaskAsync(Guid userId, Guid taskId, AdministrativeTaskActionRequest request, CancellationToken ct) => MutateTaskAsync(userId, taskId, request.Version, (x, now) => x.Resume(now, userId), ct);
    public Task<AdministrativeAffairsResult<AdministrativeTaskSummary>> CloseTaskAsync(Guid userId, Guid taskId, AdministrativeTaskActionRequest request, CancellationToken ct) => MutateTaskAsync(userId, taskId, request.Version, (x, now) => x.Close(now, userId), ct);

    public async Task<AdministrativeAffairsResult<IReadOnlyList<AdministrativeTaskCompletionSummary>>> ListTaskCompletionsAsync(Guid userId, Guid taskId, int take, CancellationToken ct)
    {
        var row = await repository.FindTaskAsync(taskId, ct); if (row is null) return AdministrativeAffairsResult<IReadOnlyList<AdministrativeTaskCompletionSummary>>.Failure("ADMIN_TASK_NOT_FOUND", "İdari görev bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, row.CompanyId, ct)) return AdministrativeAffairsResult<IReadOnlyList<AdministrativeTaskCompletionSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return AdministrativeAffairsResult<IReadOnlyList<AdministrativeTaskCompletionSummary>>.Success(await repository.ListTaskCompletionsAsync(taskId, Math.Clamp(take, 1, 500), ct));
    }

    public async Task<AdministrativeAffairsResult<IReadOnlyList<AdministrativeContractSummary>>> ListContractsAsync(Guid userId, Guid? companyId, Guid? responsibleUserId, string? status, CancellationToken ct)
    {
        var access = await ResolveAccessAsync(userId, ct);
        if (companyId is not null && !access.Global && !access.CompanyIds.Contains(companyId.Value)) return AdministrativeAffairsResult<IReadOnlyList<AdministrativeContractSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return AdministrativeAffairsResult<IReadOnlyList<AdministrativeContractSummary>>.Success(await repository.ListContractsAsync(access.Global, access.CompanyIds, companyId, responsibleUserId, Normalize(status), today, ct));
    }

    public async Task<AdministrativeAffairsResult<AdministrativeContractSummary>> CreateContractAsync(Guid userId, CreateAdministrativeContractRequest request, CancellationToken ct)
    {
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, request.CompanyId, ct)) return AdministrativeAffairsResult<AdministrativeContractSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var responsible = await repository.FindUserAsync(request.ResponsibleUserId, ct); if (responsible is null || !responsible.IsActive) return AdministrativeAffairsResult<AdministrativeContractSummary>.Failure("RESPONSIBLE_USER_NOT_FOUND", "Aktif sorumlu kullanıcı bulunamadı.");
        try
        {
            var row = AdministrativeContract.Create(request.CompanyId, request.ContractNo, request.Title, request.Counterparty, request.ResponsibleUserId, request.StartDate, request.EndDate, request.ReminderDaysBefore, request.AutoRenewal, request.ContractValue, request.Currency, request.Note, timeProvider.GetUtcNow(), userId);
            repository.AddContract(row); await repository.SaveChangesAsync(ct);
            return AdministrativeAffairsResult<AdministrativeContractSummary>.Success(Map(row, responsible.Username, DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime)));
        }
        catch (ArgumentException) { return AdministrativeAffairsResult<AdministrativeContractSummary>.Failure("ADMIN_CONTRACT_INVALID", "Kontrat bilgileri geçersiz."); }
    }

    public async Task<AdministrativeAffairsResult<AdministrativeContractSummary>> CloseContractAsync(Guid userId, Guid contractId, AdministrativeContractActionRequest request, CancellationToken ct)
    {
        var row = await repository.FindContractAsync(contractId, ct); if (row is null) return AdministrativeAffairsResult<AdministrativeContractSummary>.Failure("ADMIN_CONTRACT_NOT_FOUND", "Kontrat bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, row.CompanyId, ct)) return AdministrativeAffairsResult<AdministrativeContractSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (row.Version != request.Version) return AdministrativeAffairsResult<AdministrativeContractSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Kontrat başka bir işlem tarafından değiştirildi. Veriyi yenileyin.");
        var responsible = await repository.FindUserAsync(row.ResponsibleUserId, ct);
        try { row.Close(timeProvider.GetUtcNow(), userId); await repository.SaveChangesAsync(ct); return AdministrativeAffairsResult<AdministrativeContractSummary>.Success(Map(row, responsible?.Username ?? "—", DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime))); }
        catch (InvalidOperationException) { return AdministrativeAffairsResult<AdministrativeContractSummary>.Failure("ADMIN_CONTRACT_STATE_INVALID", "Kontrat mevcut durumda kapatılamaz."); }
    }

    public async Task<AdministrativeAffairsResult<IReadOnlyList<AdministrativeReminderSummary>>> ListRemindersAsync(Guid userId, Guid? companyId, string? eventType, DateTimeOffset? from, int take, CancellationToken ct)
    {
        var access = await ResolveAccessAsync(userId, ct);
        if (companyId is not null && !access.Global && !access.CompanyIds.Contains(companyId.Value)) return AdministrativeAffairsResult<IReadOnlyList<AdministrativeReminderSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return AdministrativeAffairsResult<IReadOnlyList<AdministrativeReminderSummary>>.Success(await repository.ListRemindersAsync(access.Global, access.CompanyIds, companyId, Normalize(eventType), from, Math.Clamp(take, 1, 500), ct));
    }

    public async Task<AdministrativeAffairsResult<AdministrativeReminderRunResult>> ProcessRemindersAsync(Guid userId, CancellationToken ct)
    {
        _ = userId;
        return AdministrativeAffairsResult<AdministrativeReminderRunResult>.Success(await reminderProcessor.RunAsync(ct));
    }

    private async Task<AdministrativeAffairsResult<AdministrativeTask>> GetTaskForMutationAsync(Guid userId, Guid taskId, int version, CancellationToken ct)
    {
        var row = await repository.FindTaskAsync(taskId, ct); if (row is null) return AdministrativeAffairsResult<AdministrativeTask>.Failure("ADMIN_TASK_NOT_FOUND", "İdari görev bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, row.CompanyId, ct)) return AdministrativeAffairsResult<AdministrativeTask>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (row.Version != version) return AdministrativeAffairsResult<AdministrativeTask>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Görev başka bir işlem tarafından değiştirildi. Veriyi yenileyin.");
        return AdministrativeAffairsResult<AdministrativeTask>.Success(row);
    }

    private async Task<AdministrativeAffairsResult<AdministrativeTaskSummary>> MutateTaskAsync(Guid userId, Guid taskId, int version, Action<AdministrativeTask, DateTimeOffset> mutation, CancellationToken ct)
    {
        var found = await GetTaskForMutationAsync(userId, taskId, version, ct); if (!found.Succeeded || found.Value is null) return AdministrativeAffairsResult<AdministrativeTaskSummary>.Failure(found.ErrorCode!, found.ErrorMessage!);
        var row = found.Value; var responsible = await repository.FindUserAsync(row.ResponsibleUserId, ct);
        try { mutation(row, timeProvider.GetUtcNow()); await repository.SaveChangesAsync(ct); return AdministrativeAffairsResult<AdministrativeTaskSummary>.Success(Map(row, responsible?.Username ?? "—")); }
        catch (InvalidOperationException) { return AdministrativeAffairsResult<AdministrativeTaskSummary>.Failure("ADMIN_TASK_STATE_INVALID", "Görev mevcut durumda bu işleme uygun değil."); }
    }

    private async Task<(bool Global, HashSet<Guid> CompanyIds)> ResolveAccessAsync(Guid userId, CancellationToken ct)
    {
        var snapshot = await accessControlService.GetSnapshotAsync(userId, ct);
        return (snapshot.Scopes.Any(x => x.ScopeType == ScopeTypes.Global), snapshot.Scopes.Where(x => x.ScopeType == ScopeTypes.Company && x.ScopeId is not null).Select(x => x.ScopeId!.Value).ToHashSet());
    }

    private static AdministrativeTaskSummary Map(AdministrativeTask x, string username) => new(x.Id, x.CompanyId, x.Code, x.Title, x.Description, x.ResponsibleUserId, username, x.DueDate, x.RecurrenceUnit, x.RecurrenceInterval, x.ReminderDaysBefore, x.Status, x.CompletionCount, x.LastCompletedAt, x.Version);
    private static AdministrativeContractSummary Map(AdministrativeContract x, string username, DateOnly today) => new(x.Id, x.CompanyId, x.ContractNo, x.Title, x.Counterparty, x.ResponsibleUserId, username, x.StartDate, x.EndDate, x.ReminderDaysBefore, x.AutoRenewal, x.ContractValue, x.Currency, x.Status, EffectiveContractStatus(x, today), x.Note, x.Version);
    private static string EffectiveContractStatus(AdministrativeContract x, DateOnly today) => x.Status == AdministrativeContractStatuses.Closed ? "CLOSED" : today > x.EndDate ? "EXPIRED" : today >= x.EndDate.AddDays(-x.ReminderDaysBefore) ? "EXPIRING" : "ACTIVE";
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}

public sealed class AdministrativeReminderProcessor(IAdministrativeAffairsRepository repository, TimeProvider timeProvider)
{
    public async Task<AdministrativeReminderRunResult> RunAsync(CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var candidates = await repository.BuildReminderCandidatesAsync(today, vehicleDateHorizonDays: 30, taskDefaultHorizonDays: 7, maintenanceKmThreshold: 1000, ct);
        var created = 0;
        foreach (var candidate in candidates)
            if (await repository.TryInsertReminderAsync(candidate, now, ct)) created++;
        return new AdministrativeReminderRunResult(candidates.Count, created, candidates.Count - created);
    }
}
