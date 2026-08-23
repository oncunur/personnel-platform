using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Integration;

public static class IntegrationSystemTypes
{
    public const string Pdks = "PDKS";
    public const string Meal = "MEAL";
    public const string Erp = "ERP";
    public const string Import = "IMPORT";
    public static bool IsKnown(string value) => value is Pdks or Meal or Erp or Import;
}

public static class IntegrationDeviceTypes
{
    public const string PdksTerminal = "PDKS_TERMINAL";
    public const string MealTerminal = "MEAL_TERMINAL";
    public const string Generic = "GENERIC";
    public static bool IsKnown(string value) => value is PdksTerminal or MealTerminal or Generic;
}

public static class IntegrationEntityTypes
{
    public const string Employee = "EMPLOYEE";
    public const string Camp = "CAMP";
    public const string MealType = "MEAL_TYPE";
    public const string Project = "PROJECT";
    public const string CostCenter = "COST_CENTER";
    public const string CostCategory = "COST_CATEGORY";
    public static bool IsKnown(string value) => value is Employee or Camp or MealType or Project or CostCenter or CostCategory;
}

public static class IntegrationEventTypes
{
    public const string AttendanceEvent = "ATTENDANCE_EVENT";
    public const string MealConsumption = "MEAL_CONSUMPTION";
    public const string ImportRow = "IMPORT_ROW";
    public static bool IsKnown(string value) => value is AttendanceEvent or MealConsumption or ImportRow;
}

public static class IntegrationStagingStatuses
{
    public const string Received = "RECEIVED";
    public const string Processing = "PROCESSING";
    public const string Processed = "PROCESSED";
    public const string BusinessError = "BUSINESS_ERROR";
    public const string TechnicalError = "TECHNICAL_ERROR";
    public const string DeadLetter = "DEAD_LETTER";
    public static bool IsKnown(string value) => value is Received or Processing or Processed or BusinessError or TechnicalError or DeadLetter;
    public static bool IsTerminal(string value) => value is Processed or DeadLetter;
}

public sealed class IntegrationSystem : AuditableEntity
{
    private IntegrationSystem() { }
    public Guid CompanyId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string SystemType { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public static IntegrationSystem Create(Guid companyId, string code, string name, string systemType, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Company and actor are required.");
        var type = NormalizeRequired(systemType, 30).ToUpperInvariant();
        if (!IntegrationSystemTypes.IsKnown(type)) throw new ArgumentException("Integration system type is invalid.", nameof(systemType));
        return new IntegrationSystem
        {
            CompanyId = companyId,
            Code = NormalizeRequired(code, 80).ToUpperInvariant(),
            Name = NormalizeRequired(name, 150),
            SystemType = type,
            IsActive = true,
            CreatedAt = now.ToUniversalTime(),
            CreatedBy = actorUserId
        };
    }

    public void SetActive(bool active, DateTimeOffset now, Guid actorUserId)
    {
        if (IsActive == active) return;
        IsActive = active; Touch(now, actorUserId);
    }

    private void Touch(DateTimeOffset now, Guid actorUserId) { UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++; }
    private static string NormalizeRequired(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Text is too long."); return v; }
}

public sealed class IntegrationDevice : AuditableEntity
{
    private IntegrationDevice() { }
    public Guid CompanyId { get; private set; }
    public Guid IntegrationSystemId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string DeviceType { get; private set; } = IntegrationDeviceTypes.Generic;
    public Guid? ScopedCampId { get; private set; }
    public string CredentialHash { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset? LastSeenAt { get; private set; }
    public DateTimeOffset? LastErrorAt { get; private set; }
    public string? LastErrorMessage { get; private set; }

    public static IntegrationDevice Create(Guid companyId, Guid integrationSystemId, string code, string name, string deviceType, Guid? scopedCampId, string credentialHash, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || integrationSystemId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Company, system and actor are required.");
        var type = Required(deviceType, 40).ToUpperInvariant();
        if (!IntegrationDeviceTypes.IsKnown(type)) throw new ArgumentException("Integration device type is invalid.", nameof(deviceType));
        return new IntegrationDevice
        {
            CompanyId = companyId,
            IntegrationSystemId = integrationSystemId,
            Code = Required(code, 100).ToUpperInvariant(),
            Name = Required(name, 150),
            DeviceType = type,
            ScopedCampId = scopedCampId,
            CredentialHash = Required(credentialHash, 128),
            IsActive = true,
            CreatedAt = now.ToUniversalTime(),
            CreatedBy = actorUserId
        };
    }

    public void RotateCredential(string credentialHash, DateTimeOffset now, Guid actorUserId) { CredentialHash = Required(credentialHash, 128); Touch(now, actorUserId); }
    public void SetActive(bool active, DateTimeOffset now, Guid actorUserId) { if (IsActive == active) return; IsActive = active; Touch(now, actorUserId); }
    public void MarkSeen(DateTimeOffset now) { LastSeenAt = now.ToUniversalTime(); LastErrorMessage = null; }
    public void MarkError(string message, DateTimeOffset now) { LastErrorAt = now.ToUniversalTime(); LastErrorMessage = Optional(message, 1000); }
    private void Touch(DateTimeOffset now, Guid actorUserId) { UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++; }
    private static string Required(string value, int max) => Optional(value, max) ?? throw new ArgumentException("Value is required.");
    private static string? Optional(string? value, int max) { if (string.IsNullOrWhiteSpace(value)) return null; var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Text is too long."); return v; }
}

public sealed class ExternalEntityMapping : AuditableEntity
{
    private ExternalEntityMapping() { }
    public Guid CompanyId { get; private set; }
    public Guid IntegrationSystemId { get; private set; }
    public string EntityType { get; private set; } = string.Empty;
    public string ExternalCode { get; private set; } = string.Empty;
    public Guid InternalEntityId { get; private set; }
    public bool IsActive { get; private set; }

    public static ExternalEntityMapping Create(Guid companyId, Guid systemId, string entityType, string externalCode, Guid internalEntityId, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || systemId == Guid.Empty || internalEntityId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Company, system, internal entity and actor are required.");
        var type = Required(entityType, 40).ToUpperInvariant();
        if (!IntegrationEntityTypes.IsKnown(type)) throw new ArgumentException("Mapping entity type is invalid.", nameof(entityType));
        return new ExternalEntityMapping
        {
            CompanyId = companyId, IntegrationSystemId = systemId, EntityType = type,
            ExternalCode = Required(externalCode, 200).ToUpperInvariant(), InternalEntityId = internalEntityId,
            IsActive = true, CreatedAt = now.ToUniversalTime(), CreatedBy = actorUserId
        };
    }

    public void ChangeTarget(Guid internalEntityId, bool active, DateTimeOffset now, Guid actorUserId)
    {
        if (internalEntityId == Guid.Empty) throw new ArgumentException("Internal entity is required.");
        InternalEntityId = internalEntityId; IsActive = active; UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
    }
    private static string Required(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Text is too long."); return v; }
}

public sealed class IntegrationStagingRecord : AuditableEntity
{
    private IntegrationStagingRecord() { }
    public Guid CompanyId { get; private set; }
    public Guid IntegrationSystemId { get; private set; }
    public Guid? DeviceId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string ExternalEventId { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = "{}";
    public string Status { get; private set; } = IntegrationStagingStatuses.Received;
    public int AttemptCount { get; private set; }
    public DateTimeOffset? NextRetryAt { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? ProcessedEntityType { get; private set; }
    public Guid? ProcessedEntityId { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset? LastAttemptAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    public static IntegrationStagingRecord Create(Guid companyId, Guid systemId, Guid? deviceId, string eventType, string externalEventId, string payloadJson, DateTimeOffset now)
    {
        if (companyId == Guid.Empty || systemId == Guid.Empty) throw new ArgumentException("Company and system are required.");
        var type = Required(eventType, 50).ToUpperInvariant();
        if (!IntegrationEventTypes.IsKnown(type)) throw new ArgumentException("Integration event type is invalid.", nameof(eventType));
        return new IntegrationStagingRecord
        {
            CompanyId = companyId, IntegrationSystemId = systemId, DeviceId = deviceId, EventType = type,
            ExternalEventId = Required(externalEventId, 240), PayloadJson = Required(payloadJson, 100_000),
            Status = IntegrationStagingStatuses.Received, AttemptCount = 0, ReceivedAt = now.ToUniversalTime(), CreatedAt = now.ToUniversalTime()
        };
    }

    public string BeginProcessing(DateTimeOffset now)
    {
        if (Status is not (IntegrationStagingStatuses.Received or IntegrationStagingStatuses.TechnicalError)) throw new InvalidOperationException("Staging record is not processable.");
        var previous = Status; Status = IntegrationStagingStatuses.Processing; AttemptCount++; LastAttemptAt = now.ToUniversalTime(); NextRetryAt = null; ErrorCode = null; ErrorMessage = null; Touch(now); return previous;
    }

    public string Complete(string entityType, Guid entityId, DateTimeOffset now)
    {
        if (Status != IntegrationStagingStatuses.Processing) throw new InvalidOperationException("Only processing record can complete.");
        var previous = Status; Status = IntegrationStagingStatuses.Processed; ProcessedEntityType = Required(entityType, 60).ToUpperInvariant(); ProcessedEntityId = entityId; ProcessedAt = now.ToUniversalTime(); ErrorCode = null; ErrorMessage = null; Touch(now); return previous;
    }

    public string BusinessError(string code, string message, DateTimeOffset now)
    {
        if (Status != IntegrationStagingStatuses.Processing) throw new InvalidOperationException("Only processing record can fail.");
        var previous = Status; Status = IntegrationStagingStatuses.BusinessError; ErrorCode = Required(code, 120).ToUpperInvariant(); ErrorMessage = Required(message, 2000); NextRetryAt = null; Touch(now); return previous;
    }

    public string TechnicalError(string code, string message, int maxAttempts, DateTimeOffset now)
    {
        if (Status != IntegrationStagingStatuses.Processing) throw new InvalidOperationException("Only processing record can fail.");
        if (maxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        var previous = Status;
        ErrorCode = Required(code, 120).ToUpperInvariant(); ErrorMessage = Required(message, 2000);
        if (AttemptCount >= maxAttempts) { Status = IntegrationStagingStatuses.DeadLetter; NextRetryAt = null; }
        else { Status = IntegrationStagingStatuses.TechnicalError; NextRetryAt = now.ToUniversalTime().AddMinutes(Math.Min(60, Math.Pow(2, AttemptCount))); }
        Touch(now); return previous;
    }

    public string Requeue(DateTimeOffset now, Guid actorUserId)
    {
        if (Status is not (IntegrationStagingStatuses.BusinessError or IntegrationStagingStatuses.TechnicalError or IntegrationStagingStatuses.DeadLetter)) throw new InvalidOperationException("Only failed staging records can be requeued.");
        var previous = Status; Status = IntegrationStagingStatuses.Received; AttemptCount = 0; NextRetryAt = null; ErrorCode = null; ErrorMessage = null; UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++; return previous;
    }

    private void Touch(DateTimeOffset now) { UpdatedAt = now.ToUniversalTime(); Version++; }
    private static string Required(string value, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length > max) throw new ArgumentException("Text is too long."); return v; }
}

public sealed class IntegrationStagingHistory : Entity
{
    private IntegrationStagingHistory() { }
    public Guid StagingRecordId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string FromStatus { get; private set; } = string.Empty;
    public string ToStatus { get; private set; } = string.Empty;
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    public static IntegrationStagingHistory Create(Guid stagingRecordId, string eventType, string fromStatus, string toStatus, string? errorCode, string? errorMessage, Guid? actorUserId, DateTimeOffset now)
    {
        if (stagingRecordId == Guid.Empty) throw new ArgumentException("Staging record is required.");
        return new IntegrationStagingHistory { StagingRecordId = stagingRecordId, EventType = eventType, FromStatus = fromStatus, ToStatus = toStatus, ErrorCode = errorCode, ErrorMessage = errorMessage, ActorUserId = actorUserId, OccurredAt = now.ToUniversalTime() };
    }
}
