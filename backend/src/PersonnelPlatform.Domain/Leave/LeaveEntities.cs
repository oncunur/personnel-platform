using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Leave;

public static class LeaveRequestStatuses
{
    public const string Draft = "DRAFT";
    public const string Submitted = "SUBMITTED";
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Cancelled = "CANCELLED";
    public const string Withdrawn = "WITHDRAWN";
    public const string Completed = "COMPLETED";

    public static bool BlocksOverlap(string status) => status is Submitted or PendingApproval or Approved or Completed;
    public static bool IsTerminal(string status) => status is Rejected or Cancelled or Withdrawn or Completed;
}

public static class LeaveDayParts
{
    public const string FullDay = "FULL_DAY";
    public const string FirstHalf = "FIRST_HALF";
    public const string SecondHalf = "SECOND_HALF";
    public static bool IsKnown(string value) => value is FullDay or FirstHalf or SecondHalf;
}

public sealed class LeaveType : AuditableEntity
{
    private LeaveType() { }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsPaid { get; private set; }
    public bool BalanceRequired { get; private set; }
    public bool AllowHalfDay { get; private set; }
    public bool AttachmentRequired { get; private set; }
    public bool IsActive { get; private set; }
    public int DisplayOrder { get; private set; }

    public static LeaveType Create(string code, string name, string? description, bool isPaid, bool balanceRequired, bool allowHalfDay, bool attachmentRequired, int displayOrder, DateTimeOffset now, Guid? actorUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalizedCode = code.Trim().ToUpperInvariant();
        if (normalizedCode.Length > 80) throw new ArgumentException("Leave type code is too long.", nameof(code));
        if (name.Trim().Length > 150) throw new ArgumentException("Leave type name is too long.", nameof(name));
        if (!string.IsNullOrWhiteSpace(description) && description.Trim().Length > 1000) throw new ArgumentException("Leave type description is too long.", nameof(description));
        return new LeaveType
        {
            Code = normalizedCode,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            IsPaid = isPaid,
            BalanceRequired = balanceRequired,
            AllowHalfDay = allowHalfDay,
            AttachmentRequired = attachmentRequired,
            IsActive = true,
            DisplayOrder = displayOrder,
            CreatedAt = now,
            CreatedBy = actorUserId
        };
    }
}

public sealed class LeaveEntitlement : AuditableEntity
{
    private LeaveEntitlement() { }

    public Guid EmployeeId { get; private set; }
    public Guid LeaveTypeId { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public decimal EntitledDays { get; private set; }
    public decimal CarryOverDays { get; private set; }
    public decimal AdjustmentDays { get; private set; }
    public string? Note { get; private set; }

    public static LeaveEntitlement Create(Guid employeeId, Guid leaveTypeId, DateOnly periodStart, DateOnly periodEnd, decimal entitledDays, decimal carryOverDays, decimal adjustmentDays, string? note, DateTimeOffset now, Guid? actorUserId)
    {
        if (employeeId == Guid.Empty || leaveTypeId == Guid.Empty) throw new ArgumentException("Employee and leave type are required.");
        Validate(periodStart, periodEnd, entitledDays, carryOverDays, adjustmentDays);
        return new LeaveEntitlement
        {
            EmployeeId = employeeId,
            LeaveTypeId = leaveTypeId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            EntitledDays = entitledDays,
            CarryOverDays = carryOverDays,
            AdjustmentDays = adjustmentDays,
            Note = Clean(note, 1000),
            CreatedAt = now,
            CreatedBy = actorUserId
        };
    }

    public void Update(decimal entitledDays, decimal carryOverDays, decimal adjustmentDays, string? note, DateTimeOffset now, Guid? actorUserId)
    {
        Validate(PeriodStart, PeriodEnd, entitledDays, carryOverDays, adjustmentDays);
        EntitledDays = entitledDays;
        CarryOverDays = carryOverDays;
        AdjustmentDays = adjustmentDays;
        Note = Clean(note, 1000);
        UpdatedAt = now;
        UpdatedBy = actorUserId;
        Version++;
    }

    private static void Validate(DateOnly start, DateOnly end, decimal entitled, decimal carry, decimal adjustment)
    {
        if (end < start) throw new ArgumentException("Entitlement period is invalid.");
        if (entitled < 0 || carry < 0) throw new ArgumentOutOfRangeException(nameof(entitled), "Entitlement and carry-over cannot be negative.");
        if (Math.Abs(adjustment) > 10000) throw new ArgumentOutOfRangeException(nameof(adjustment));
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim();
        if (cleaned.Length > maxLength) throw new ArgumentException("Value is too long.");
        return cleaned;
    }
}

public sealed class LeaveBalance : AuditableEntity
{
    private LeaveBalance() { }

    public Guid EmployeeId { get; private set; }
    public Guid LeaveTypeId { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public decimal EntitledDays { get; private set; }
    public decimal CarryOverDays { get; private set; }
    public decimal AdjustmentDays { get; private set; }
    public decimal ReservedDays { get; private set; }
    public decimal UsedDays { get; private set; }
    public decimal AvailableDays => EntitledDays + CarryOverDays + AdjustmentDays - ReservedDays - UsedDays;

    public static LeaveBalance CreateFromEntitlement(LeaveEntitlement entitlement, DateTimeOffset now, Guid? actorUserId) => new()
    {
        EmployeeId = entitlement.EmployeeId,
        LeaveTypeId = entitlement.LeaveTypeId,
        PeriodStart = entitlement.PeriodStart,
        PeriodEnd = entitlement.PeriodEnd,
        EntitledDays = entitlement.EntitledDays,
        CarryOverDays = entitlement.CarryOverDays,
        AdjustmentDays = entitlement.AdjustmentDays,
        ReservedDays = 0,
        UsedDays = 0,
        CreatedAt = now,
        CreatedBy = actorUserId
    };

    public void SyncEntitlement(LeaveEntitlement entitlement, DateTimeOffset now, Guid? actorUserId)
    {
        if (entitlement.EmployeeId != EmployeeId || entitlement.LeaveTypeId != LeaveTypeId || entitlement.PeriodStart != PeriodStart || entitlement.PeriodEnd != PeriodEnd)
            throw new InvalidOperationException("Entitlement does not match balance period.");
        EntitledDays = entitlement.EntitledDays;
        CarryOverDays = entitlement.CarryOverDays;
        AdjustmentDays = entitlement.AdjustmentDays;
        UpdatedAt = now;
        UpdatedBy = actorUserId;
        Version++;
    }

    public void Reserve(decimal days, DateTimeOffset now, Guid? actorUserId)
    {
        if (days <= 0) throw new ArgumentOutOfRangeException(nameof(days));
        if (AvailableDays < days) throw new InvalidOperationException("Insufficient leave balance.");
        ReservedDays += days;
        UpdatedAt = now;
        UpdatedBy = actorUserId;
        Version++;
    }

    public void Release(decimal days, DateTimeOffset now, Guid? actorUserId)
    {
        if (days <= 0) throw new ArgumentOutOfRangeException(nameof(days));
        ReservedDays = Math.Max(0, ReservedDays - days);
        UpdatedAt = now;
        UpdatedBy = actorUserId;
        Version++;
    }

    public void Consume(decimal days, DateTimeOffset now, Guid? actorUserId)
    {
        if (days <= 0) throw new ArgumentOutOfRangeException(nameof(days));
        if (ReservedDays < days) throw new InvalidOperationException("Reserved balance is insufficient.");
        ReservedDays -= days;
        UsedDays += days;
        UpdatedAt = now;
        UpdatedBy = actorUserId;
        Version++;
    }
}

public sealed class LeaveRequest : AuditableEntity
{
    private LeaveRequest() { }

    public Guid EmployeeId { get; private set; }
    public Guid LeaveTypeId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public string StartDayPart { get; private set; } = LeaveDayParts.FullDay;
    public string EndDayPart { get; private set; } = LeaveDayParts.FullDay;
    public decimal RequestedDays { get; private set; }
    public string? Reason { get; private set; }
    public string Status { get; private set; } = LeaveRequestStatuses.Draft;
    public DateTimeOffset? SubmittedAt { get; private set; }
    public DateTimeOffset? WithdrawnAt { get; private set; }

    public static LeaveRequest CreateDraft(Guid employeeId, Guid leaveTypeId, DateOnly startDate, DateOnly endDate, string startDayPart, string endDayPart, decimal requestedDays, string? reason, DateTimeOffset now, Guid? actorUserId)
    {
        if (employeeId == Guid.Empty || leaveTypeId == Guid.Empty) throw new ArgumentException("Employee and leave type are required.");
        if (endDate < startDate) throw new ArgumentException("Leave date range is invalid.");
        if (!LeaveDayParts.IsKnown(startDayPart) || !LeaveDayParts.IsKnown(endDayPart)) throw new ArgumentException("Leave day part is invalid.");
        if (requestedDays <= 0) throw new ArgumentOutOfRangeException(nameof(requestedDays));
        return new LeaveRequest
        {
            EmployeeId = employeeId,
            LeaveTypeId = leaveTypeId,
            StartDate = startDate,
            EndDate = endDate,
            StartDayPart = startDayPart,
            EndDayPart = endDayPart,
            RequestedDays = requestedDays,
            Reason = Clean(reason, 2000),
            Status = LeaveRequestStatuses.Draft,
            CreatedAt = now,
            CreatedBy = actorUserId
        };
    }

    public void Submit(DateTimeOffset now, Guid? actorUserId)
    {
        if (Status != LeaveRequestStatuses.Draft) throw new InvalidOperationException("Only draft leave can be submitted.");
        Status = LeaveRequestStatuses.Submitted;
        SubmittedAt = now;
        UpdatedAt = now;
        UpdatedBy = actorUserId;
        Version++;
    }

    public void MarkPendingApproval(DateTimeOffset now, Guid? actorUserId)
    {
        if (Status != LeaveRequestStatuses.Submitted) throw new InvalidOperationException("Only submitted leave can enter approval.");
        Status = LeaveRequestStatuses.PendingApproval;
        UpdatedAt = now;
        UpdatedBy = actorUserId;
        Version++;
    }

    public void Approve(DateTimeOffset now, Guid? actorUserId)
    {
        if (Status != LeaveRequestStatuses.PendingApproval) throw new InvalidOperationException("Only pending leave can be approved.");
        Status = LeaveRequestStatuses.Approved;
        UpdatedAt = now;
        UpdatedBy = actorUserId;
        Version++;
    }

    public void Reject(DateTimeOffset now, Guid? actorUserId)
    {
        if (Status != LeaveRequestStatuses.PendingApproval) throw new InvalidOperationException("Only pending leave can be rejected.");
        Status = LeaveRequestStatuses.Rejected;
        UpdatedAt = now;
        UpdatedBy = actorUserId;
        Version++;
    }

    public void Withdraw(DateTimeOffset now, Guid? actorUserId)
    {
        if (Status is not (LeaveRequestStatuses.Draft or LeaveRequestStatuses.Submitted or LeaveRequestStatuses.PendingApproval))
            throw new InvalidOperationException("Leave cannot be withdrawn in its current state.");
        Status = LeaveRequestStatuses.Withdrawn;
        WithdrawnAt = now;
        UpdatedAt = now;
        UpdatedBy = actorUserId;
        Version++;
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim();
        if (cleaned.Length > maxLength) throw new ArgumentException("Value is too long.");
        return cleaned;
    }
}
