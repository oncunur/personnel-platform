namespace PersonnelPlatform.Application.Organization;

public sealed record CompanySummary(Guid Id, string Code, string Name, string DefaultCurrency, bool IsActive, int Version);
public sealed record BranchSummary(Guid Id, Guid CompanyId, string Code, string Name, string? Location, bool IsActive, int Version);
public sealed record DepartmentSummary(Guid Id, Guid CompanyId, Guid? BranchId, Guid? ParentDepartmentId, string Code, string Name, bool IsActive, int Version);
public sealed record PositionSummary(Guid Id, Guid DepartmentId, string Code, string Name, bool IsActive, int Version);
public sealed record ProjectSummary(Guid Id, Guid CompanyId, string Code, string Name, string Status, string? Location, string? CountryCode, DateOnly? StartDate, DateOnly? PlannedEndDate, bool IsActive, int Version);
public sealed record CostCenterSummary(Guid Id, Guid CompanyId, Guid? ProjectId, Guid? ParentCostCenterId, string Code, string Name, bool IsActive, int Version);

public sealed record CreateCompanyRequest(string Code, string Name, string? TaxNumber, string? Phone, string? Email, string? Address, string? DefaultCurrency);
public sealed record CreateBranchRequest(Guid CompanyId, string Code, string Name, string? Location, string? Address);
public sealed record CreateDepartmentRequest(Guid CompanyId, Guid? BranchId, Guid? ParentDepartmentId, string Code, string Name);
public sealed record CreatePositionRequest(Guid DepartmentId, string Code, string Name);
public sealed record CreateProjectRequest(Guid CompanyId, string Code, string Name, string? Location, string? CountryCode, DateOnly? StartDate, DateOnly? PlannedEndDate);
public sealed record CreateCostCenterRequest(Guid CompanyId, Guid? ProjectId, Guid? ParentCostCenterId, string Code, string Name);

public sealed record OrganizationResult<T>(bool Succeeded, T? Value, string? ErrorCode, string? ErrorMessage) where T : class
{
    public static OrganizationResult<T> Success(T value) => new(true, value, null, null);
    public static OrganizationResult<T> Failure(string code, string message) => new(false, null, code, message);
}
