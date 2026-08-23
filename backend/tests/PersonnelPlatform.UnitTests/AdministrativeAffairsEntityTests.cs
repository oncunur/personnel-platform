using PersonnelPlatform.Domain.Administration;
using Xunit;

namespace PersonnelPlatform.UnitTests;

public sealed class AdministrativeAffairsEntityTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 8, 23, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MonthlyTask_CompletionAdvancesFromOriginalDueDate()
    {
        var task = AdministrativeTask.Create(CompanyId, "RENT", "Kira kontrolü", null, UserId, new DateOnly(2026, 8, 31), AdministrativeRecurrenceUnits.Monthly, 1, 7, Now, UserId);
        task.Complete(new DateOnly(2026, 9, 2), Now.AddDays(10), UserId);
        Assert.Equal(AdministrativeTaskStatuses.Open, task.Status);
        Assert.Equal(new DateOnly(2026, 9, 30), task.DueDate);
        Assert.Equal(1, task.CompletionCount);
    }

    [Fact]
    public void NonRecurringTask_BecomesCompleted()
    {
        var task = AdministrativeTask.Create(CompanyId, "ONE", "Tek görev", null, UserId, new DateOnly(2026, 8, 25), AdministrativeRecurrenceUnits.None, 0, 3, Now, UserId);
        task.Complete(new DateOnly(2026, 8, 25), Now.AddDays(2), UserId);
        Assert.Equal(AdministrativeTaskStatuses.Completed, task.Status);
    }

    [Fact]
    public void RecurringTask_RejectsZeroInterval()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AdministrativeTask.Create(CompanyId, "BAD", "Hatalı", null, UserId, new DateOnly(2026, 8, 25), AdministrativeRecurrenceUnits.Weekly, 0, 3, Now, UserId));
    }

    [Fact]
    public void Contract_RejectsEndBeforeStart()
    {
        Assert.Throws<ArgumentException>(() => AdministrativeContract.Create(CompanyId, "C-1", "Kontrat", "Tedarikçi", UserId, new DateOnly(2026, 9, 1), new DateOnly(2026, 8, 31), 30, false, null, null, null, Now, UserId));
    }

    [Fact]
    public void Contract_ValueRequiresCurrency()
    {
        Assert.Throws<ArgumentException>(() => AdministrativeContract.Create(CompanyId, "C-2", "Kontrat", "Tedarikçi", UserId, new DateOnly(2026, 8, 1), new DateOnly(2027, 8, 1), 30, false, 1000m, null, null, Now, UserId));
    }

    [Fact]
    public void Reminder_NormalizesEventAndSeverity()
    {
        var row = AdministrativeReminderEvent.Create(CompanyId, "admin_task_due", "admin_task", Guid.NewGuid(), new DateOnly(2026, 8, 25), "important", "task:key", "Görev yaklaşıyor", "{}", Now);
        Assert.Equal("ADMIN_TASK_DUE", row.EventType);
        Assert.Equal("IMPORTANT", row.Severity);
    }
}
