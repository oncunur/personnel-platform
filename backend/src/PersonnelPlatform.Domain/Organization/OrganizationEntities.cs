using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Organization;

public abstract class OrganizationEntity : AuditableEntity
{
    public string Code { get; protected set; } = string.Empty;
    public string Name { get; protected set; } = string.Empty;
    public bool IsActive { get; protected set; } = true;

    protected void Initialize(string code, string name, DateTimeOffset now, Guid? actorUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        IsActive = true;
        CreatedAt = now;
        CreatedBy = actorUserId;
    }

    public void SetActive(bool active, DateTimeOffset now, Guid? actorUserId)
    {
        if (IsActive == active) return;
        IsActive = active;
        UpdatedAt = now;
        UpdatedBy = actorUserId;
        Version++;
    }
}

public sealed class Company : OrganizationEntity
{
    private Company() { }

    public string? TaxNumber { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public string DefaultCurrency { get; private set; } = "TRY";

    public static Company Create(string code, string name, string? taxNumber, string? phone, string? email, string? address, string defaultCurrency, DateTimeOffset now, Guid? actorUserId)
    {
        var company = new Company();
        company.Initialize(code, name, now, actorUserId);
        company.TaxNumber = Clean(taxNumber, 50);
        company.Phone = Clean(phone, 50);
        company.Email = Clean(email, 320);
        company.Address = Clean(address, 1000);
        company.DefaultCurrency = NormalizeCurrency(defaultCurrency);
        return company;
    }

    private static string NormalizeCurrency(string value)
    {
        var currency = string.IsNullOrWhiteSpace(value) ? "TRY" : value.Trim().ToUpperInvariant();
        if (currency.Length != 3) throw new ArgumentException("Currency must be a 3-letter ISO code.", nameof(value));
        return currency;
    }

    internal static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength) throw new ArgumentOutOfRangeException(nameof(value));
        return trimmed;
    }
}

public sealed class Branch : OrganizationEntity
{
    private Branch() { }
    public Guid CompanyId { get; private set; }
    public string? Location { get; private set; }
    public string? Address { get; private set; }

    public static Branch Create(Guid companyId, string code, string name, string? location, string? address, DateTimeOffset now, Guid? actorUserId)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("Company id is required.", nameof(companyId));
        var branch = new Branch { CompanyId = companyId, Location = Company.Clean(location, 200), Address = Company.Clean(address, 1000) };
        branch.Initialize(code, name, now, actorUserId);
        return branch;
    }
}

public sealed class Department : OrganizationEntity
{
    private Department() { }
    public Guid CompanyId { get; private set; }
    public Guid? BranchId { get; private set; }
    public Guid? ParentDepartmentId { get; private set; }
    public Guid? ManagerEmployeeId { get; private set; }

    public static Department Create(Guid companyId, Guid? branchId, Guid? parentDepartmentId, string code, string name, DateTimeOffset now, Guid? actorUserId)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("Company id is required.", nameof(companyId));
        var department = new Department { CompanyId = companyId, BranchId = branchId, ParentDepartmentId = parentDepartmentId };
        department.Initialize(code, name, now, actorUserId);
        if (department.Id == parentDepartmentId) throw new ArgumentException("Department cannot be its own parent.", nameof(parentDepartmentId));
        return department;
    }
}

public sealed class Position : OrganizationEntity
{
    private Position() { }
    public Guid DepartmentId { get; private set; }

    public static Position Create(Guid departmentId, string code, string name, DateTimeOffset now, Guid? actorUserId)
    {
        if (departmentId == Guid.Empty) throw new ArgumentException("Department id is required.", nameof(departmentId));
        var position = new Position { DepartmentId = departmentId };
        position.Initialize(code, name, now, actorUserId);
        return position;
    }
}

public static class ProjectStatuses
{
    public const string Draft = "DRAFT";
    public const string Planned = "PLANNED";
    public const string Active = "ACTIVE";
    public const string OnHold = "ON_HOLD";
    public const string Closing = "CLOSING";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";
    public static bool IsKnown(string value) => value is Draft or Planned or Active or OnHold or Closing or Completed or Cancelled;
}

public sealed class Project : OrganizationEntity
{
    private Project() { }
    public Guid CompanyId { get; private set; }
    public string? Location { get; private set; }
    public string? CountryCode { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public DateOnly? PlannedEndDate { get; private set; }
    public DateOnly? ActualEndDate { get; private set; }
    public Guid? ManagerEmployeeId { get; private set; }
    public Guid? DefaultCostCenterId { get; private set; }
    public string Status { get; private set; } = ProjectStatuses.Draft;

    public static Project Create(Guid companyId, string code, string name, string? location, string? countryCode, DateOnly? startDate, DateOnly? plannedEndDate, DateTimeOffset now, Guid? actorUserId)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("Company id is required.", nameof(companyId));
        if (startDate is not null && plannedEndDate is not null && plannedEndDate < startDate) throw new ArgumentException("Planned end date must not be before start date.", nameof(plannedEndDate));
        var project = new Project
        {
            CompanyId = companyId,
            Location = Company.Clean(location, 200),
            CountryCode = NormalizeCountry(countryCode),
            StartDate = startDate,
            PlannedEndDate = plannedEndDate,
            Status = ProjectStatuses.Draft
        };
        project.Initialize(code, name, now, actorUserId);
        return project;
    }

    public void ChangeStatus(string status, DateTimeOffset now, Guid? actorUserId)
    {
        var normalized = status.Trim().ToUpperInvariant();
        if (!ProjectStatuses.IsKnown(normalized)) throw new ArgumentException("Unknown project status.", nameof(status));
        if (Status == normalized) return;
        Status = normalized;
        UpdatedAt = now;
        UpdatedBy = actorUserId;
        Version++;
    }

    private static string? NormalizeCountry(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var country = value.Trim().ToUpperInvariant();
        if (country.Length is < 2 or > 3) throw new ArgumentException("Country code must contain 2 or 3 letters.", nameof(value));
        return country;
    }
}

public sealed class CostCenter : OrganizationEntity
{
    private CostCenter() { }
    public Guid CompanyId { get; private set; }
    public Guid? ProjectId { get; private set; }
    public Guid? ParentCostCenterId { get; private set; }

    public static CostCenter Create(Guid companyId, Guid? projectId, Guid? parentCostCenterId, string code, string name, DateTimeOffset now, Guid? actorUserId)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("Company id is required.", nameof(companyId));
        var costCenter = new CostCenter { CompanyId = companyId, ProjectId = projectId, ParentCostCenterId = parentCostCenterId };
        costCenter.Initialize(code, name, now, actorUserId);
        if (costCenter.Id == parentCostCenterId) throw new ArgumentException("Cost center cannot be its own parent.", nameof(parentCostCenterId));
        return costCenter;
    }
}
