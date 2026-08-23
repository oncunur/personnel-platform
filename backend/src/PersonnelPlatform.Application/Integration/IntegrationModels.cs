namespace PersonnelPlatform.Application.Integration;

public static class IntegrationPermissions
{
    public const string SystemView = "integration.system.view";
    public const string SystemManage = "integration.system.manage";
    public const string MappingView = "integration.mapping.view";
    public const string MappingManage = "integration.mapping.manage";
    public const string QueueView = "integration.queue.view";
    public const string QueueReprocess = "integration.queue.reprocess";
    public const string MonitorView = "integration.monitor.view";
}

public sealed record IntegrationSystemSummary(Guid Id, Guid CompanyId, string Code, string Name, string SystemType, bool IsActive, int Version);
public sealed record CreateIntegrationSystemRequest(Guid CompanyId, string Code, string Name, string SystemType);
public sealed record UpdateIntegrationSystemRequest(bool IsActive, int Version);

public sealed record IntegrationDeviceSummary(Guid Id, Guid CompanyId, Guid IntegrationSystemId, string Code, string Name, string DeviceType, Guid? ScopedCampId, bool IsActive, DateTimeOffset? LastSeenAt, DateTimeOffset? LastErrorAt, string? LastErrorMessage, int Version);
public sealed record IntegrationDeviceCredentialSummary(IntegrationDeviceSummary Device, string PlaintextKey);
public sealed record CreateIntegrationDeviceRequest(Guid IntegrationSystemId, string Code, string Name, string DeviceType, Guid? ScopedCampId);
public sealed record UpdateIntegrationDeviceRequest(bool IsActive, int Version);
public sealed record RotateIntegrationDeviceCredentialRequest(int Version);

public sealed record IntegrationMappingSummary(Guid Id, Guid CompanyId, Guid IntegrationSystemId, string EntityType, string ExternalCode, Guid InternalEntityId, bool IsActive, int Version);
public sealed record CreateIntegrationMappingRequest(Guid IntegrationSystemId, string EntityType, string ExternalCode, Guid InternalEntityId);
public sealed record UpdateIntegrationMappingRequest(Guid InternalEntityId, bool IsActive, int Version);

public sealed record StagingRecordSummary(Guid Id, Guid CompanyId, Guid IntegrationSystemId, Guid? DeviceId, string EventType, string ExternalEventId, string Status, int AttemptCount, DateTimeOffset? NextRetryAt, string? ErrorCode, string? ErrorMessage, string? ProcessedEntityType, Guid? ProcessedEntityId, DateTimeOffset ReceivedAt, DateTimeOffset? LastAttemptAt, DateTimeOffset? ProcessedAt, int Version);
public sealed record StagingHistorySummary(Guid Id, Guid StagingRecordId, string EventType, string FromStatus, string ToStatus, string? ErrorCode, string? ErrorMessage, Guid? ActorUserId, DateTimeOffset OccurredAt);
public sealed record IntegrationQueueQuery(Guid? CompanyId, Guid? IntegrationSystemId, string? EventType, string? Status, int Take = 200);

public sealed record IntegrationDeviceHealth(Guid DeviceId, string SystemCode, string DeviceCode, string DeviceName, string DeviceType, Guid? ScopedCampId, string Health, DateTimeOffset? LastSeenAt, DateTimeOffset? LastErrorAt, string? LastErrorMessage);
public sealed record IntegrationQueueMetrics(int Received, int Processing, int BusinessError, int TechnicalError, int DeadLetter, int Processed);
public sealed record IntegrationSystemHealth(Guid SystemId, string SystemCode, string SystemName, string SystemType, DateTimeOffset? LastEventAt, DateTimeOffset? LastProcessedAt, DateTimeOffset? LastErrorAt, IntegrationQueueMetrics Queue, IReadOnlyList<IntegrationDeviceHealth> Devices);
public sealed record IntegrationMonitoringSummary(Guid CompanyId, IReadOnlyList<IntegrationSystemHealth> Systems, int TotalBacklog, int TotalErrors, int TotalDeadLetters);

public sealed record ExternalDeviceHeaders(string CompanyCode, string SystemCode, string DeviceCode, string DeviceKey);
public sealed record IntegrationDeviceContext(Guid CompanyId, Guid SystemId, Guid DeviceId, string SystemType, string SystemCode, string DeviceCode, string DeviceType, Guid? ScopedCampId);

public sealed record AttendanceIntegrationEventRequest(string ExternalEventId, string ExternalEmployeeCode, string Direction, DateTimeOffset EventAt);
public sealed record MealIntegrationEventRequest(string ExternalEventId, string ExternalEmployeeCode, string ExternalMealTypeCode, DateTimeOffset ConsumedAt, decimal Quantity = 1m);
public sealed record MealIntegrationBatchRequest(IReadOnlyList<MealIntegrationEventRequest> Events);
public sealed record ExternalIngestResult(string ExternalEventId, Guid StagingRecordId, string Status, bool Duplicate);
public sealed record ExternalBatchIngestResult(int Received, int Duplicates, IReadOnlyList<ExternalIngestResult> Items);

public sealed record IntegrationProcessResult(int Claimed, int Processed, int BusinessErrors, int TechnicalErrors, int DeadLetters);

public sealed record IntegrationResult<T>(bool Succeeded, T? Value, string? ErrorCode, string? ErrorMessage) where T : class
{
    public static IntegrationResult<T> Success(T value) => new(true, value, null, null);
    public static IntegrationResult<T> Failure(string code, string message) => new(false, null, code, message);
}
