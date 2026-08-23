using PersonnelPlatform.Domain.Notification;
using Xunit;

namespace PersonnelPlatform.UnitTests;

public sealed class NotificationEntityTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid RuleId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 8, 23, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Template_Create_NormalizesCodeAndStartsActive()
    {
        var row = NotificationTemplate.Create(CompanyId, " workflow_pending ", "Workflow", "{{message}}", "{{message}}", "/workflow", Now, ActorId);
        Assert.Equal("WORKFLOW_PENDING", row.Code);
        Assert.True(row.IsActive);
        Assert.Equal(1, row.Version);
    }

    [Fact]
    public void Rule_UserRecipientRequiresUserOnly()
    {
        Assert.Throws<ArgumentException>(() => NotificationRule.Create(CompanyId, "R1", "Rule", "WORKFLOW", "WORKFLOW_APPROVAL_PENDING", "IMPORTANT", "USER", null, null, Guid.NewGuid(), null, null, null, null, Now, ActorId));
    }

    [Fact]
    public void Rule_ManagerEscalationRejectsFixedTarget()
    {
        Assert.Throws<ArgumentException>(() => NotificationRule.Create(CompanyId, "R2", "Rule", "WORKFLOW", "WORKFLOW_APPROVAL_PENDING", "IMPORTANT", "REQUESTER", null, null, Guid.NewGuid(), 60, "MANAGER", UserId, null, Now, ActorId));
    }

    [Fact]
    public void Notification_StartsNew_ThenMovesInProgress()
    {
        var row = CreateNotification();
        Assert.Equal(NotificationStatuses.New, row.Status);
        row.Start(Now.AddMinutes(1), ActorId);
        Assert.Equal(NotificationStatuses.InProgress, row.Status);
        Assert.NotNull(row.SeenAt);
        Assert.NotNull(row.StartedAt);
        Assert.Equal(2, row.Version);
    }

    [Fact]
    public void Notification_SnoozeRequiresFutureTime()
    {
        var row = CreateNotification();
        Assert.Throws<ArgumentException>(() => row.Snooze(Now, Now, ActorId));
    }

    [Fact]
    public void CompletedNotification_IsTerminalForStart()
    {
        var row = CreateNotification();
        row.Complete(Now.AddMinutes(1), ActorId);
        Assert.Equal(NotificationStatuses.Completed, row.Status);
        Assert.Throws<InvalidOperationException>(() => row.Start(Now.AddMinutes(2), ActorId));
    }

    [Fact]
    public void Notification_Escalate_IsTerminalAndVersioned()
    {
        var row = CreateNotification();
        row.Escalate(Now.AddMinutes(30));
        Assert.Equal(NotificationStatuses.Escalated, row.Status);
        Assert.Equal(Now.AddMinutes(30), row.EscalatedAt);
        Assert.Equal(2, row.Version);
    }

    private static UserNotification CreateNotification() => UserNotification.Create(
        CompanyId, UserId, RuleId, "WORKFLOW", "WORKFLOW_APPROVAL_PENDING", Guid.NewGuid(), Guid.NewGuid(), null,
        $"TEST:{Guid.NewGuid():N}", NotificationPriorities.Important, "Onay bekliyor", "Talep onay bekliyor", "/workflow", Now.AddHours(1), 0, Now);
}
