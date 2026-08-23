using System.Text.Json;
using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Domain.Notification;

namespace PersonnelPlatform.Application.Notification;

public sealed class NotificationService(
    INotificationRepository repository,
    AccessControlService accessControlService,
    NotificationProcessor processor,
    TimeProvider timeProvider)
{
    public async Task<NotificationResult<IReadOnlyList<NotificationTemplateSummary>>> ListTemplatesAsync(Guid userId, Guid? companyId, bool? active, CancellationToken ct)
    {
        var access = await ResolveAccessAsync(userId, ct);
        if (companyId is not null && !access.Global && !access.CompanyIds.Contains(companyId.Value)) return NotificationResult<IReadOnlyList<NotificationTemplateSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return NotificationResult<IReadOnlyList<NotificationTemplateSummary>>.Success(await repository.ListTemplatesAsync(access.Global, access.CompanyIds, companyId, active, ct));
    }

    public async Task<NotificationResult<NotificationTemplateSummary>> CreateTemplateAsync(Guid userId, CreateNotificationTemplateRequest request, CancellationToken ct)
    {
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, request.CompanyId, ct)) return NotificationResult<NotificationTemplateSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        try
        {
            var row = NotificationTemplate.Create(request.CompanyId, request.Code, request.Name, request.TitleTemplate, request.BodyTemplate, request.DeepLinkTemplate, timeProvider.GetUtcNow(), userId);
            repository.AddTemplate(row); await repository.SaveChangesAsync(ct);
            return NotificationResult<NotificationTemplateSummary>.Success(Map(row));
        }
        catch (ArgumentException) { return NotificationResult<NotificationTemplateSummary>.Failure("NOTIFICATION_TEMPLATE_INVALID", "Bildirim template bilgileri geçersiz."); }
    }

    public async Task<NotificationResult<NotificationTemplateSummary>> UpdateTemplateAsync(Guid userId, Guid templateId, UpdateNotificationTemplateRequest request, CancellationToken ct)
    {
        var row = await repository.FindTemplateAsync(templateId, ct); if (row is null) return NotificationResult<NotificationTemplateSummary>.Failure("NOTIFICATION_TEMPLATE_NOT_FOUND", "Bildirim template bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, row.CompanyId, ct)) return NotificationResult<NotificationTemplateSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (row.Version != request.Version) return NotificationResult<NotificationTemplateSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Template başka bir işlem tarafından değiştirildi. Veriyi yenileyin.");
        try { row.Update(request.Name, request.TitleTemplate, request.BodyTemplate, request.DeepLinkTemplate, request.IsActive, timeProvider.GetUtcNow(), userId); await repository.SaveChangesAsync(ct); return NotificationResult<NotificationTemplateSummary>.Success(Map(row)); }
        catch (ArgumentException) { return NotificationResult<NotificationTemplateSummary>.Failure("NOTIFICATION_TEMPLATE_INVALID", "Bildirim template bilgileri geçersiz."); }
    }

    public async Task<NotificationResult<IReadOnlyList<NotificationRuleSummary>>> ListRulesAsync(Guid userId, Guid? companyId, bool? active, string? sourceModule, string? eventType, CancellationToken ct)
    {
        var access = await ResolveAccessAsync(userId, ct);
        if (companyId is not null && !access.Global && !access.CompanyIds.Contains(companyId.Value)) return NotificationResult<IReadOnlyList<NotificationRuleSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return NotificationResult<IReadOnlyList<NotificationRuleSummary>>.Success(await repository.ListRulesAsync(access.Global, access.CompanyIds, companyId, active, Normalize(sourceModule), Normalize(eventType), ct));
    }

    public async Task<NotificationResult<NotificationRuleSummary>> CreateRuleAsync(Guid userId, CreateNotificationRuleRequest request, CancellationToken ct)
    {
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, request.CompanyId, ct)) return NotificationResult<NotificationRuleSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var validation = await ValidateRuleReferencesAsync(request.CompanyId, request.TemplateId, request.RecipientKind, request.RecipientUserId, request.RecipientRoleId, request.EscalationRecipientKind, request.EscalationUserId, request.EscalationRoleId, ct);
        if (validation is not null) return NotificationResult<NotificationRuleSummary>.Failure(validation.Value.Code, validation.Value.Message);
        try
        {
            var row = NotificationRule.Create(request.CompanyId, request.Code, request.Name, request.SourceModule, request.EventType, request.Priority, request.RecipientKind, request.RecipientUserId, request.RecipientRoleId, request.TemplateId, request.EscalateAfterMinutes, request.EscalationRecipientKind, request.EscalationUserId, request.EscalationRoleId, timeProvider.GetUtcNow(), userId);
            repository.AddRule(row); await repository.SaveChangesAsync(ct);
            var summary = (await repository.ListRulesAsync(true, [], row.CompanyId, null, null, null, ct)).Single(x => x.Id == row.Id);
            return NotificationResult<NotificationRuleSummary>.Success(summary);
        }
        catch (ArgumentException) { return NotificationResult<NotificationRuleSummary>.Failure("NOTIFICATION_RULE_INVALID", "Bildirim kuralı geçersiz."); }
    }

    public async Task<NotificationResult<NotificationRuleSummary>> UpdateRuleAsync(Guid userId, Guid ruleId, UpdateNotificationRuleRequest request, CancellationToken ct)
    {
        var row = await repository.FindRuleAsync(ruleId, ct); if (row is null) return NotificationResult<NotificationRuleSummary>.Failure("NOTIFICATION_RULE_NOT_FOUND", "Bildirim kuralı bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, row.CompanyId, ct)) return NotificationResult<NotificationRuleSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (row.Version != request.Version) return NotificationResult<NotificationRuleSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Bildirim kuralı başka bir işlem tarafından değiştirildi. Veriyi yenileyin.");
        var validation = await ValidateRuleReferencesAsync(row.CompanyId, request.TemplateId, request.RecipientKind, request.RecipientUserId, request.RecipientRoleId, request.EscalationRecipientKind, request.EscalationUserId, request.EscalationRoleId, ct);
        if (validation is not null) return NotificationResult<NotificationRuleSummary>.Failure(validation.Value.Code, validation.Value.Message);
        try
        {
            row.Update(request.Name, request.SourceModule, request.EventType, request.Priority, request.RecipientKind, request.RecipientUserId, request.RecipientRoleId, request.TemplateId, request.EscalateAfterMinutes, request.EscalationRecipientKind, request.EscalationUserId, request.EscalationRoleId, request.IsActive, timeProvider.GetUtcNow(), userId);
            await repository.SaveChangesAsync(ct);
            var summary = (await repository.ListRulesAsync(true, [], row.CompanyId, null, null, null, ct)).Single(x => x.Id == row.Id);
            return NotificationResult<NotificationRuleSummary>.Success(summary);
        }
        catch (ArgumentException) { return NotificationResult<NotificationRuleSummary>.Failure("NOTIFICATION_RULE_INVALID", "Bildirim kuralı geçersiz."); }
    }

    public async Task<NotificationResult<IReadOnlyList<NotificationSummary>>> ListNotificationsAsync(Guid userId, Guid? companyId, string? status, string? priority, int take, CancellationToken ct)
    {
        var access = await ResolveAccessAsync(userId, ct);
        if (companyId is not null && !access.Global && !access.CompanyIds.Contains(companyId.Value)) return NotificationResult<IReadOnlyList<NotificationSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return NotificationResult<IReadOnlyList<NotificationSummary>>.Success(await repository.ListNotificationsAsync(userId, access.Global, access.CompanyIds, companyId, Normalize(status), Normalize(priority), Math.Clamp(take, 1, 500), ct));
    }

    public async Task<NotificationResult<NotificationDetail>> GetNotificationAsync(Guid userId, Guid notificationId, CancellationToken ct)
    {
        var row = await repository.FindNotificationAsync(notificationId, ct); if (row is null) return NotificationResult<NotificationDetail>.Failure("NOTIFICATION_NOT_FOUND", "Bildirim bulunamadı.");
        if (row.UserId != userId) return NotificationResult<NotificationDetail>.Failure("NOTIFICATION_OWNER_DENIED", "Bu bildirim başka bir kullanıcıya aittir.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, row.CompanyId, ct)) return NotificationResult<NotificationDetail>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var summary = await repository.GetNotificationSummaryAsync(row.Id, ct); if (summary is null) return NotificationResult<NotificationDetail>.Failure("NOTIFICATION_NOT_FOUND", "Bildirim bulunamadı.");
        return NotificationResult<NotificationDetail>.Success(new(summary, await repository.ListHistoryAsync(row.Id, 200, ct)));
    }

    public async Task<NotificationResult<NotificationActionCenterSummary>> GetActionCenterAsync(Guid userId, Guid? companyId, CancellationToken ct)
    {
        var listed = await ListNotificationsAsync(userId, companyId, null, null, 500, ct); if (!listed.Succeeded || listed.Value is null) return NotificationResult<NotificationActionCenterSummary>.Failure(listed.ErrorCode!, listed.ErrorMessage!);
        var now = timeProvider.GetUtcNow();
        var actionable = listed.Value.Where(x => x.Status is not NotificationStatuses.Completed and not NotificationStatuses.Escalated && (x.Status != NotificationStatuses.Snoozed || x.SnoozedUntil <= now)).ToArray();
        var critical = actionable.Where(x => x.Priority == NotificationPriorities.Critical).OrderBy(x => x.DueAt ?? DateTimeOffset.MaxValue).Take(100).ToArray();
        var overdue = actionable.Where(x => x.DueAt is not null && x.DueAt < now).OrderBy(x => x.DueAt).Take(100).ToArray();
        var pending = actionable.OrderByDescending(x => PriorityRank(x.Priority)).ThenBy(x => x.DueAt ?? DateTimeOffset.MaxValue).Take(200).ToArray();
        return NotificationResult<NotificationActionCenterSummary>.Success(new(critical.Length, pending.Length, overdue.Length, critical, pending, overdue));
    }

    public Task<NotificationResult<NotificationDetail>> MarkSeenAsync(Guid userId, Guid notificationId, NotificationActionRequest request, CancellationToken ct) => MutateAsync(userId, notificationId, request.Version, "NOTIFICATION_SEEN", (x, now) => x.MarkSeen(now, userId), ct);
    public Task<NotificationResult<NotificationDetail>> StartAsync(Guid userId, Guid notificationId, NotificationActionRequest request, CancellationToken ct) => MutateAsync(userId, notificationId, request.Version, "NOTIFICATION_STARTED", (x, now) => x.Start(now, userId), ct);
    public Task<NotificationResult<NotificationDetail>> CompleteAsync(Guid userId, Guid notificationId, NotificationActionRequest request, CancellationToken ct) => MutateAsync(userId, notificationId, request.Version, "NOTIFICATION_COMPLETED", (x, now) => x.Complete(now, userId), ct);

    public async Task<NotificationResult<NotificationDetail>> SnoozeAsync(Guid userId, Guid notificationId, SnoozeNotificationRequest request, CancellationToken ct)
    {
        return await MutateAsync(userId, notificationId, request.Version, "NOTIFICATION_SNOOZED", (x, now) => x.Snooze(request.Until, now, userId), ct, JsonSerializer.Serialize(new { request.Until }));
    }

    public async Task<NotificationResult<NotificationRunResult>> ProcessAsync(Guid userId, CancellationToken ct)
    {
        var access = await ResolveAccessAsync(userId, ct); if (!access.Global && access.CompanyIds.Count == 0) return NotificationResult<NotificationRunResult>.Failure("SCOPE_DENIED", "Bildirim işlemek için şirket kapsamınız bulunmuyor.");
        return NotificationResult<NotificationRunResult>.Success(await processor.RunAsync(access.Global ? null : access.CompanyIds, ct));
    }

    private async Task<NotificationResult<NotificationDetail>> MutateAsync(Guid userId, Guid notificationId, int version, string eventType, Action<UserNotification, DateTimeOffset> mutation, CancellationToken ct, string detailsJson = "{}")
    {
        var row = await repository.FindNotificationAsync(notificationId, ct); if (row is null) return NotificationResult<NotificationDetail>.Failure("NOTIFICATION_NOT_FOUND", "Bildirim bulunamadı.");
        if (row.UserId != userId) return NotificationResult<NotificationDetail>.Failure("NOTIFICATION_OWNER_DENIED", "Bu bildirim başka bir kullanıcıya aittir.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, row.CompanyId, ct)) return NotificationResult<NotificationDetail>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (row.Version != version) return NotificationResult<NotificationDetail>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Bildirim başka bir işlem tarafından değiştirildi. Veriyi yenileyin.");
        try
        {
            var from = row.Status; var now = timeProvider.GetUtcNow(); mutation(row, now); repository.AddHistory(NotificationHistory.Create(row.CompanyId, row.Id, row.UserId, eventType, from, row.Status, userId, now, detailsJson)); await repository.SaveChangesAsync(ct); return await GetNotificationAsync(userId, row.Id, ct);
        }
        catch (InvalidOperationException) { return NotificationResult<NotificationDetail>.Failure("NOTIFICATION_STATE_INVALID", "Bildirim mevcut durumda bu işleme uygun değil."); }
        catch (ArgumentException) { return NotificationResult<NotificationDetail>.Failure("NOTIFICATION_ACTION_INVALID", "Bildirim işlemi geçersiz."); }
    }

    private async Task<(string Code, string Message)?> ValidateRuleReferencesAsync(Guid companyId, Guid templateId, string recipientKind, Guid? recipientUserId, Guid? recipientRoleId, string? escalationKind, Guid? escalationUserId, Guid? escalationRoleId, CancellationToken ct)
    {
        var template = await repository.FindTemplateAsync(templateId, ct); if (template is null || template.CompanyId != companyId) return ("NOTIFICATION_TEMPLATE_NOT_FOUND", "Şirket kapsamındaki template bulunamadı.");
        var kind = Normalize(recipientKind);
        if (kind == NotificationRecipientKinds.User && recipientUserId is not null) { var user = await repository.FindUserAsync(recipientUserId.Value, ct); if (user is null || !user.IsActive) return ("NOTIFICATION_RECIPIENT_USER_NOT_FOUND", "Aktif alıcı kullanıcı bulunamadı."); }
        if (kind == NotificationRecipientKinds.Role && recipientRoleId is not null) { var role = await repository.FindRoleAsync(recipientRoleId.Value, ct); if (role is null || !role.IsActive) return ("NOTIFICATION_RECIPIENT_ROLE_NOT_FOUND", "Aktif alıcı rolü bulunamadı."); }
        var escalation = Normalize(escalationKind);
        if (escalation == NotificationRecipientKinds.User && escalationUserId is not null) { var user = await repository.FindUserAsync(escalationUserId.Value, ct); if (user is null || !user.IsActive) return ("NOTIFICATION_ESCALATION_USER_NOT_FOUND", "Aktif escalation kullanıcısı bulunamadı."); }
        if (escalation == NotificationRecipientKinds.Role && escalationRoleId is not null) { var role = await repository.FindRoleAsync(escalationRoleId.Value, ct); if (role is null || !role.IsActive) return ("NOTIFICATION_ESCALATION_ROLE_NOT_FOUND", "Aktif escalation rolü bulunamadı."); }
        return null;
    }

    private async Task<(bool Global, HashSet<Guid> CompanyIds)> ResolveAccessAsync(Guid userId, CancellationToken ct)
    {
        var snapshot = await accessControlService.GetSnapshotAsync(userId, ct); return (snapshot.Scopes.Any(x => x.ScopeType == ScopeTypes.Global), snapshot.Scopes.Where(x => x.ScopeType == ScopeTypes.Company && x.ScopeId is not null).Select(x => x.ScopeId!.Value).ToHashSet());
    }

    private static NotificationTemplateSummary Map(NotificationTemplate x) => new(x.Id, x.CompanyId, x.Code, x.Name, x.TitleTemplate, x.BodyTemplate, x.DeepLinkTemplate, x.IsActive, x.Version);
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private static int PriorityRank(string value) => value switch { NotificationPriorities.Critical => 4, NotificationPriorities.Important => 3, NotificationPriorities.Normal => 2, _ => 1 };
}

public sealed class NotificationProcessor(INotificationRepository repository, TimeProvider timeProvider)
{
    public Task<NotificationRunResult> RunAsync(CancellationToken ct) => RunAsync(null, ct);

    public async Task<NotificationRunResult> RunAsync(IReadOnlyCollection<Guid>? companyIds, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var sourceEvents = await repository.BuildSourceEventsAsync(companyIds, 2000, ct);
        var ruleMatches = 0; var created = 0; var duplicates = 0;
        foreach (var source in sourceEvents)
        {
            var rules = await repository.ListMatchingRulesAsync(source.CompanyId, source.SourceModule, source.EventType, ct);
            ruleMatches += rules.Count;
            foreach (var rule in rules)
            {
                var template = await repository.FindTemplateAsync(rule.TemplateId, ct); if (template is null || !template.IsActive) continue;
                var recipients = await ResolveRuleRecipientsAsync(rule, source, now, ct);
                foreach (var userId in recipients)
                {
                    var user = await repository.FindUserAsync(userId, ct); if (user is null || !user.IsActive) continue;
                    var notification = UserNotification.Create(source.CompanyId, userId, rule.Id, source.SourceModule, source.EventType, source.Id, source.SourceEntityId, null, $"SRC:{rule.Id:N}:{source.SourceModule}:{source.Id:N}:{userId:N}", rule.Priority, Render(template.TitleTemplate, source), Render(template.BodyTemplate, source), Render(template.DeepLinkTemplate, source), source.DueAt, 0, now);
                    if (await repository.TryInsertNotificationAsync(notification, ct)) { created++; repository.AddHistory(NotificationHistory.Create(notification.CompanyId, notification.Id, notification.UserId, "NOTIFICATION_CREATED", null, notification.Status, null, now, JsonSerializer.Serialize(new { source.SourceModule, source.EventType, source.Id, rule.Code }))); }
                    else duplicates++;
                }
            }
        }
        await repository.SaveChangesAsync(ct);

        var escalated = 0;
        var candidates = await repository.ListEscalationCandidatesAsync(companyIds, now, 1000, ct);
        foreach (var original in candidates)
        {
            var rule = await repository.FindRuleAsync(original.RuleId, ct); if (rule is null || !rule.IsActive || rule.EscalateAfterMinutes is null || rule.EscalationRecipientKind is null) continue;
            var recipients = await ResolveEscalationRecipientsAsync(rule, original, now, ct); if (recipients.Count == 0) continue;
            var anyTarget = false;
            foreach (var userId in recipients)
            {
                var user = await repository.FindUserAsync(userId, ct); if (user is null || !user.IsActive || userId == original.UserId && rule.EscalationRecipientKind == NotificationRecipientKinds.Manager) continue;
                anyTarget = true;
                var child = UserNotification.Create(original.CompanyId, userId, original.RuleId, original.SourceModule, original.SourceEventType, original.SourceEventId, original.SourceEntityId, original.Id, $"ESC:{original.Id:N}:{userId:N}:{original.EscalationLevel + 1}", NotificationPriorities.Critical, $"Escalation · {original.Title}", original.Body, original.DeepLink, original.DueAt, original.EscalationLevel + 1, now);
                if (await repository.TryInsertNotificationAsync(child, ct)) { created++; repository.AddHistory(NotificationHistory.Create(child.CompanyId, child.Id, child.UserId, "NOTIFICATION_ESCALATION_CREATED", null, child.Status, null, now, JsonSerializer.Serialize(new { parentNotificationId = original.Id, level = child.EscalationLevel }))); }
                else duplicates++;
            }
            if (anyTarget)
            {
                var from = original.Status; original.Escalate(now); repository.AddHistory(NotificationHistory.Create(original.CompanyId, original.Id, original.UserId, "NOTIFICATION_ESCALATED", from, original.Status, null, now, JsonSerializer.Serialize(new { rule.EscalationRecipientKind, level = original.EscalationLevel + 1 }))); escalated++;
            }
        }
        await repository.SaveChangesAsync(ct);
        return new NotificationRunResult(sourceEvents.Count, ruleMatches, created, duplicates, escalated);
    }

    private async Task<IReadOnlyList<Guid>> ResolveRuleRecipientsAsync(NotificationRule rule, NotificationSourceEvent source, DateTimeOffset now, CancellationToken ct)
    {
        var users = new HashSet<Guid>();
        if (rule.RecipientKind == NotificationRecipientKinds.User && rule.RecipientUserId is not null) users.Add(rule.RecipientUserId.Value);
        else if (rule.RecipientKind == NotificationRecipientKinds.Role && rule.RecipientRoleId is not null) users.UnionWith(await repository.ResolveRoleUsersAsync(source.CompanyId, rule.RecipientRoleId.Value, now, ct));
        else if (rule.RecipientKind == NotificationRecipientKinds.Requester && source.RequesterUserId is not null) users.Add(source.RequesterUserId.Value);
        else if (rule.RecipientKind == NotificationRecipientKinds.Responsible && source.ResponsibleUserId is not null) users.Add(source.ResponsibleUserId.Value);
        else if (rule.RecipientKind == NotificationRecipientKinds.CurrentApprover)
        {
            if (source.CurrentApproverUserId is not null) users.Add(source.CurrentApproverUserId.Value);
            if (source.CurrentApproverRoleId is not null) users.UnionWith(await repository.ResolveRoleUsersAsync(source.CompanyId, source.CurrentApproverRoleId.Value, now, ct));
        }
        return users.ToArray();
    }

    private async Task<IReadOnlyList<Guid>> ResolveEscalationRecipientsAsync(NotificationRule rule, UserNotification original, DateTimeOffset now, CancellationToken ct)
    {
        if (rule.EscalationRecipientKind == NotificationRecipientKinds.User && rule.EscalationUserId is not null) return [rule.EscalationUserId.Value];
        if (rule.EscalationRecipientKind == NotificationRecipientKinds.Role && rule.EscalationRoleId is not null) return await repository.ResolveRoleUsersAsync(original.CompanyId, rule.EscalationRoleId.Value, now, ct);
        if (rule.EscalationRecipientKind == NotificationRecipientKinds.Manager)
        {
            var manager = await repository.ResolveManagerUserAsync(original.UserId, ct); return manager is null ? [] : [manager.Value];
        }
        return [];
    }

    private static string Render(string template, NotificationSourceEvent source)
    {
        var result = template
            .Replace("{{message}}", source.Message, StringComparison.OrdinalIgnoreCase)
            .Replace("{{sourceModule}}", source.SourceModule, StringComparison.OrdinalIgnoreCase)
            .Replace("{{eventType}}", source.EventType, StringComparison.OrdinalIgnoreCase)
            .Replace("{{sourceEventId}}", source.Id.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{{sourceEntityId}}", source.SourceEntityId?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{{dueAt}}", source.DueAt?.ToString("O") ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        try
        {
            using var doc = JsonDocument.Parse(source.MetadataJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
                foreach (var property in doc.RootElement.EnumerateObject())
                {
                    var value = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() ?? string.Empty : property.Value.ToString();
                    result = result.Replace($"{{{{{property.Name}}}}}", value, StringComparison.OrdinalIgnoreCase);
                }
        }
        catch { }
        return result;
    }
}
