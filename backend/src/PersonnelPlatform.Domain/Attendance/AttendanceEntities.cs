using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Attendance;

public static class WorkCalendarDayTypes
{
    public const string Workday = "WORKDAY";
    public const string Weekend = "WEEKEND";
    public const string Holiday = "HOLIDAY";
    public const string OffDay = "OFF_DAY";

    public static bool IsKnown(string value) => value is Workday or Weekend or Holiday or OffDay;
}

public sealed class WorkCalendar : AuditableEntity
{
    private WorkCalendar() { }

    public Guid CompanyId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; }

    public static WorkCalendar Create(Guid companyId, string code, string name, bool isDefault, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Company and actor are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalizedCode = code.Trim().ToUpperInvariant();
        var normalizedName = name.Trim();
        if (normalizedCode.Length > 80 || normalizedName.Length > 150) throw new ArgumentException("Calendar code or name is too long.");
        return new WorkCalendar
        {
            CompanyId = companyId,
            Code = normalizedCode,
            Name = normalizedName,
            IsDefault = isDefault,
            IsActive = true,
            CreatedAt = now,
            CreatedBy = actorUserId
        };
    }
}

public sealed class WorkCalendarDay : AuditableEntity
{
    private WorkCalendarDay() { }

    public Guid WorkCalendarId { get; private set; }
    public DateOnly Date { get; private set; }
    public string DayType { get; private set; } = WorkCalendarDayTypes.Workday;
    public int PlannedMinutes { get; private set; }
    public bool IsPaid { get; private set; }
    public string? Description { get; private set; }

    public static WorkCalendarDay Create(Guid workCalendarId, DateOnly date, string dayType, int plannedMinutes, bool isPaid, string? description, DateTimeOffset now, Guid actorUserId)
    {
        if (workCalendarId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Calendar and actor are required.");
        var normalizedType = dayType.Trim().ToUpperInvariant();
        if (!WorkCalendarDayTypes.IsKnown(normalizedType)) throw new ArgumentException("Calendar day type is invalid.", nameof(dayType));
        if (plannedMinutes < 0 || plannedMinutes > 1440) throw new ArgumentOutOfRangeException(nameof(plannedMinutes));
        if (normalizedType != WorkCalendarDayTypes.Workday && plannedMinutes != 0) throw new ArgumentException("Non-work days must have zero planned minutes.", nameof(plannedMinutes));
        return new WorkCalendarDay
        {
            WorkCalendarId = workCalendarId,
            Date = date,
            DayType = normalizedType,
            PlannedMinutes = plannedMinutes,
            IsPaid = isPaid,
            Description = Normalize(description, 500),
            CreatedAt = now,
            CreatedBy = actorUserId
        };
    }

    public void Update(string dayType, int plannedMinutes, bool isPaid, string? description, DateTimeOffset now, Guid actorUserId)
    {
        var normalizedType = dayType.Trim().ToUpperInvariant();
        if (!WorkCalendarDayTypes.IsKnown(normalizedType)) throw new ArgumentException("Calendar day type is invalid.", nameof(dayType));
        if (plannedMinutes < 0 || plannedMinutes > 1440) throw new ArgumentOutOfRangeException(nameof(plannedMinutes));
        if (normalizedType != WorkCalendarDayTypes.Workday && plannedMinutes != 0) throw new ArgumentException("Non-work days must have zero planned minutes.", nameof(plannedMinutes));
        DayType = normalizedType;
        PlannedMinutes = plannedMinutes;
        IsPaid = isPaid;
        Description = Normalize(description, 500);
        UpdatedAt = now;
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

public sealed class ShiftDefinition : AuditableEntity
{
    private ShiftDefinition() { }

    public Guid CompanyId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public int BreakMinutes { get; private set; }
    public int PlannedMinutes { get; private set; }
    public int GraceInMinutes { get; private set; }
    public int GraceOutMinutes { get; private set; }
    public bool CrossesMidnight { get; private set; }
    public bool IsActive { get; private set; }

    public static ShiftDefinition Create(Guid companyId, string code, string name, TimeOnly startTime, TimeOnly endTime, int breakMinutes, int graceInMinutes, int graceOutMinutes, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Company and actor are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (startTime == endTime) throw new ArgumentException("Shift start and end time cannot be equal.");
        if (breakMinutes < 0 || graceInMinutes < 0 || graceOutMinutes < 0) throw new ArgumentOutOfRangeException(nameof(breakMinutes));
        if (graceInMinutes > 240 || graceOutMinutes > 240) throw new ArgumentOutOfRangeException(nameof(graceInMinutes));

        var grossMinutes = CalculateGrossMinutes(startTime, endTime);
        if (breakMinutes >= grossMinutes) throw new ArgumentException("Break duration must be shorter than shift duration.", nameof(breakMinutes));

        var normalizedCode = code.Trim().ToUpperInvariant();
        var normalizedName = name.Trim();
        if (normalizedCode.Length > 80 || normalizedName.Length > 150) throw new ArgumentException("Shift code or name is too long.");

        return new ShiftDefinition
        {
            CompanyId = companyId,
            Code = normalizedCode,
            Name = normalizedName,
            StartTime = startTime,
            EndTime = endTime,
            BreakMinutes = breakMinutes,
            PlannedMinutes = grossMinutes - breakMinutes,
            GraceInMinutes = graceInMinutes,
            GraceOutMinutes = graceOutMinutes,
            CrossesMidnight = endTime <= startTime,
            IsActive = true,
            CreatedAt = now,
            CreatedBy = actorUserId
        };
    }

    public static int CalculateGrossMinutes(TimeOnly startTime, TimeOnly endTime)
    {
        var start = startTime.Hour * 60 + startTime.Minute;
        var end = endTime.Hour * 60 + endTime.Minute;
        if (end <= start) end += 1440;
        return end - start;
    }
}

public sealed class EmployeeShiftAssignment : AuditableEntity
{
    private EmployeeShiftAssignment() { }

    public Guid EmployeeId { get; private set; }
    public Guid ShiftId { get; private set; }
    public Guid WorkCalendarId { get; private set; }
    public DateOnly ValidFrom { get; private set; }
    public DateOnly? ValidUntil { get; private set; }
    public string? Note { get; private set; }

    public static EmployeeShiftAssignment Create(Guid employeeId, Guid shiftId, Guid workCalendarId, DateOnly validFrom, DateOnly? validUntil, string? note, DateTimeOffset now, Guid actorUserId)
    {
        if (employeeId == Guid.Empty || shiftId == Guid.Empty || workCalendarId == Guid.Empty || actorUserId == Guid.Empty)
            throw new ArgumentException("Employee, shift, calendar and actor are required.");
        if (validUntil is not null && validUntil < validFrom) throw new ArgumentException("Assignment end date cannot be before start date.");
        var normalizedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (normalizedNote?.Length > 1000) throw new ArgumentException("Assignment note is too long.", nameof(note));
        return new EmployeeShiftAssignment
        {
            EmployeeId = employeeId,
            ShiftId = shiftId,
            WorkCalendarId = workCalendarId,
            ValidFrom = validFrom,
            ValidUntil = validUntil,
            Note = normalizedNote,
            CreatedAt = now,
            CreatedBy = actorUserId
        };
    }
}
