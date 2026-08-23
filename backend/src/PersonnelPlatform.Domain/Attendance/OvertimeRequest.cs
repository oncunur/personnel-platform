using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Attendance;

public static class OvertimeRequestStatuses
{
    public const string PendingManager = "PENDING_MANAGER";
    public const string PendingHr = "PENDING_HR";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Cancelled = "CANCELLED";

    public static bool IsKnown(string value) => value is PendingManager or PendingHr or Approved or Rejected or Cancelled;
    public static bool IsTerminal(string value) => value is Approved or Rejected or Cancelled;
}

public sealed class OvertimeRequest : AuditableEntity
{
    private OvertimeRequest() { }

    public Guid CompanyId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid DailyAttendanceId { get; private set; }
    public int SourceDailyVersion { get; private set; }
    public DateOnly AttendanceDate { get; private set; }
    public int CandidateMinutes { get; private set; }
    public int RequestedMinutes { get; private set; }
    public int ApprovedMinutes { get; private set; }
    public string Status { get; private set; } = OvertimeRequestStatuses.PendingManager;
    public string? Reason { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }
    public DateTimeOffset? ManagerDecidedAt { get; private set; }
    public Guid? ManagerDecidedBy { get; private set; }
    public DateTimeOffset? HrDecidedAt { get; private set; }
    public Guid? HrDecidedBy { get; private set; }
    public DateTimeOffset? RejectedAt { get; private set; }
    public Guid? RejectedBy { get; private set; }
    public string? DecisionNote { get; private set; }

    public static OvertimeRequest Create(
        Guid companyId,
        Guid employeeId,
        Guid dailyAttendanceId,
        int sourceDailyVersion,
        DateOnly attendanceDate,
        int candidateMinutes,
        int requestedMinutes,
        string? reason,
        DateTimeOffset now,
        Guid actorUserId)
    {
        if (companyId == Guid.Empty || employeeId == Guid.Empty || dailyAttendanceId == Guid.Empty || actorUserId == Guid.Empty)
            throw new ArgumentException("Company, employee, daily attendance and actor are required.");
        if (sourceDailyVersion <= 0) throw new ArgumentOutOfRangeException(nameof(sourceDailyVersion));
        if (candidateMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(candidateMinutes));
        if (requestedMinutes <= 0 || requestedMinutes > candidateMinutes) throw new ArgumentOutOfRangeException(nameof(requestedMinutes));

        return new OvertimeRequest
        {
            CompanyId = companyId,
            EmployeeId = employeeId,
            DailyAttendanceId = dailyAttendanceId,
            SourceDailyVersion = sourceDailyVersion,
            AttendanceDate = attendanceDate,
            CandidateMinutes = candidateMinutes,
            RequestedMinutes = requestedMinutes,
            ApprovedMinutes = 0,
            Status = OvertimeRequestStatuses.PendingManager,
            Reason = Normalize(reason, 2000),
            SubmittedAt = now.ToUniversalTime(),
            CreatedAt = now.ToUniversalTime(),
            CreatedBy = actorUserId
        };
    }

    public void ApproveManager(Guid actorUserId, string? note, DateTimeOffset now)
    {
        if (Status != OvertimeRequestStatuses.PendingManager) throw new InvalidOperationException("Overtime request is not waiting for manager approval.");
        if (actorUserId == Guid.Empty) throw new ArgumentException("Actor is required.", nameof(actorUserId));
        Status = OvertimeRequestStatuses.PendingHr;
        ManagerDecidedAt = now.ToUniversalTime();
        ManagerDecidedBy = actorUserId;
        DecisionNote = Normalize(note, 2000);
        Touch(now, actorUserId);
    }

    public void ApproveHr(Guid actorUserId, int approvedMinutes, string? note, DateTimeOffset now)
    {
        if (Status != OvertimeRequestStatuses.PendingHr) throw new InvalidOperationException("Overtime request is not waiting for HR approval.");
        if (actorUserId == Guid.Empty) throw new ArgumentException("Actor is required.", nameof(actorUserId));
        if (approvedMinutes <= 0 || approvedMinutes > RequestedMinutes) throw new ArgumentOutOfRangeException(nameof(approvedMinutes));
        Status = OvertimeRequestStatuses.Approved;
        ApprovedMinutes = approvedMinutes;
        HrDecidedAt = now.ToUniversalTime();
        HrDecidedBy = actorUserId;
        DecisionNote = Normalize(note, 2000);
        Touch(now, actorUserId);
    }

    public void Reject(Guid actorUserId, string? note, DateTimeOffset now)
    {
        if (Status is not (OvertimeRequestStatuses.PendingManager or OvertimeRequestStatuses.PendingHr))
            throw new InvalidOperationException("Only pending overtime requests can be rejected.");
        if (actorUserId == Guid.Empty) throw new ArgumentException("Actor is required.", nameof(actorUserId));
        Status = OvertimeRequestStatuses.Rejected;
        ApprovedMinutes = 0;
        RejectedAt = now.ToUniversalTime();
        RejectedBy = actorUserId;
        DecisionNote = Normalize(note, 2000);
        Touch(now, actorUserId);
    }

    public void Cancel(Guid actorUserId, DateTimeOffset now)
    {
        if (Status != OvertimeRequestStatuses.PendingManager)
            throw new InvalidOperationException("Only manager-pending overtime requests can be cancelled by the requester.");
        if (actorUserId == Guid.Empty) throw new ArgumentException("Actor is required.", nameof(actorUserId));
        Status = OvertimeRequestStatuses.Cancelled;
        ApprovedMinutes = 0;
        Touch(now, actorUserId);
    }

    private void Touch(DateTimeOffset now, Guid actorUserId)
    {
        UpdatedAt = now.ToUniversalTime();
        UpdatedBy = actorUserId;
        Version++;
    }

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentException("Text is too long.");
        return normalized;
    }
}
