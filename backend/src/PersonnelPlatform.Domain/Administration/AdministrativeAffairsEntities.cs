using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Administration;

public static class AdministrativeTaskStatuses
{
    public const string Open = "OPEN";
    public const string Paused = "PAUSED";
    public const string Completed = "COMPLETED";
    public const string Closed = "CLOSED";
    public static bool IsKnown(string value) => value is Open or Paused or Completed or Closed;
}

public static class AdministrativeRecurrenceUnits
{
    public const string None = "NONE";
    public const string Daily = "DAILY";
    public const string Weekly = "WEEKLY";
    public const string Monthly = "MONTHLY";
    public const string Yearly = "YEARLY";
    public static bool IsKnown(string value) => value is None or Daily or Weekly or Monthly or Yearly;
}

public sealed class AdministrativeTask : AuditableEntity
{
    private AdministrativeTask() { }

    public Guid CompanyId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid ResponsibleUserId { get; private set; }
    public DateOnly DueDate { get; private set; }
    public string RecurrenceUnit { get; private set; } = AdministrativeRecurrenceUnits.None;
    public int RecurrenceInterval { get; private set; }
    public int ReminderDaysBefore { get; private set; }
    public string Status { get; private set; } = AdministrativeTaskStatuses.Open;
    public int CompletionCount { get; private set; }
    public DateTimeOffset? LastCompletedAt { get; private set; }
    public Guid? LastCompletedBy { get; private set; }

    public static AdministrativeTask Create(Guid companyId, string code, string title, string? description, Guid responsibleUserId, DateOnly dueDate, string recurrenceUnit, int recurrenceInterval, int reminderDaysBefore, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || responsibleUserId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Company, responsible user and actor are required.");
        var recurrence = Required(recurrenceUnit, 20).ToUpperInvariant();
        if (!AdministrativeRecurrenceUnits.IsKnown(recurrence)) throw new ArgumentException("Recurrence unit is invalid.", nameof(recurrenceUnit));
        if (recurrence == AdministrativeRecurrenceUnits.None && recurrenceInterval != 0) throw new ArgumentException("Non-recurring task interval must be zero.", nameof(recurrenceInterval));
        if (recurrence != AdministrativeRecurrenceUnits.None && recurrenceInterval is < 1 or > 365) throw new ArgumentOutOfRangeException(nameof(recurrenceInterval));
        if (reminderDaysBefore is < 0 or > 365) throw new ArgumentOutOfRangeException(nameof(reminderDaysBefore));
        return new AdministrativeTask
        {
            CompanyId = companyId,
            Code = Required(code, 80).ToUpperInvariant(),
            Title = Required(title, 200),
            Description = Optional(description, 2000),
            ResponsibleUserId = responsibleUserId,
            DueDate = dueDate,
            RecurrenceUnit = recurrence,
            RecurrenceInterval = recurrenceInterval,
            ReminderDaysBefore = reminderDaysBefore,
            Status = AdministrativeTaskStatuses.Open,
            CompletionCount = 0,
            CreatedAt = now.ToUniversalTime(),
            CreatedBy = actorUserId
        };
    }

    public void Complete(DateOnly completedLocalDate, DateTimeOffset now, Guid actorUserId)
    {
        if (Status != AdministrativeTaskStatuses.Open) throw new InvalidOperationException("Only open administrative tasks can be completed.");
        CompletionCount++;
        LastCompletedAt = now.ToUniversalTime();
        LastCompletedBy = actorUserId;
        if (RecurrenceUnit == AdministrativeRecurrenceUnits.None)
            Status = AdministrativeTaskStatuses.Completed;
        else
            DueDate = NextDueDate(DueDate, RecurrenceUnit, RecurrenceInterval);
        UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
        _ = completedLocalDate;
    }

    public void Pause(DateTimeOffset now, Guid actorUserId)
    {
        if (Status != AdministrativeTaskStatuses.Open) throw new InvalidOperationException("Only open administrative tasks can be paused.");
        Status = AdministrativeTaskStatuses.Paused; UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
    }

    public void Resume(DateTimeOffset now, Guid actorUserId)
    {
        if (Status != AdministrativeTaskStatuses.Paused) throw new InvalidOperationException("Only paused administrative tasks can be resumed.");
        Status = AdministrativeTaskStatuses.Open; UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
    }

    public void Close(DateTimeOffset now, Guid actorUserId)
    {
        if (Status is AdministrativeTaskStatuses.Completed or AdministrativeTaskStatuses.Closed) throw new InvalidOperationException("Administrative task is already terminal.");
        Status = AdministrativeTaskStatuses.Closed; UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
    }

    public static DateOnly NextDueDate(DateOnly current, string unit, int interval) => unit switch
    {
        AdministrativeRecurrenceUnits.Daily => current.AddDays(interval),
        AdministrativeRecurrenceUnits.Weekly => current.AddDays(checked(interval * 7)),
        AdministrativeRecurrenceUnits.Monthly => current.AddMonths(interval),
        AdministrativeRecurrenceUnits.Yearly => current.AddYears(interval),
        _ => current
    };

    private static string Required(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
    private static string? Optional(string? value, int max) { if (string.IsNullOrWhiteSpace(value)) return null; var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
}

public sealed class AdministrativeTaskCompletion : Entity
{
    private AdministrativeTaskCompletion() { }
    public Guid CompanyId { get; private set; }
    public Guid TaskId { get; private set; }
    public DateOnly DueDateSnapshot { get; private set; }
    public DateOnly CompletedLocalDate { get; private set; }
    public DateTimeOffset CompletedAt { get; private set; }
    public Guid CompletedBy { get; private set; }
    public string? Note { get; private set; }

    public static AdministrativeTaskCompletion Create(Guid companyId, Guid taskId, DateOnly dueDateSnapshot, DateOnly completedLocalDate, DateTimeOffset completedAt, Guid completedBy, string? note)
    {
        if (companyId == Guid.Empty || taskId == Guid.Empty || completedBy == Guid.Empty) throw new ArgumentException("Required identifiers are missing.");
        return new AdministrativeTaskCompletion { CompanyId = companyId, TaskId = taskId, DueDateSnapshot = dueDateSnapshot, CompletedLocalDate = completedLocalDate, CompletedAt = completedAt.ToUniversalTime(), CompletedBy = completedBy, Note = Optional(note, 1000) };
    }

    private static string? Optional(string? value, int max) { if (string.IsNullOrWhiteSpace(value)) return null; var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
}

public static class AdministrativeContractStatuses
{
    public const string Active = "ACTIVE";
    public const string Closed = "CLOSED";
}

public sealed class AdministrativeContract : AuditableEntity
{
    private AdministrativeContract() { }
    public Guid CompanyId { get; private set; }
    public string ContractNo { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Counterparty { get; private set; } = string.Empty;
    public Guid ResponsibleUserId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public int ReminderDaysBefore { get; private set; }
    public bool AutoRenewal { get; private set; }
    public decimal? ContractValue { get; private set; }
    public string? Currency { get; private set; }
    public string Status { get; private set; } = AdministrativeContractStatuses.Active;
    public string? Note { get; private set; }

    public static AdministrativeContract Create(Guid companyId, string contractNo, string title, string counterparty, Guid responsibleUserId, DateOnly startDate, DateOnly endDate, int reminderDaysBefore, bool autoRenewal, decimal? contractValue, string? currency, string? note, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || responsibleUserId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Company, responsible user and actor are required.");
        if (endDate < startDate) throw new ArgumentException("Contract end date cannot be before start date.");
        if (reminderDaysBefore is < 0 or > 730) throw new ArgumentOutOfRangeException(nameof(reminderDaysBefore));
        if (contractValue is < 0) throw new ArgumentOutOfRangeException(nameof(contractValue));
        string? normalizedCurrency = null;
        if (contractValue is not null)
        {
            normalizedCurrency = Required(currency ?? string.Empty, 3).ToUpperInvariant();
            if (normalizedCurrency.Length != 3) throw new ArgumentException("Currency must be a three-letter code.", nameof(currency));
        }
        return new AdministrativeContract
        {
            CompanyId = companyId,
            ContractNo = Required(contractNo, 100).ToUpperInvariant(),
            Title = Required(title, 200),
            Counterparty = Required(counterparty, 200),
            ResponsibleUserId = responsibleUserId,
            StartDate = startDate,
            EndDate = endDate,
            ReminderDaysBefore = reminderDaysBefore,
            AutoRenewal = autoRenewal,
            ContractValue = contractValue is null ? null : decimal.Round(contractValue.Value, 2, MidpointRounding.AwayFromZero),
            Currency = normalizedCurrency,
            Status = AdministrativeContractStatuses.Active,
            Note = Optional(note, 2000),
            CreatedAt = now.ToUniversalTime(), CreatedBy = actorUserId
        };
    }

    public void Close(DateTimeOffset now, Guid actorUserId)
    {
        if (Status != AdministrativeContractStatuses.Active) throw new InvalidOperationException("Only active contracts can be closed.");
        Status = AdministrativeContractStatuses.Closed; UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
    }

    private static string Required(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
    private static string? Optional(string? value, int max) { if (string.IsNullOrWhiteSpace(value)) return null; var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
}

public sealed class AdministrativeReminderEvent : Entity
{
    private AdministrativeReminderEvent() { }
    public Guid CompanyId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string SourceType { get; private set; } = string.Empty;
    public Guid SourceId { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public string Severity { get; private set; } = "NORMAL";
    public string DedupeKey { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string MetadataJson { get; private set; } = "{}";
    public DateTimeOffset CreatedAt { get; private set; }

    public static AdministrativeReminderEvent Create(Guid companyId, string eventType, string sourceType, Guid sourceId, DateOnly? dueDate, string severity, string dedupeKey, string message, string metadataJson, DateTimeOffset createdAt)
    {
        if (companyId == Guid.Empty || sourceId == Guid.Empty) throw new ArgumentException("Company and source are required.");
        return new AdministrativeReminderEvent
        {
            CompanyId = companyId,
            EventType = Required(eventType, 80).ToUpperInvariant(),
            SourceType = Required(sourceType, 50).ToUpperInvariant(),
            SourceId = sourceId,
            DueDate = dueDate,
            Severity = Required(severity, 20).ToUpperInvariant(),
            DedupeKey = Required(dedupeKey, 300),
            Message = Required(message, 1000),
            MetadataJson = Required(metadataJson, 10000),
            CreatedAt = createdAt.ToUniversalTime()
        };
    }

    private static string Required(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Value is too long."); return v; }
}
