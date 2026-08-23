using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Personnel;

public static class EmployeeStatuses
{
    public const string Draft = "DRAFT";
    public const string Active = "ACTIVE";
    public const string Suspended = "SUSPENDED";
    public const string Terminated = "TERMINATED";
    public static bool IsKnown(string value) => value is Draft or Active or Suspended or Terminated;
}

public sealed class EmployeeType : Entity
{
    private EmployeeType() { }
    private EmployeeType(Guid id, string code, string name, int displayOrder)
    {
        Id = id;
        Code = code;
        Name = name;
        DisplayOrder = displayOrder;
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public int DisplayOrder { get; private set; }

    public static EmployeeType Seed(Guid id, string code, string name, int displayOrder) => new(id, code, name, displayOrder);
}

public sealed class Employee : AuditableEntity
{
    private Employee() { }

    public Guid CompanyId { get; private set; }
    public Guid? BranchId { get; private set; }
    public Guid DepartmentId { get; private set; }
    public Guid PositionId { get; private set; }
    public Guid EmployeeTypeId { get; private set; }
    public Guid? ManagerEmployeeId { get; private set; }
    public string EmployeeNo { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string? PreferredName { get; private set; }
    public DateOnly? BirthDate { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public DateOnly HireDate { get; private set; }
    public DateOnly? TerminationDate { get; private set; }
    public string Status { get; private set; } = EmployeeStatuses.Draft;
    public string? Notes { get; private set; }

    public static Employee Create(
        Guid companyId,
        Guid? branchId,
        Guid departmentId,
        Guid positionId,
        Guid employeeTypeId,
        Guid? managerEmployeeId,
        string employeeNo,
        string firstName,
        string lastName,
        string? preferredName,
        DateOnly? birthDate,
        string? phone,
        string? email,
        DateOnly hireDate,
        string? notes,
        DateTimeOffset now,
        Guid? actorUserId)
    {
        if (companyId == Guid.Empty || departmentId == Guid.Empty || positionId == Guid.Empty || employeeTypeId == Guid.Empty)
            throw new ArgumentException("Organization and employee type are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeNo);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        var employee = new Employee
        {
            CompanyId = companyId,
            BranchId = branchId,
            DepartmentId = departmentId,
            PositionId = positionId,
            EmployeeTypeId = employeeTypeId,
            ManagerEmployeeId = managerEmployeeId,
            EmployeeNo = Normalize(employeeNo, 50)!,
            FirstName = Normalize(firstName, 100)!,
            LastName = Normalize(lastName, 100)!,
            PreferredName = Normalize(preferredName, 100),
            BirthDate = birthDate,
            Phone = Normalize(phone, 50),
            Email = Normalize(email, 320),
            HireDate = hireDate,
            Notes = Normalize(notes, 2000),
            Status = EmployeeStatuses.Active,
            CreatedAt = now,
            CreatedBy = actorUserId
        };

        if (employee.ManagerEmployeeId == employee.Id) throw new ArgumentException("Employee cannot manage self.", nameof(managerEmployeeId));
        return employee;
    }

    public void Update(
        Guid? branchId,
        Guid departmentId,
        Guid positionId,
        Guid employeeTypeId,
        Guid? managerEmployeeId,
        string firstName,
        string lastName,
        string? preferredName,
        DateOnly? birthDate,
        string? phone,
        string? email,
        string? notes,
        DateTimeOffset now,
        Guid? actorUserId)
    {
        if (managerEmployeeId == Id) throw new ArgumentException("Employee cannot manage self.", nameof(managerEmployeeId));
        BranchId = branchId;
        DepartmentId = departmentId;
        PositionId = positionId;
        EmployeeTypeId = employeeTypeId;
        ManagerEmployeeId = managerEmployeeId;
        FirstName = Normalize(firstName, 100) ?? throw new ArgumentException("First name is required.", nameof(firstName));
        LastName = Normalize(lastName, 100) ?? throw new ArgumentException("Last name is required.", nameof(lastName));
        PreferredName = Normalize(preferredName, 100);
        BirthDate = birthDate;
        Phone = Normalize(phone, 50);
        Email = Normalize(email, 320);
        Notes = Normalize(notes, 2000);
        UpdatedAt = now;
        UpdatedBy = actorUserId;
        Version++;
    }

    public void Suspend(DateTimeOffset now, Guid? actorUserId)
    {
        if (Status == EmployeeStatuses.Terminated) throw new InvalidOperationException("Terminated employee cannot be suspended.");
        Status = EmployeeStatuses.Suspended;
        UpdatedAt = now; UpdatedBy = actorUserId; Version++;
    }

    public void Activate(DateTimeOffset now, Guid? actorUserId)
    {
        if (Status == EmployeeStatuses.Terminated) throw new InvalidOperationException("Terminated employee cannot be activated.");
        Status = EmployeeStatuses.Active;
        UpdatedAt = now; UpdatedBy = actorUserId; Version++;
    }

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength) throw new ArgumentOutOfRangeException(nameof(value));
        return trimmed;
    }
}

public static class ProjectAssignmentStatuses
{
    public const string Active = "ACTIVE";
    public const string Closed = "CLOSED";
}

public sealed class EmployeeProjectAssignment : AuditableEntity
{
    private EmployeeProjectAssignment() { }

    public Guid EmployeeId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid? CostCenterId { get; private set; }
    public DateOnly ValidFrom { get; private set; }
    public DateOnly? ValidUntil { get; private set; }
    public decimal AllocationPercent { get; private set; }
    public string Status { get; private set; } = ProjectAssignmentStatuses.Active;

    public static EmployeeProjectAssignment Create(Guid employeeId, Guid projectId, Guid? costCenterId, DateOnly validFrom, DateOnly? validUntil, decimal allocationPercent, DateTimeOffset now, Guid? actorUserId)
    {
        if (employeeId == Guid.Empty || projectId == Guid.Empty) throw new ArgumentException("Employee and project are required.");
        if (validUntil is not null && validUntil < validFrom) throw new ArgumentException("Assignment end date cannot be before start date.");
        if (allocationPercent <= 0 || allocationPercent > 100) throw new ArgumentOutOfRangeException(nameof(allocationPercent));
        return new EmployeeProjectAssignment
        {
            EmployeeId = employeeId,
            ProjectId = projectId,
            CostCenterId = costCenterId,
            ValidFrom = validFrom,
            ValidUntil = validUntil,
            AllocationPercent = allocationPercent,
            Status = ProjectAssignmentStatuses.Active,
            CreatedAt = now,
            CreatedBy = actorUserId
        };
    }
}
