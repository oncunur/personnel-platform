using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Attendance;

public static class RawAttendanceDirections
{
    public const string In = "IN";
    public const string Out = "OUT";
    public const string Unknown = "UNKNOWN";
    public static bool IsKnown(string value) => value is In or Out or Unknown;
}

public static class RawAttendanceSources
{
    public const string Pdks = "PDKS";
    public const string Manual = "MANUAL";
    public const string Import = "IMPORT";
    public const string Integration = "INTEGRATION";
    public static bool IsKnown(string value) => value is Pdks or Manual or Import or Integration;
}

public sealed class RawAttendanceEvent : Entity
{
    private RawAttendanceEvent() { }

    public Guid CompanyId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public string Direction { get; private set; } = RawAttendanceDirections.Unknown;
    public DateTimeOffset EventAt { get; private set; }
    public DateOnly LocalDate { get; private set; }
    public TimeOnly LocalTime { get; private set; }
    public int UtcOffsetMinutes { get; private set; }
    public string? DeviceCode { get; private set; }
    public string? ExternalEventId { get; private set; }
    public string? RawPayloadJson { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public Guid ReceivedBy { get; private set; }

    public static RawAttendanceEvent Create(
        Guid companyId,
        Guid employeeId,
        string source,
        string direction,
        DateTimeOffset eventAt,
        string? deviceCode,
        string? externalEventId,
        string? rawPayloadJson,
        DateTimeOffset receivedAt,
        Guid receivedBy)
    {
        if (companyId == Guid.Empty || employeeId == Guid.Empty || receivedBy == Guid.Empty)
            throw new ArgumentException("Company, employee and receiver are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(direction);

        var normalizedSource = source.Trim().ToUpperInvariant();
        var normalizedDirection = direction.Trim().ToUpperInvariant();
        if (!RawAttendanceSources.IsKnown(normalizedSource)) throw new ArgumentException("Attendance event source is invalid.", nameof(source));
        if (!RawAttendanceDirections.IsKnown(normalizedDirection)) throw new ArgumentException("Attendance direction is invalid.", nameof(direction));

        var externalId = Normalize(externalEventId, 200);
        if (normalizedSource != RawAttendanceSources.Manual && externalId is null)
            throw new ArgumentException("External event id is required for non-manual sources.", nameof(externalEventId));

        var payload = Normalize(rawPayloadJson, 20_000);
        var sourceLocalDateTime = eventAt.DateTime;
        return new RawAttendanceEvent
        {
            CompanyId = companyId,
            EmployeeId = employeeId,
            Source = normalizedSource,
            Direction = normalizedDirection,
            EventAt = eventAt.ToUniversalTime(),
            LocalDate = DateOnly.FromDateTime(sourceLocalDateTime),
            LocalTime = TimeOnly.FromDateTime(sourceLocalDateTime),
            UtcOffsetMinutes = checked((int)eventAt.Offset.TotalMinutes),
            DeviceCode = Normalize(deviceCode, 100),
            ExternalEventId = externalId,
            RawPayloadJson = payload,
            ReceivedAt = receivedAt.ToUniversalTime(),
            ReceivedBy = receivedBy
        };
    }

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentException("Text is too long.");
        return normalized;
    }
}

public static class DailyAttendanceStatuses
{
    public const string Worked = "WORKED";
    public const string Partial = "PARTIAL";
    public const string Leave = "LEAVE";
    public const string Sick = "SICK";
    public const string Absent = "ABSENT";
    public const string Holiday = "HOLIDAY";
    public const string OffDay = "OFF_DAY";
    public const string MissingRecord = "MISSING_RECORD";

    public static bool IsKnown(string value) => value is Worked or Partial or Leave or Sick or Absent or Holiday or OffDay or MissingRecord;
}

public static class DailyAttendanceProcessingStatuses
{
    public const string Calculated = "CALCULATED";
    public const string ReviewRequired = "REVIEW_REQUIRED";
    public const string Approved = "APPROVED";
    public const string Locked = "LOCKED";

    public static bool IsKnown(string value) => value is Calculated or ReviewRequired or Approved or Locked;
}

public sealed class DailyAttendance : AuditableEntity
{
    private DailyAttendance() { }

    public Guid CompanyId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateOnly AttendanceDate { get; private set; }
    public Guid? ShiftAssignmentId { get; private set; }
    public Guid? ShiftId { get; private set; }
    public Guid? WorkCalendarId { get; private set; }
    public Guid? LeaveId { get; private set; }
    public string Status { get; private set; } = DailyAttendanceStatuses.MissingRecord;
    public string ProcessingStatus { get; private set; } = DailyAttendanceProcessingStatuses.ReviewRequired;
    public int PlannedMinutes { get; private set; }
    public int LeaveMinutes { get; private set; }
    public int WorkedMinutes { get; private set; }
    public int LateMinutes { get; private set; }
    public int EarlyLeaveMinutes { get; private set; }
    public int OvertimeCandidateMinutes { get; private set; }
    public DateTimeOffset? FirstInAt { get; private set; }
    public DateTimeOffset? LastOutAt { get; private set; }
    public string SourceSnapshotJson { get; private set; } = "{}";
    public string? CalculationMessage { get; private set; }
    public DateTimeOffset CalculatedAt { get; private set; }

    public static DailyAttendance Create(
        Guid companyId,
        Guid employeeId,
        DateOnly attendanceDate,
        Guid? shiftAssignmentId,
        Guid? shiftId,
        Guid? workCalendarId,
        Guid? leaveId,
        string status,
        string processingStatus,
        int plannedMinutes,
        int leaveMinutes,
        int workedMinutes,
        int lateMinutes,
        int earlyLeaveMinutes,
        int overtimeCandidateMinutes,
        DateTimeOffset? firstInAt,
        DateTimeOffset? lastOutAt,
        string sourceSnapshotJson,
        string? calculationMessage,
        DateTimeOffset now,
        Guid actorUserId)
    {
        if (companyId == Guid.Empty || employeeId == Guid.Empty || actorUserId == Guid.Empty)
            throw new ArgumentException("Company, employee and actor are required.");
        Validate(status, processingStatus, plannedMinutes, leaveMinutes, workedMinutes, lateMinutes, earlyLeaveMinutes, overtimeCandidateMinutes, sourceSnapshotJson);
        return new DailyAttendance
        {
            CompanyId = companyId,
            EmployeeId = employeeId,
            AttendanceDate = attendanceDate,
            ShiftAssignmentId = shiftAssignmentId,
            ShiftId = shiftId,
            WorkCalendarId = workCalendarId,
            LeaveId = leaveId,
            Status = status,
            ProcessingStatus = processingStatus,
            PlannedMinutes = plannedMinutes,
            LeaveMinutes = leaveMinutes,
            WorkedMinutes = workedMinutes,
            LateMinutes = lateMinutes,
            EarlyLeaveMinutes = earlyLeaveMinutes,
            OvertimeCandidateMinutes = overtimeCandidateMinutes,
            FirstInAt = firstInAt?.ToUniversalTime(),
            LastOutAt = lastOutAt?.ToUniversalTime(),
            SourceSnapshotJson = sourceSnapshotJson,
            CalculationMessage = NormalizeMessage(calculationMessage),
            CalculatedAt = now.ToUniversalTime(),
            CreatedAt = now.ToUniversalTime(),
            CreatedBy = actorUserId
        };
    }

    public void Recalculate(
        Guid? shiftAssignmentId,
        Guid? shiftId,
        Guid? workCalendarId,
        Guid? leaveId,
        string status,
        string processingStatus,
        int plannedMinutes,
        int leaveMinutes,
        int workedMinutes,
        int lateMinutes,
        int earlyLeaveMinutes,
        int overtimeCandidateMinutes,
        DateTimeOffset? firstInAt,
        DateTimeOffset? lastOutAt,
        string sourceSnapshotJson,
        string? calculationMessage,
        DateTimeOffset now,
        Guid actorUserId)
    {
        if (ProcessingStatus is DailyAttendanceProcessingStatuses.Approved or DailyAttendanceProcessingStatuses.Locked)
            throw new InvalidOperationException("Approved or locked attendance cannot be recalculated.");
        Validate(status, processingStatus, plannedMinutes, leaveMinutes, workedMinutes, lateMinutes, earlyLeaveMinutes, overtimeCandidateMinutes, sourceSnapshotJson);
        ShiftAssignmentId = shiftAssignmentId;
        ShiftId = shiftId;
        WorkCalendarId = workCalendarId;
        LeaveId = leaveId;
        Status = status;
        ProcessingStatus = processingStatus;
        PlannedMinutes = plannedMinutes;
        LeaveMinutes = leaveMinutes;
        WorkedMinutes = workedMinutes;
        LateMinutes = lateMinutes;
        EarlyLeaveMinutes = earlyLeaveMinutes;
        OvertimeCandidateMinutes = overtimeCandidateMinutes;
        FirstInAt = firstInAt?.ToUniversalTime();
        LastOutAt = lastOutAt?.ToUniversalTime();
        SourceSnapshotJson = sourceSnapshotJson;
        CalculationMessage = NormalizeMessage(calculationMessage);
        CalculatedAt = now.ToUniversalTime();
        UpdatedAt = now.ToUniversalTime();
        UpdatedBy = actorUserId;
        Version++;
    }

    private static void Validate(string status, string processingStatus, int plannedMinutes, int leaveMinutes, int workedMinutes, int lateMinutes, int earlyLeaveMinutes, int overtimeCandidateMinutes, string sourceSnapshotJson)
    {
        if (!DailyAttendanceStatuses.IsKnown(status)) throw new ArgumentException("Daily attendance status is invalid.", nameof(status));
        if (!DailyAttendanceProcessingStatuses.IsKnown(processingStatus)) throw new ArgumentException("Daily attendance processing status is invalid.", nameof(processingStatus));
        if (plannedMinutes < 0 || leaveMinutes < 0 || workedMinutes < 0 || lateMinutes < 0 || earlyLeaveMinutes < 0 || overtimeCandidateMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(plannedMinutes));
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSnapshotJson);
        if (sourceSnapshotJson.Length > 100_000) throw new ArgumentException("Attendance source snapshot is too large.", nameof(sourceSnapshotJson));
    }

    private static string? NormalizeMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;
        var normalized = message.Trim();
        if (normalized.Length > 2000) throw new ArgumentException("Calculation message is too long.", nameof(message));
        return normalized;
    }
}
