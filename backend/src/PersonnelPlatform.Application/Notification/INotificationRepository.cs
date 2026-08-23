using PersonnelPlatform.Domain.Identity;
using PersonnelPlatform.Domain.Notification;

namespace PersonnelPlatform.Application.Notification;

public interface INotificationRepository
{
    Task<User?> FindUserAsync(Guid userId, CancellationToken ct);
    Task<Role?> FindRoleAsync(Guid roleId, CancellationToken ct);

    Task<NotificationTemplate?> FindTemplateAsync(Guid templateId, CancellationToken ct);
    Task<NotificationRule?> FindRuleAsync(Guid ruleId, CancellationToken ct);
    Task<IReadOnlyList<NotificationTemplateSummary>> ListTemplatesAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, bool? active, CancellationToken ct);
    Task<IReadOnlyList<NotificationRuleSummary>> ListRulesAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, bool? active, string? sourceModule, string? eventType, CancellationToken ct);
    void AddTemplate(NotificationTemplate template);
    void AddRule(NotificationRule rule);

    Task<UserNotification?> FindNotificationAsync(Guid notificationId, CancellationToken ct);
    Task<NotificationSummary?> GetNotificationSummaryAsync(Guid notificationId, CancellationToken ct);
    Task<IReadOnlyList<NotificationSummary>> ListNotificationsAsync(Guid userId, bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, string? status, string? priority, int take, CancellationToken ct);
    Task<IReadOnlyList<NotificationHistorySummary>> ListHistoryAsync(Guid notificationId, int take, CancellationToken ct);
    void AddHistory(NotificationHistory history);

    Task<IReadOnlyList<NotificationSourceEvent>> BuildSourceEventsAsync(IReadOnlyCollection<Guid>? companyIds, int takePerSource, CancellationToken ct);
    Task<IReadOnlyList<NotificationRule>> ListMatchingRulesAsync(Guid companyId, string sourceModule, string eventType, CancellationToken ct);
    Task<IReadOnlyList<Guid>> ResolveRoleUsersAsync(Guid companyId, Guid roleId, DateTimeOffset now, CancellationToken ct);
    Task<Guid?> ResolveManagerUserAsync(Guid userId, CancellationToken ct);
    Task<bool> UserHasCompanyAccessAsync(Guid userId, Guid companyId, DateTimeOffset now, CancellationToken ct);
    Task<bool> TryInsertNotificationAsync(UserNotification notification, CancellationToken ct);
    Task<IReadOnlyList<UserNotification>> ListEscalationCandidatesAsync(IReadOnlyCollection<Guid>? companyIds, DateTimeOffset now, int take, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);
}
