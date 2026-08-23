using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Application.Notification;
using PersonnelPlatform.Domain.Administration;
using PersonnelPlatform.Domain.Identity;
using PersonnelPlatform.Domain.Notification;
using PersonnelPlatform.Domain.Workflow;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Notification;

public sealed class NotificationRepository(ApplicationDbContext db) : INotificationRepository
{
    public Task<User?> FindUserAsync(Guid userId, CancellationToken ct) => db.Users.FirstOrDefaultAsync(x => x.Id == userId && x.DeletedAt == null, ct);
    public Task<Role?> FindRoleAsync(Guid roleId, CancellationToken ct) => db.Roles.FirstOrDefaultAsync(x => x.Id == roleId && x.DeletedAt == null, ct);

    public Task<NotificationTemplate?> FindTemplateAsync(Guid templateId, CancellationToken ct) => db.NotificationTemplates.FirstOrDefaultAsync(x => x.Id == templateId && x.DeletedAt == null, ct);
    public Task<NotificationRule?> FindRuleAsync(Guid ruleId, CancellationToken ct) => db.NotificationRules.FirstOrDefaultAsync(x => x.Id == ruleId && x.DeletedAt == null, ct);

    public async Task<IReadOnlyList<NotificationTemplateSummary>> ListTemplatesAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, bool? active, CancellationToken ct)
    {
        var q = db.NotificationTemplates.AsNoTracking().Where(x => x.DeletedAt == null);
        if (!globalAccess) q = q.Where(x => companyIds.Contains(x.CompanyId));
        if (companyId is not null) q = q.Where(x => x.CompanyId == companyId.Value);
        if (active is not null) q = q.Where(x => x.IsActive == active.Value);
        return await q.OrderBy(x => x.Code).Select(x => new NotificationTemplateSummary(x.Id, x.CompanyId, x.Code, x.Name, x.TitleTemplate, x.BodyTemplate, x.DeepLinkTemplate, x.IsActive, x.Version)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<NotificationRuleSummary>> ListRulesAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, bool? active, string? sourceModule, string? eventType, CancellationToken ct)
    {
        var q = db.NotificationRules.AsNoTracking().Where(x => x.DeletedAt == null);
        if (!globalAccess) q = q.Where(x => companyIds.Contains(x.CompanyId));
        if (companyId is not null) q = q.Where(x => x.CompanyId == companyId.Value);
        if (active is not null) q = q.Where(x => x.IsActive == active.Value);
        if (sourceModule is not null) q = q.Where(x => x.SourceModule == sourceModule);
        if (eventType is not null) q = q.Where(x => x.EventType == eventType);
        var rows = await q.OrderBy(x => x.Code).ToListAsync(ct);
        var templateIds = rows.Select(x => x.TemplateId).Distinct().ToArray();
        var userIds = rows.Where(x => x.RecipientUserId != null).Select(x => x.RecipientUserId!.Value)
            .Concat(rows.Where(x => x.EscalationUserId != null).Select(x => x.EscalationUserId!.Value)).Distinct().ToArray();
        var roleIds = rows.Where(x => x.RecipientRoleId != null).Select(x => x.RecipientRoleId!.Value)
            .Concat(rows.Where(x => x.EscalationRoleId != null).Select(x => x.EscalationRoleId!.Value)).Distinct().ToArray();
        var templates = await db.NotificationTemplates.AsNoTracking().Where(x => templateIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Code, ct);
        var users = await db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Username, ct);
        var roles = await db.Roles.AsNoTracking().Where(x => roleIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Code, ct);
        return rows.Select(x => new NotificationRuleSummary(
            x.Id, x.CompanyId, x.Code, x.Name, x.SourceModule, x.EventType, x.Priority, x.RecipientKind,
            x.RecipientUserId, Lookup(users, x.RecipientUserId), x.RecipientRoleId, Lookup(roles, x.RecipientRoleId),
            x.TemplateId, templates.TryGetValue(x.TemplateId, out var tc) ? tc : "—", x.EscalateAfterMinutes,
            x.EscalationRecipientKind, x.EscalationUserId, Lookup(users, x.EscalationUserId), x.EscalationRoleId, Lookup(roles, x.EscalationRoleId), x.IsActive, x.Version)).ToArray();
    }

    public void AddTemplate(NotificationTemplate template) => db.NotificationTemplates.Add(template);
    public void AddRule(NotificationRule rule) => db.NotificationRules.Add(rule);

    public Task<UserNotification?> FindNotificationAsync(Guid notificationId, CancellationToken ct) => db.UserNotifications.FirstOrDefaultAsync(x => x.Id == notificationId && x.DeletedAt == null, ct);

    public async Task<NotificationSummary?> GetNotificationSummaryAsync(Guid notificationId, CancellationToken ct)
    {
        var row = await db.UserNotifications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == notificationId && x.DeletedAt == null, ct);
        return row is null ? null : await MapNotificationAsync(row, ct);
    }

    public async Task<IReadOnlyList<NotificationSummary>> ListNotificationsAsync(Guid userId, bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, string? status, string? priority, int take, CancellationToken ct)
    {
        var q = db.UserNotifications.AsNoTracking().Where(x => x.UserId == userId && x.DeletedAt == null);
        if (!globalAccess) q = q.Where(x => companyIds.Contains(x.CompanyId));
        if (companyId is not null) q = q.Where(x => x.CompanyId == companyId.Value);
        if (status is not null) q = q.Where(x => x.Status == status);
        if (priority is not null) q = q.Where(x => x.Priority == priority);
        var rows = await q.OrderByDescending(x => x.CreatedAt).Take(take).ToListAsync(ct);
        if (rows.Count == 0) return [];
        var ruleIds = rows.Select(x => x.RuleId).Distinct().ToArray();
        var rules = await db.NotificationRules.AsNoTracking().Where(x => ruleIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Code, ct);
        var username = await db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => x.Username).FirstOrDefaultAsync(ct) ?? "—";
        return rows.Select(x => MapNotification(x, username, rules.TryGetValue(x.RuleId, out var rc) ? rc : "—")).ToArray();
    }

    public async Task<IReadOnlyList<NotificationHistorySummary>> ListHistoryAsync(Guid notificationId, int take, CancellationToken ct)
    {
        var rows = await db.NotificationHistories.AsNoTracking().Where(x => x.NotificationId == notificationId).OrderByDescending(x => x.OccurredAt).Take(take).ToListAsync(ct);
        var actorIds = rows.Where(x => x.ActorUserId != null).Select(x => x.ActorUserId!.Value).Distinct().ToArray();
        var actors = await db.Users.AsNoTracking().Where(x => actorIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Username, ct);
        return rows.Select(x => new NotificationHistorySummary(x.Id, x.NotificationId, x.EventType, x.FromStatus, x.ToStatus, x.ActorUserId, Lookup(actors, x.ActorUserId), x.OccurredAt, x.DetailsJson)).ToArray();
    }

    public void AddHistory(NotificationHistory history) => db.NotificationHistories.Add(history);

    public async Task<IReadOnlyList<NotificationSourceEvent>> BuildSourceEventsAsync(IReadOnlyCollection<Guid>? companyIds, int takePerSource, CancellationToken ct)
    {
        var result = new List<NotificationSourceEvent>();

        var requests = db.WorkflowRequests.AsNoTracking().Where(x => x.DeletedAt == null && x.Status == WorkflowRequestStatuses.InApproval);
        if (companyIds is not null) requests = requests.Where(x => companyIds.Contains(x.CompanyId));
        var pendingRequests = await requests.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).Take(takePerSource).ToListAsync(ct);
        if (pendingRequests.Count > 0)
        {
            var ids = pendingRequests.Select(x => x.Id).ToArray();
            var histories = await db.WorkflowRequestHistories.AsNoTracking().Where(x => ids.Contains(x.RequestId)).OrderByDescending(x => x.OccurredAt).ToListAsync(ct);
            var latestHistory = histories.GroupBy(x => x.RequestId).ToDictionary(g => g.Key, g => g.First());
            var approvals = await db.WorkflowRequestApprovals.AsNoTracking().Where(x => ids.Contains(x.RequestId) && x.Status == WorkflowApprovalStatuses.Pending).ToListAsync(ct);
            var approvalMap = approvals.ToDictionary(x => x.RequestId);
            foreach (var request in pendingRequests)
            {
                if (!latestHistory.TryGetValue(request.Id, out var history) || !approvalMap.TryGetValue(request.Id, out var approval)) continue;
                var metadata = JsonSerializer.Serialize(new { requestNo = request.RequestNo, currentStepOrder = request.CurrentStepOrder, requestId = request.Id });
                result.Add(new NotificationSourceEvent(history.Id, request.CompanyId, "WORKFLOW", "WORKFLOW_APPROVAL_PENDING", request.Priority, request.Id, request.RequesterUserId, null, approval.ApproverUserIdSnapshot, approval.ApproverRoleIdSnapshot, request.DueAt, $"{request.RequestNo} onay bekliyor (adım {request.CurrentStepOrder}).", metadata, history.OccurredAt));
            }
        }

        var sla = db.WorkflowSlaEvents.AsNoTracking().AsQueryable();
        if (companyIds is not null) sla = sla.Where(x => companyIds.Contains(x.CompanyId));
        var slaRows = await sla.OrderByDescending(x => x.CreatedAt).Take(takePerSource).ToListAsync(ct);
        if (slaRows.Count > 0)
        {
            var requestIds = slaRows.Select(x => x.RequestId).Distinct().ToArray();
            var requestMap = await db.WorkflowRequests.AsNoTracking().Where(x => requestIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
            var pendingApprovals = await db.WorkflowRequestApprovals.AsNoTracking().Where(x => requestIds.Contains(x.RequestId) && x.Status == WorkflowApprovalStatuses.Pending).ToListAsync(ct);
            var pendingMap = pendingApprovals.ToDictionary(x => x.RequestId);
            foreach (var e in slaRows)
            {
                if (!requestMap.TryGetValue(e.RequestId, out var request)) continue;
                pendingMap.TryGetValue(request.Id, out var approval);
                result.Add(new NotificationSourceEvent(e.Id, e.CompanyId, "WORKFLOW", e.EventType, e.Severity, request.Id, request.RequesterUserId, null, approval?.ApproverUserIdSnapshot, approval?.ApproverRoleIdSnapshot, request.DueAt, e.Message, MergeMetadata(e.MetadataJson, new { requestNo = request.RequestNo, requestId = request.Id }), e.CreatedAt));
            }
        }

        var admin = db.AdministrativeReminderEvents.AsNoTracking().AsQueryable();
        if (companyIds is not null) admin = admin.Where(x => companyIds.Contains(x.CompanyId));
        var adminRows = await admin.OrderByDescending(x => x.CreatedAt).Take(takePerSource).ToListAsync(ct);
        if (adminRows.Count > 0)
        {
            var taskIds = adminRows.Where(x => x.SourceType == "ADMIN_TASK").Select(x => x.SourceId).Distinct().ToArray();
            var contractIds = adminRows.Where(x => x.SourceType == "ADMIN_CONTRACT").Select(x => x.SourceId).Distinct().ToArray();
            var taskOwners = await db.AdministrativeTasks.AsNoTracking().Where(x => taskIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.ResponsibleUserId, ct);
            var contractOwners = await db.AdministrativeContracts.AsNoTracking().Where(x => contractIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.ResponsibleUserId, ct);
            foreach (var e in adminRows)
            {
                Guid? responsible = e.SourceType == "ADMIN_TASK" && taskOwners.TryGetValue(e.SourceId, out var taskOwner) ? taskOwner
                    : e.SourceType == "ADMIN_CONTRACT" && contractOwners.TryGetValue(e.SourceId, out var contractOwner) ? contractOwner : null;
                var dueAt = e.DueDate is { } d ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)) : null;
                result.Add(new NotificationSourceEvent(e.Id, e.CompanyId, "ADMINISTRATION", e.EventType, e.Severity, e.SourceId, null, responsible, null, null, dueAt, e.Message, e.MetadataJson, e.CreatedAt));
            }
        }

        return result.OrderByDescending(x => x.CreatedAt).ToArray();
    }

    public async Task<IReadOnlyList<NotificationRule>> ListMatchingRulesAsync(Guid companyId, string sourceModule, string eventType, CancellationToken ct) =>
        await db.NotificationRules.AsNoTracking().Where(x => x.CompanyId == companyId && x.DeletedAt == null && x.IsActive && x.SourceModule == sourceModule && x.EventType == eventType).OrderBy(x => x.Code).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> ResolveRoleUsersAsync(Guid companyId, Guid roleId, DateTimeOffset now, CancellationToken ct)
    {
        return await (from ur in db.UserRoles.AsNoTracking()
                      join u in db.Users.AsNoTracking() on ur.UserId equals u.Id
                      where ur.RoleId == roleId && u.IsActive && u.DeletedAt == null
                         && db.UserScopes.Any(s => s.UserId == u.Id && s.IsActive && s.ValidFrom <= now && (s.ValidUntil == null || s.ValidUntil > now)
                            && (s.ScopeType == ScopeTypes.Global || (s.ScopeType == ScopeTypes.Company && s.ScopeId == companyId)))
                      select u.Id).Distinct().ToListAsync(ct);
    }

    public async Task<Guid?> ResolveManagerUserAsync(Guid userId, CancellationToken ct)
    {
        var link = await db.EmployeeUserLinks.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.IsActive && x.DeletedAt == null, ct);
        if (link is null) return null;
        var managerEmployeeId = await db.Employees.AsNoTracking().Where(x => x.Id == link.EmployeeId && x.DeletedAt == null).Select(x => x.ManagerEmployeeId).FirstOrDefaultAsync(ct);
        if (managerEmployeeId is null) return null;
        return await db.EmployeeUserLinks.AsNoTracking().Where(x => x.EmployeeId == managerEmployeeId.Value && x.IsActive && x.DeletedAt == null).Select(x => (Guid?)x.UserId).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> TryInsertNotificationAsync(UserNotification n, CancellationToken ct)
    {
        var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO notification.notifications
                (id, company_id, user_id, rule_id, source_module, source_event_type, source_event_id, source_entity_id, parent_notification_id,
                 dedupe_key, priority, title, body, deep_link, status, due_at, snoozed_until, seen_at, started_at, completed_at, escalated_at,
                 escalation_level, created_at, created_by, updated_at, updated_by, deleted_at, deleted_by, version)
            VALUES ({n.Id}, {n.CompanyId}, {n.UserId}, {n.RuleId}, {n.SourceModule}, {n.SourceEventType}, {n.SourceEventId}, {n.SourceEntityId}, {n.ParentNotificationId},
                    {n.DedupeKey}, {n.Priority}, {n.Title}, {n.Body}, {n.DeepLink}, {n.Status}, {n.DueAt}, {n.SnoozedUntil}, {n.SeenAt}, {n.StartedAt}, {n.CompletedAt}, {n.EscalatedAt},
                    {n.EscalationLevel}, {n.CreatedAt}, NULL, NULL, NULL, NULL, NULL, {n.Version})
            ON CONFLICT (dedupe_key) DO NOTHING
            """, ct);
        return affected > 0;
    }

    public async Task<IReadOnlyList<UserNotification>> ListEscalationCandidatesAsync(IReadOnlyCollection<Guid>? companyIds, DateTimeOffset now, int take, CancellationToken ct)
    {
        var q = db.UserNotifications.Where(x => x.DeletedAt == null && x.Status != NotificationStatuses.Completed && x.Status != NotificationStatuses.Escalated && x.EscalationLevel < 5 && (x.Status != NotificationStatuses.Snoozed || x.SnoozedUntil <= now));
        if (companyIds is not null) q = q.Where(x => companyIds.Contains(x.CompanyId));
        var rows = await q.OrderBy(x => x.CreatedAt).Take(Math.Min(take * 3, 3000)).ToListAsync(ct);
        if (rows.Count == 0) return [];
        var ruleIds = rows.Select(x => x.RuleId).Distinct().ToArray();
        var rules = await db.NotificationRules.AsNoTracking().Where(x => ruleIds.Contains(x.Id) && x.DeletedAt == null && x.IsActive && x.EscalateAfterMinutes != null).ToDictionaryAsync(x => x.Id, ct);
        return rows.Where(x => rules.TryGetValue(x.RuleId, out var rule)
            && x.CreatedAt.AddMinutes(rule.EscalateAfterMinutes!.Value) <= now
            && (x.EscalationLevel == 0 || rule.EscalationRecipientKind == NotificationRecipientKinds.Manager)).Take(take).ToArray();
    }

    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private async Task<NotificationSummary> MapNotificationAsync(UserNotification x, CancellationToken ct)
    {
        var username = await db.Users.AsNoTracking().Where(u => u.Id == x.UserId).Select(u => u.Username).FirstOrDefaultAsync(ct) ?? "—";
        var ruleCode = await db.NotificationRules.AsNoTracking().Where(r => r.Id == x.RuleId).Select(r => r.Code).FirstOrDefaultAsync(ct) ?? "—";
        return MapNotification(x, username, ruleCode);
    }

    private static NotificationSummary MapNotification(UserNotification x, string username, string ruleCode) => new(
        x.Id, x.CompanyId, x.UserId, username, x.RuleId, ruleCode, x.SourceModule, x.SourceEventType, x.SourceEventId, x.SourceEntityId, x.ParentNotificationId,
        x.DedupeKey, x.Priority, x.Title, x.Body, x.DeepLink, x.Status, x.DueAt, x.SnoozedUntil, x.SeenAt, x.StartedAt, x.CompletedAt, x.EscalatedAt, x.EscalationLevel, x.CreatedAt, x.Version);

    private static string? Lookup(IReadOnlyDictionary<Guid, string> values, Guid? id) => id is { } v && values.TryGetValue(v, out var name) ? name : null;

    private static string MergeMetadata(string json, object additions)
    {
        try
        {
            using var baseDoc = JsonDocument.Parse(json);
            using var addDoc = JsonDocument.Parse(JsonSerializer.Serialize(additions));
            var map = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            if (baseDoc.RootElement.ValueKind == JsonValueKind.Object) foreach (var p in baseDoc.RootElement.EnumerateObject()) map[p.Name] = p.Value.Clone();
            foreach (var p in addDoc.RootElement.EnumerateObject()) map[p.Name] = p.Value.Clone();
            return JsonSerializer.Serialize(map);
        }
        catch { return JsonSerializer.Serialize(additions); }
    }
}
