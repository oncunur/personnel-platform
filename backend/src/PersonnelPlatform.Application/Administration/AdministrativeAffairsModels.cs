namespace PersonnelPlatform.Application.Administration;

public static class AdministrativeAffairsPermissions
{
    public const string TaskView = "administration.task.view";
    public const string TaskManage = "administration.task.manage";
    public const string ContractView = "administration.contract.view";
    public const string ContractManage = "administration.contract.manage";
    public const string ReminderView = "administration.reminder.view";
    public const string ReminderProcess = "administration.reminder.process";
}

public sealed record CreateAdministrativeTaskRequest(Guid CompanyId, string Code, string Title, string? Description, Guid ResponsibleUserId, DateOnly DueDate, string RecurrenceUnit, int RecurrenceInterval, int ReminderDaysBefore);
public sealed record AdministrativeTaskActionRequest(int Version, string? Note = null);
public sealed record AdministrativeTaskSummary(Guid Id, Guid CompanyId, string Code, string Title, string? Description, Guid ResponsibleUserId, string ResponsibleUsername, DateOnly DueDate, string RecurrenceUnit, int RecurrenceInterval, int ReminderDaysBefore, string Status, int CompletionCount, DateTimeOffset? LastCompletedAt, int Version);
public sealed record AdministrativeTaskCompletionSummary(Guid Id, Guid TaskId, DateOnly DueDateSnapshot, DateOnly CompletedLocalDate, DateTimeOffset CompletedAt, Guid CompletedBy, string CompletedByUsername, string? Note);

public sealed record CreateAdministrativeContractRequest(Guid CompanyId, string ContractNo, string Title, string Counterparty, Guid ResponsibleUserId, DateOnly StartDate, DateOnly EndDate, int ReminderDaysBefore, bool AutoRenewal, decimal? ContractValue, string? Currency, string? Note);
public sealed record AdministrativeContractActionRequest(int Version);
public sealed record AdministrativeContractSummary(Guid Id, Guid CompanyId, string ContractNo, string Title, string Counterparty, Guid ResponsibleUserId, string ResponsibleUsername, DateOnly StartDate, DateOnly EndDate, int ReminderDaysBefore, bool AutoRenewal, decimal? ContractValue, string? Currency, string StoredStatus, string EffectiveStatus, string? Note, int Version);

public sealed record AdministrativeReminderSummary(Guid Id, Guid CompanyId, string EventType, string SourceType, Guid SourceId, DateOnly? DueDate, string Severity, string Message, string MetadataJson, DateTimeOffset CreatedAt);
public sealed record AdministrativeReminderCandidate(Guid CompanyId, string EventType, string SourceType, Guid SourceId, DateOnly? DueDate, string Severity, string DedupeKey, string Message, string MetadataJson);
public sealed record AdministrativeReminderRunResult(int Candidates, int Created, int Duplicates);

public sealed record AdministrativeAffairsResult<T>(bool Succeeded, T? Value, string? ErrorCode, string? ErrorMessage) where T : class
{
    public static AdministrativeAffairsResult<T> Success(T value) => new(true, value, null, null);
    public static AdministrativeAffairsResult<T> Failure(string code, string message) => new(false, null, code, message);
}
